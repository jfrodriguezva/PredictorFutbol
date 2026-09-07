namespace SportsPredictor.Application.Predictions;

/// <summary>
/// This is the actual "generate a prediction" flow: computes a match's current
/// features (Dataset Builder), scores them with a trained model artifact, prices
/// them against known odds (Expected Value, CLAUDE.md section 25), and persists a
/// new Prediction snapshot per selection — never modified after the fact
/// (CLAUDE.md section 29).
/// </summary>
public interface IPredictionGenerationService
{
    Task<GeneratePredictionResultDto> GeneratePrediction1X2Async(Guid matchId, Guid? modelVersionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PredictionDto>> GetPredictionsForMatchAsync(Guid matchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PredictionDto>> GetRecommendedPredictionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records the real outcome of a finished match against every Prediction already
    /// made for it (sets ActualOutcome/IsCorrect only). Throws if the match isn't
    /// finished yet — there is nothing real to compare against.
    /// </summary>
    Task<EvaluateMatchResultDto> EvaluateMatchAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// Aggregate hit-rate across every evaluated prediction (optionally scoped to one
    /// ModelVersion) plus the most recent misses among Recommended selections — the
    /// "where did the model get it wrong" view.
    /// </summary>
    Task<AccuracySummaryDto> GetAccuracySummaryAsync(Guid? modelVersionId, CancellationToken cancellationToken);
}
