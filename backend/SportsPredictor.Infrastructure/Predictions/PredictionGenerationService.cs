using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Betting;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.Predictions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Predictions;

/// <summary>
/// Orchestrates the end-to-end "generate a prediction" flow: Dataset Builder (current
/// features) → ML service (scores them against a saved artifact) → Expected Value
/// (against known odds) → persisted Prediction rows. C# is the only thing that ever
/// writes Predictions — the ML service only ever returns numbers (same ownership
/// rule as Phase 9's ModelTrainingService).
/// </summary>
public sealed class PredictionGenerationService : IPredictionGenerationService
{
    private const string Market = "Match Winner";
    private const string ModelName = "football_1x2";

    private readonly SportsPredictorDbContext _dbContext;
    private readonly IDatasetBuilderService _datasetBuilderService;
    private readonly IMlServiceClient _mlServiceClient;

    public PredictionGenerationService(
        SportsPredictorDbContext dbContext,
        IDatasetBuilderService datasetBuilderService,
        IMlServiceClient mlServiceClient)
    {
        _dbContext = dbContext;
        _datasetBuilderService = datasetBuilderService;
        _mlServiceClient = mlServiceClient;
    }

    public async Task<GeneratePredictionResultDto> GeneratePrediction1X2Async(Guid matchId, Guid? modelVersionId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        var modelVersion = modelVersionId is Guid id
            ? await _dbContext.ModelVersions.FindAsync([id], cancellationToken) ?? throw new NotFoundException(nameof(ModelVersion), id)
            : await _dbContext.ModelVersions
                .Where(m => m.ModelName == ModelName)
                .OrderByDescending(m => m.Active)
                .ThenByDescending(m => m.TrainedAt)
                .FirstOrDefaultAsync(cancellationToken)
              ?? throw new InvalidOperationException($"No '{ModelName}' ModelVersion exists yet — train one first (POST .../train-football-1x2/{{trackedCompetitionId}}).");

        var features = await _datasetBuilderService.ComputeFeaturesForPredictionAsync(matchId, cancellationToken);
        var probabilities = await _mlServiceClient.PredictMatch1X2Async(modelVersion.ArtifactPath, features, cancellationToken);

        var predictionDate = DateTime.UtcNow;
        var oddsForMatch = await _dbContext.OddsSnapshots
            .Where(o => o.MatchId == matchId && o.Market == Market)
            .ToListAsync(cancellationToken);

        var selections = new (string Selection, double Probability)[]
        {
            ("Home", probabilities.Home),
            ("Draw", probabilities.Draw),
            ("Away", probabilities.Away),
        };

        var rows = new List<Prediction>();
        Prediction? bestPositiveEvRow = null;
        double bestEv = double.NegativeInfinity;

        foreach (var (selection, probability) in selections)
        {
            var latestOdds = oddsForMatch
                .Where(o => o.Selection == selection)
                .OrderByDescending(o => o.CapturedAt)
                .FirstOrDefault();

            double? expectedValue = latestOdds is not null
                ? ExpectedValueCalculator.Calculate(probability, latestOdds.DecimalOdds)
                : null;

            var row = new Prediction
            {
                MatchId = matchId,
                ModelVersionId = modelVersion.Id,
                PredictionDate = predictionDate,
                Market = Market,
                Selection = selection,
                Probability = probability,
                ExpectedValue = expectedValue,
                Recommended = false,
            };
            rows.Add(row);

            if (expectedValue is double ev && ev > bestEv)
            {
                bestEv = ev;
                bestPositiveEvRow = row;
            }
        }

        // Never recommend purely on favorite status (CLAUDE.md section 25) — only a
        // genuinely positive-EV selection ever gets Recommended = true.
        if (bestPositiveEvRow is not null && ExpectedValueCalculator.Categorize(bestEv) is ValueCategory.Value or ValueCategory.StrongValue)
        {
            bestPositiveEvRow.Recommended = true;
        }

        _dbContext.Predictions.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GeneratePredictionResultDto(matchId, modelVersion.Id, predictionDate, rows.Select(ToDto).ToList());
    }

    public async Task<IReadOnlyList<PredictionDto>> GetPredictionsForMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Predictions
            .Where(p => p.MatchId == matchId)
            .OrderByDescending(p => p.PredictionDate)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PredictionDto>> GetRecommendedPredictionsAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Predictions
            .Where(p => p.Recommended)
            .OrderByDescending(p => p.ExpectedValue)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<EvaluateMatchResultDto> EvaluateMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        if (match.Status != MatchStatus.Finished || match.HomeScore is null || match.AwayScore is null)
        {
            throw new InvalidOperationException(
                "This match hasn't finished yet — there is no real result to evaluate predictions against. " +
                "Sync fixtures again once it's played.");
        }

        var actualOutcome = match.HomeScore > match.AwayScore ? "Home" : match.HomeScore < match.AwayScore ? "Away" : "Draw";

        var predictions = await _dbContext.Predictions
            .Where(p => p.MatchId == matchId && p.Market == Market)
            .ToListAsync(cancellationToken);

        foreach (var prediction in predictions)
        {
            // Only ActualOutcome/IsCorrect are ever touched here — Probability, ExpectedValue,
            // Selection and PredictionDate stay exactly as they were when the forecast was made
            // (CLAUDE.md section 29: never rewrite a prediction after the fact).
            prediction.ActualOutcome = actualOutcome;
            prediction.IsCorrect = prediction.Selection == actualOutcome;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EvaluateMatchResultDto(matchId, actualOutcome, predictions.Select(ToDto).ToList());
    }

    public async Task<AccuracySummaryDto> GetAccuracySummaryAsync(Guid? modelVersionId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Predictions.Where(p => p.IsCorrect != null);
        if (modelVersionId is Guid id)
        {
            query = query.Where(p => p.ModelVersionId == id);
        }

        var evaluated = await query.ToListAsync(cancellationToken);
        var total = evaluated.Count;
        var correct = evaluated.Count(p => p.IsCorrect == true);

        var recommended = evaluated.Where(p => p.Recommended).ToList();
        var recommendedCorrect = recommended.Count(p => p.IsCorrect == true);

        var recentMisses = evaluated
            .Where(p => p.Recommended && p.IsCorrect == false)
            .OrderByDescending(p => p.PredictionDate)
            .Take(20)
            .Select(p => new MissedPredictionDto(p.MatchId, p.Market, p.Selection, p.Probability, p.ActualOutcome!, p.PredictionDate))
            .ToList();

        return new AccuracySummaryDto(
            TotalEvaluated: total,
            CorrectCount: correct,
            Accuracy: total > 0 ? (double)correct / total : 0.0,
            RecommendedTotal: recommended.Count,
            RecommendedCorrect: recommendedCorrect,
            RecommendedAccuracy: recommended.Count > 0 ? (double)recommendedCorrect / recommended.Count : 0.0,
            RecentMisses: recentMisses);
    }

    private static PredictionDto ToDto(Prediction p) => new(
        p.Id, p.MatchId, p.ModelVersionId, p.PredictionDate, p.Market, p.Selection, p.Probability,
        p.ExpectedValue, p.ExpectedValue is double v ? ExpectedValueCalculator.Categorize(v).ToString() : null,
        p.Recommended, p.ActualOutcome, p.IsCorrect);
}
