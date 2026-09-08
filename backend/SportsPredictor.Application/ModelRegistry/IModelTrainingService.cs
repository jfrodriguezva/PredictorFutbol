using SportsPredictor.Application.MlService;

namespace SportsPredictor.Application.ModelRegistry;

/// <summary>
/// Orchestrates Phase 9's "Football 1X2 baseline": exports the tracked competition's
/// dataset (Phase 8), asks the Python ML service to train and compare algorithms, and
/// registers the winning run as a new ModelVersion + TrainingRun — never overwriting
/// a previous version (CLAUDE.md section 28/29).
/// </summary>
public interface IModelTrainingService
{
    Task<ModelVersionDto> TrainFootball1X2Async(Guid trackedCompetitionId, CancellationToken cancellationToken);

    /// <summary>Same as <see cref="TrainFootball1X2Async"/> but pools every tracked competition's finished matches into one dataset.</summary>
    Task<ModelVersionDto> TrainFootball1X2GlobalAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ModelVersionDto>> GetModelVersionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Phase 10: calibration + walk-forward backtest + benchmark comparison for a
    /// tracked competition's dataset. Purely diagnostic — nothing is persisted.
    /// </summary>
    Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(Guid trackedCompetitionId, int windows, CancellationToken cancellationToken);

    /// <summary>Same as <see cref="EvaluateFootball1X2Async"/> but over the pooled all-competitions dataset. Purely diagnostic.</summary>
    Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2GlobalAsync(int windows, CancellationToken cancellationToken);

    /// <summary>
    /// Phase 11: Expected Goals (Poisson/Dixon-Coles) for a hypothetical fixture
    /// between two known teams. Purely diagnostic — nothing is persisted.
    /// </summary>
    Task<PredictGoalsResultDto> PredictGoalsAsync(Guid trackedCompetitionId, Guid homeTeamId, Guid awayTeamId, double? dixonColesRho, CancellationToken cancellationToken);

    /// <summary>
    /// Marks this ModelVersion as the one predictions should use, and deactivates every
    /// other version with the same ModelName (at most one active version per model at a
    /// time). Without ever calling this, generation/analysis fall back to "most recently
    /// trained" — same as before this feature existed.
    /// </summary>
    Task<ModelVersionDto> ActivateAsync(Guid modelVersionId, CancellationToken cancellationToken);
}
