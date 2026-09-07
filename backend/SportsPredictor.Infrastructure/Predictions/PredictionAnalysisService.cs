using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.Predictions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Predictions;

/// <summary>
/// Orchestrates the "expert analyst" flow: Dataset Builder (current features) → ML
/// service's POST /analyze/football-1x2 (SHAP + Kelly + narrative, entirely stateless
/// on the Python side) → persisted PredictionExplanation snapshot. Keyed by
/// MatchId + ModelVersionId rather than a specific Prediction row, so it can run
/// independently of whether GeneratePrediction1X2Async has already run for this match.
/// </summary>
public sealed class PredictionAnalysisService : IPredictionAnalysisService
{
    private const string Market = "Match Winner";
    private const string ModelName = "football_1x2";

    private readonly SportsPredictorDbContext _dbContext;
    private readonly IDatasetBuilderService _datasetBuilderService;
    private readonly IMlServiceClient _mlServiceClient;

    public PredictionAnalysisService(
        SportsPredictorDbContext dbContext,
        IDatasetBuilderService datasetBuilderService,
        IMlServiceClient mlServiceClient)
    {
        _dbContext = dbContext;
        _datasetBuilderService = datasetBuilderService;
        _mlServiceClient = mlServiceClient;
    }

    public async Task<PredictionExplanationDto> AnalyzeMatchAsync(Guid matchId, Guid? modelVersionId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches
            .Include(m => m.Competition)
            .Include(m => m.Season)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        var modelVersion = modelVersionId is Guid id
            ? await _dbContext.ModelVersions.FindAsync([id], cancellationToken) ?? throw new NotFoundException(nameof(ModelVersion), id)
            : await _dbContext.ModelVersions
                .Where(m => m.ModelName == ModelName)
                .OrderByDescending(m => m.TrainedAt)
                .FirstOrDefaultAsync(cancellationToken)
              ?? throw new InvalidOperationException($"No '{ModelName}' ModelVersion exists yet — train one first (POST .../train-football-1x2/{{trackedCompetitionId}}).");

        var features = await _datasetBuilderService.ComputeFeaturesForPredictionAsync(matchId, cancellationToken);

        // API-FOOTBALL-style status codes, same convention ml/narrative/generate.py
        // uses to decide whether web_search is worth enabling — "FT" for anything
        // already finished, "NS" (not started) for everything else.
        var fixture = new AnalyzeFixtureContextDto(
            match.HomeTeam?.Name ?? "Unknown",
            match.AwayTeam?.Name ?? "Unknown",
            match.Competition?.Name ?? "Unknown",
            match.Competition?.Country ?? "Unknown",
            match.Season?.Name ?? "Unknown",
            match.Status == MatchStatus.Finished ? "FT" : "NS");

        var odds = await ResolveOddsAsync(matchId, cancellationToken);

        var result = await _mlServiceClient.AnalyzeFootball1X2Async(modelVersion.ArtifactPath, features, fixture, odds, cancellationToken);

        var shapFeatures = result.ShapTopFeatures.Select(f => new ShapFeatureDto(f.Feature, f.Impact)).ToList();
        var stakes = result.Stakes?.ToDictionary(
            kv => kv.Key,
            kv => new StakeDto(
                kv.Value.Label, kv.Value.DecimalOdds, kv.Value.ImpliedProbability, kv.Value.ModelProbability,
                kv.Value.Edge, kv.Value.IsValueBet, kv.Value.KellyFractionFull, kv.Value.SuggestedStakePctBankroll));

        var entity = new PredictionExplanation
        {
            MatchId = matchId,
            ModelVersionId = modelVersion.Id,
            GeneratedAt = DateTime.UtcNow,
            ProbabilityHome = result.Home,
            ProbabilityDraw = result.Draw,
            ProbabilityAway = result.Away,
            ShapTopFeaturesJson = JsonSerializer.Serialize(shapFeatures),
            StakesJson = stakes is not null ? JsonSerializer.Serialize(stakes) : null,
            NarrativeText = result.Narrative,
        };

        _dbContext.PredictionExplanations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity, shapFeatures, stakes);
    }

    public async Task<PredictionExplanationDto?> GetLatestAnalysisForMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PredictionExplanations
            .Where(e => e.MatchId == matchId)
            .OrderByDescending(e => e.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var shapFeatures = JsonSerializer.Deserialize<List<ShapFeatureDto>>(entity.ShapTopFeaturesJson) ?? [];
        var stakes = entity.StakesJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, StakeDto>>(entity.StakesJson)
            : null;

        return ToDto(entity, shapFeatures, stakes);
    }

    /// <summary>Latest odds per selection for this match, only when all three (Home/Draw/Away) are available — Kelly staking needs a complete market, not a partial one.</summary>
    private async Task<AnalyzeOddsDto?> ResolveOddsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var latestBySelection = (await _dbContext.OddsSnapshots
                .Where(o => o.MatchId == matchId && o.Market == Market)
                .ToListAsync(cancellationToken))
            .GroupBy(o => o.Selection)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.CapturedAt).First());

        if (latestBySelection.TryGetValue("Home", out var home) &&
            latestBySelection.TryGetValue("Draw", out var draw) &&
            latestBySelection.TryGetValue("Away", out var away))
        {
            return new AnalyzeOddsDto((double)home.DecimalOdds, (double)draw.DecimalOdds, (double)away.DecimalOdds);
        }

        return null;
    }

    private static PredictionExplanationDto ToDto(
        PredictionExplanation entity, IReadOnlyList<ShapFeatureDto> shapFeatures, IReadOnlyDictionary<string, StakeDto>? stakes) =>
        new(
            entity.Id, entity.MatchId, entity.ModelVersionId, entity.GeneratedAt,
            entity.ProbabilityHome, entity.ProbabilityDraw, entity.ProbabilityAway,
            shapFeatures, stakes, entity.NarrativeText);
}
