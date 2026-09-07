using SportsPredictor.Application.Progol;
using Xunit;

namespace SportsPredictor.Application.Tests.Progol;

public class ProgolOptimizerTests
{
    private static List<ProgolMatchInput> FourteenMatches()
    {
        var random = new Random(42);
        var matches = new List<ProgolMatchInput>();
        for (var i = 1; i <= 14; i++)
        {
            var home = 0.2 + random.NextDouble() * 0.5;
            var draw = 0.15 + random.NextDouble() * 0.2;
            var away = Math.Max(0.05, 1.0 - home - draw);
            matches.Add(new ProgolMatchInput($"Match{i}", home, draw, away));
        }
        return matches;
    }

    [Fact]
    public void Optimize_ReturnsFourScenarios()
    {
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(FourteenMatches(), budget: 27, maxDoubles: 3, maxTriples: 1);

        Assert.Equal(4, result.Scenarios.Count);
        Assert.Equal(new[] { "Safe", "Balanced", "Aggressive", "Contrarian" }, result.Scenarios.Select(s => s.Name));
    }

    [Fact]
    public void Optimize_SafeScenario_IsAllSinglesWithBudgetOfOne()
    {
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(FourteenMatches(), budget: 27, maxDoubles: 3, maxTriples: 1);

        var safe = result.Scenarios.Single(s => s.Name == "Safe");
        Assert.All(safe.Picks, p => Assert.Single(p.Selections));
        Assert.Equal(1, safe.TotalCombinations);
    }

    [Fact]
    public void Optimize_NeverExceedsBudget()
    {
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(FourteenMatches(), budget: 8, maxDoubles: 5, maxTriples: 3);

        Assert.All(result.Scenarios, s => Assert.True(s.TotalCombinations <= 8));
    }

    [Fact]
    public void Optimize_BalancedScenario_SpendsDoublesOnMostUncertainMatches()
    {
        var matches = new List<ProgolMatchInput>
        {
            new("Certain", 0.9, 0.05, 0.05), // huge margin, should stay a single
            new("Toss-up", 0.34, 0.33, 0.33), // tiny margin, should get the double
        };
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(matches, budget: 4, maxDoubles: 1, maxTriples: 0);

        var balanced = result.Scenarios.Single(s => s.Name == "Balanced");
        var certainPick = balanced.Picks.Single(p => p.MatchLabel == "Certain");
        var tossUpPick = balanced.Picks.Single(p => p.MatchLabel == "Toss-up");
        Assert.Single(certainPick.Selections);
        Assert.Equal(2, tossUpPick.Selections.Count);
    }

    [Fact]
    public void Optimize_ContrarianScenario_PicksLeastLikelyOutcome()
    {
        var matches = new List<ProgolMatchInput> { new("M1", 0.7, 0.2, 0.1) };
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(matches, budget: 1, maxDoubles: 0, maxTriples: 0);

        var contrarian = result.Scenarios.Single(s => s.Name == "Contrarian");
        Assert.Equal("V", contrarian.Picks.Single().Selections.Single());
    }

    [Fact]
    public void Optimize_EstimatedFullHitProbability_IsProductOfPerMatchHitProbabilities()
    {
        var matches = new List<ProgolMatchInput>
        {
            new("M1", 0.5, 0.3, 0.2),
            new("M2", 0.6, 0.25, 0.15),
        };
        var optimizer = new ProgolOptimizer();

        var result = optimizer.Optimize(matches, budget: 1, maxDoubles: 0, maxTriples: 0);

        var safe = result.Scenarios.Single(s => s.Name == "Safe");
        Assert.Equal(0.5 * 0.6, safe.EstimatedFullHitProbability, 6);
    }

    [Fact]
    public void Optimize_EmptyMatchList_Throws()
    {
        var optimizer = new ProgolOptimizer();

        Assert.Throws<ArgumentException>(() => optimizer.Optimize([], budget: 1, maxDoubles: 0, maxTriples: 0));
    }

    [Fact]
    public void Optimize_ZeroBudget_Throws()
    {
        var optimizer = new ProgolOptimizer();

        Assert.Throws<ArgumentOutOfRangeException>(() => optimizer.Optimize(FourteenMatches(), budget: 0, maxDoubles: 0, maxTriples: 0));
    }
}
