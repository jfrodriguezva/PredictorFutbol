namespace SportsPredictor.Application.Predictions;

public sealed record ShapFeatureDto(string Feature, double Impact);

public sealed record StakeDto(
    string Label,
    double DecimalOdds,
    double ImpliedProbability,
    double ModelProbability,
    double Edge,
    bool IsValueBet,
    double KellyFractionFull,
    double SuggestedStakePctBankroll);

public sealed record PredictionExplanationDto(
    Guid Id,
    Guid MatchId,
    Guid ModelVersionId,
    DateTime GeneratedAt,
    double Home,
    double Draw,
    double Away,
    IReadOnlyList<ShapFeatureDto> ShapTopFeatures,
    IReadOnlyDictionary<string, StakeDto>? Stakes,
    string Narrative);
