namespace SportsPredictor.Application.MlService;

/// <summary>Expected Goals (CLAUDE.md section 21) for one hypothetical fixture. Diagnostic only, nothing persisted.</summary>
public sealed record PredictGoalsResultDto(
    double HomeExpectedGoals,
    double AwayExpectedGoals,
    double Over25,
    double Under25,
    double BttsYes,
    double BttsNo,
    int MostLikelyHomeGoals,
    int MostLikelyAwayGoals,
    double MostLikelyScoreProbability);
