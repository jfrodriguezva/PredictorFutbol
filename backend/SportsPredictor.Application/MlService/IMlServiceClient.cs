namespace SportsPredictor.Application.MlService;

/// <summary>
/// Application-facing contract for the Python ML service (ml/). The ML service never
/// touches SQLite itself — it only ever receives data handed to it by the backend and
/// returns metadata; the backend is the one that persists ModelVersion/TrainingRun
/// (Phase 8/9 decision: C# is the sole owner of the database).
/// </summary>
public interface IMlServiceClient
{
    Task<TrainFootball1X2ResultDto> TrainFootball1X2Async(string csvContent, string modelName, CancellationToken cancellationToken);

    /// <summary>Phase 10: calibration + walk-forward backtest + benchmark comparison. Diagnostic only, nothing is persisted.</summary>
    Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(string csvContent, int windows, CancellationToken cancellationToken);

    /// <summary>Phase 11: Expected Goals (Poisson/Dixon-Coles) for one hypothetical fixture. Diagnostic only, nothing is persisted.</summary>
    Task<PredictGoalsResultDto> PredictGoalsAsync(string csvContent, string homeTeamName, string awayTeamName, double? dixonColesRho, CancellationToken cancellationToken);

    /// <summary>Loads a saved artifact and scores one real match's current features. This is what backs POST /api/predictions/generate.</summary>
    Task<PredictMatch1X2ResultDto> PredictMatch1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, CancellationToken cancellationToken);

    /// <summary>
    /// The "expert analyst" explanation on top of a plain prediction: SHAP feature
    /// attribution, Kelly-Criterion stake sizing (only when <paramref name="odds"/> is
    /// supplied), and a narrative. Backs POST /api/predictions/{matchId}/analyze.
    /// </summary>
    Task<AnalyzeFootball1X2ResultDto> AnalyzeFootball1X2Async(
        string artifactPath,
        IReadOnlyDictionary<string, double> features,
        AnalyzeFixtureContextDto fixture,
        AnalyzeOddsDto? odds,
        CancellationToken cancellationToken);
}
