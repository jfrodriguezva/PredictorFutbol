namespace SportsPredictor.Application.Predictions;

public sealed record PredictionDto(
    Guid Id,
    Guid MatchId,
    Guid ModelVersionId,
    DateTime PredictionDate,
    string Market,
    string Selection,
    double Probability,
    double? ExpectedValue,
    string? ValueCategory,
    bool Recommended,
    string? ActualOutcome,
    bool? IsCorrect);

public sealed record GeneratePredictionResultDto(
    Guid MatchId,
    Guid ModelVersionId,
    DateTime PredictionDate,
    IReadOnlyList<PredictionDto> Selections);

/// <summary>
/// The result of recording what actually happened in a finished match. Only ever
/// sets ActualOutcome/IsCorrect — never touches Probability/ExpectedValue/Selection
/// (CLAUDE.md section 29: a prediction's forecast is never rewritten after the fact).
/// </summary>
public sealed record EvaluateMatchResultDto(Guid MatchId, string ActualOutcome, IReadOnlyList<PredictionDto> Predictions);

/// <summary>A missed call: a Recommended selection that turned out wrong — the most actionable thing to review.</summary>
public sealed record MissedPredictionDto(Guid MatchId, string Market, string Selection, double Probability, string ActualOutcome, DateTime PredictionDate);

public sealed record AccuracySummaryDto(
    int TotalEvaluated,
    int CorrectCount,
    double Accuracy,
    int RecommendedTotal,
    int RecommendedCorrect,
    double RecommendedAccuracy,
    IReadOnlyList<MissedPredictionDto> RecentMisses);
