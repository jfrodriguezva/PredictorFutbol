namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Corresponds to CLAUDE.md section 23: captures API-Football's own prediction for a
/// match as an external benchmark — never treated as our model's output or as ground truth.
/// </summary>
public interface IFootballPredictionSyncService
{
    Task<PredictionSyncResultDto> SyncPredictionAsync(Guid matchId, CancellationToken cancellationToken);
}
