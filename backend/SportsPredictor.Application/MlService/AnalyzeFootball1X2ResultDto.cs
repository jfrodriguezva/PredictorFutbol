namespace SportsPredictor.Application.MlService;

public sealed record ShapFeatureImpactDto(string Feature, double Impact);

public sealed record StakeRecommendationDto(
    string Label,
    double DecimalOdds,
    double ImpliedProbability,
    double ModelProbability,
    double Edge,
    bool IsValueBet,
    double KellyFractionFull,
    double SuggestedStakePctBankroll);

/// <summary>Match context the ML service needs for the narrative — it never looks this up itself.</summary>
public sealed record AnalyzeFixtureContextDto(string HomeTeam, string AwayTeam, string LeagueName, string Country, string Season, string Status);

public sealed record AnalyzeOddsDto(double Home, double Draw, double Away);

public sealed record AnalyzeFootball1X2ResultDto(
    double Home,
    double Draw,
    double Away,
    IReadOnlyList<ShapFeatureImpactDto> ShapTopFeatures,
    IReadOnlyDictionary<string, StakeRecommendationDto>? Stakes,
    string Narrative);
