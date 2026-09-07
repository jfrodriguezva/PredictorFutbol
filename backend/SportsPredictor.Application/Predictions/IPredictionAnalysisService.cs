namespace SportsPredictor.Application.Predictions;

/// <summary>
/// The "expert analyst" explanation on top of a plain 1X2 prediction: SHAP feature
/// attribution, Kelly-Criterion stake sizing (when odds are available), and a
/// narrative — see ml/app/main.py's POST /analyze/football-1x2. Persisted as an
/// immutable PredictionExplanation snapshot, same ownership rule as
/// IPredictionGenerationService (the ML service never persists anything itself).
/// </summary>
public interface IPredictionAnalysisService
{
    Task<PredictionExplanationDto> AnalyzeMatchAsync(Guid matchId, Guid? modelVersionId, CancellationToken cancellationToken);

    /// <summary>Most recent analysis already generated for this match, or null if none exists yet.</summary>
    Task<PredictionExplanationDto?> GetLatestAnalysisForMatchAsync(Guid matchId, CancellationToken cancellationToken);
}
