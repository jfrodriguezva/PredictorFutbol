namespace SportsPredictor.Application.Progol;

/// <summary>One of the pool's matches with model L/E/V (Home/Draw/Away) probabilities.</summary>
public sealed record ProgolMatchInput(string MatchLabel, double HomeProbability, double DrawProbability, double AwayProbability);

/// <summary>What was picked for one match: 1 selection (single), 2 (double) or 3 (triple).</summary>
public sealed record ProgolMatchPick(string MatchLabel, IReadOnlyList<string> Selections, double HitProbability);

public sealed record ProgolScenario(
    string Name,
    IReadOnlyList<ProgolMatchPick> Picks,
    int TotalCombinations,
    double EstimatedFullHitProbability);

public sealed record ProgolOptimizationResult(IReadOnlyList<ProgolScenario> Scenarios);
