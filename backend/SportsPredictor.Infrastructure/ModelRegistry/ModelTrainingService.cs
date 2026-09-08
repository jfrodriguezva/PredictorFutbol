using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.ModelRegistry;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ModelRegistry;

/// <summary>
/// Orchestrates Phase 9: exports the dataset (Phase 8), asks the Python ML service to
/// train (never touches SQLite itself), then registers the result here — this is the
/// only place ModelVersion/TrainingRun rows get written, keeping C# the sole database
/// owner (Phase 8/9 decision).
/// </summary>
public sealed class ModelTrainingService : IModelTrainingService
{
    private const string ModelName = "football_1x2";
    private const string Sport = "Football";

    private readonly IDatasetBuilderService _datasetBuilderService;
    private readonly IMlServiceClient _mlServiceClient;
    private readonly SportsPredictorDbContext _dbContext;

    public ModelTrainingService(IDatasetBuilderService datasetBuilderService, IMlServiceClient mlServiceClient, SportsPredictorDbContext dbContext)
    {
        _datasetBuilderService = datasetBuilderService;
        _mlServiceClient = mlServiceClient;
        _dbContext = dbContext;
    }

    public async Task<ModelVersionDto> TrainFootball1X2Async(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        var csvContent = await _datasetBuilderService.ExportDatasetCsvAsync(trackedCompetitionId, cancellationToken);
        var result = await _mlServiceClient.TrainFootball1X2Async(csvContent, ModelName, cancellationToken);
        var selected = result.AlgorithmResults.First(r => r.Algorithm == result.SelectedAlgorithm);

        // A new training run always creates a new ModelVersion — existing versions
        // are never overwritten (CLAUDE.md section 28/29).
        var modelVersion = new ModelVersion
        {
            ModelName = ModelName,
            Sport = Sport,
            Version = result.Version,
            Algorithm = result.SelectedAlgorithm,
            TrainedAt = result.TrainedAtUtc,
            TrainingStartDate = result.TrainingStartDate,
            TrainingEndDate = result.TrainingEndDate,
            LogLoss = selected.LogLoss,
            BrierScore = selected.BrierScore,
            Accuracy = selected.Accuracy,
            ArtifactPath = result.ArtifactPath,
            Active = false,
        };
        _dbContext.ModelVersions.Add(modelVersion);

        _dbContext.TrainingRuns.Add(new TrainingRun
        {
            ModelVersionId = modelVersion.Id,
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            DatasetSize = result.DatasetSize,
            TrainingParametersJson = JsonSerializer.Serialize(new { trackedCompetitionId }),
            MetricsJson = JsonSerializer.Serialize(result.AlgorithmResults),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(modelVersion);
    }

    public async Task<ModelVersionDto> TrainFootball1X2GlobalAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        var csvContent = await _datasetBuilderService.ExportGlobalDatasetCsvAsync(cancellationToken);
        var result = await _mlServiceClient.TrainFootball1X2Async(csvContent, ModelName, cancellationToken);
        var selected = result.AlgorithmResults.First(r => r.Algorithm == result.SelectedAlgorithm);

        var modelVersion = new ModelVersion
        {
            ModelName = ModelName,
            Sport = Sport,
            Version = result.Version,
            Algorithm = result.SelectedAlgorithm,
            TrainedAt = result.TrainedAtUtc,
            TrainingStartDate = result.TrainingStartDate,
            TrainingEndDate = result.TrainingEndDate,
            LogLoss = selected.LogLoss,
            BrierScore = selected.BrierScore,
            Accuracy = selected.Accuracy,
            ArtifactPath = result.ArtifactPath,
            Active = false,
        };
        _dbContext.ModelVersions.Add(modelVersion);

        _dbContext.TrainingRuns.Add(new TrainingRun
        {
            ModelVersionId = modelVersion.Id,
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            DatasetSize = result.DatasetSize,
            TrainingParametersJson = JsonSerializer.Serialize(new { scope = "global-all-competitions" }),
            MetricsJson = JsonSerializer.Serialize(result.AlgorithmResults),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(modelVersion);
    }

    public async Task<IReadOnlyList<ModelVersionDto>> GetModelVersionsAsync(CancellationToken cancellationToken)
    {
        var versions = await _dbContext.ModelVersions
            .OrderByDescending(v => v.TrainedAt)
            .ToListAsync(cancellationToken);

        return versions.Select(ToDto).ToList();
    }

    public async Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(Guid trackedCompetitionId, int windows, CancellationToken cancellationToken)
    {
        var csvContent = await _datasetBuilderService.ExportDatasetCsvAsync(trackedCompetitionId, cancellationToken);
        return await _mlServiceClient.EvaluateFootball1X2Async(csvContent, windows, cancellationToken);
    }

    public async Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2GlobalAsync(int windows, CancellationToken cancellationToken)
    {
        var csvContent = await _datasetBuilderService.ExportGlobalDatasetCsvAsync(cancellationToken);
        return await _mlServiceClient.EvaluateFootball1X2Async(csvContent, windows, cancellationToken);
    }

    public async Task<PredictGoalsResultDto> PredictGoalsAsync(Guid trackedCompetitionId, Guid homeTeamId, Guid awayTeamId, double? dixonColesRho, CancellationToken cancellationToken)
    {
        var homeTeam = await _dbContext.Teams.FindAsync([homeTeamId], cancellationToken)
            ?? throw new NotFoundException(nameof(Team), homeTeamId);
        var awayTeam = await _dbContext.Teams.FindAsync([awayTeamId], cancellationToken)
            ?? throw new NotFoundException(nameof(Team), awayTeamId);

        var csvContent = await _datasetBuilderService.ExportDatasetCsvAsync(trackedCompetitionId, cancellationToken);
        return await _mlServiceClient.PredictGoalsAsync(csvContent, homeTeam.Name, awayTeam.Name, dixonColesRho, cancellationToken);
    }

    public async Task<ModelVersionDto> ActivateAsync(Guid modelVersionId, CancellationToken cancellationToken)
    {
        var target = await _dbContext.ModelVersions.FindAsync([modelVersionId], cancellationToken)
            ?? throw new NotFoundException(nameof(ModelVersion), modelVersionId);

        var siblings = await _dbContext.ModelVersions
            .Where(v => v.ModelName == target.ModelName && v.Id != target.Id && v.Active)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.Active = false;
        }

        target.Active = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(target);
    }

    private static ModelVersionDto ToDto(ModelVersion version) => new(
        version.Id,
        version.ModelName,
        version.Sport,
        version.Version,
        version.Algorithm,
        version.TrainedAt,
        version.TrainingStartDate,
        version.TrainingEndDate,
        version.BrierScore,
        version.LogLoss,
        version.Accuracy,
        version.ArtifactPath,
        version.Active);
}
