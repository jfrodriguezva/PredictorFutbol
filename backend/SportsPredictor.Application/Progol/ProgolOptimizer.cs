namespace SportsPredictor.Application.Progol;

/// <summary>
/// Heuristic optimizer (CLAUDE.md section 30) — NOT a full integer-programming solver.
/// Ranks matches by how uncertain the model is about them (smallest margin between the
/// top two outcomes) and spends doubles/triples there first, since that is where extra
/// coverage buys the most real chance of a full hit. Documented simplification: the
/// "estimated full-hit probability" multiplies per-match hit probabilities assuming
/// independence between matches, which is not strictly true in reality but is the
/// standard simplification for this kind of pool-betting estimate.
/// </summary>
public sealed class ProgolOptimizer : IProgolOptimizer
{
    public ProgolOptimizationResult Optimize(IReadOnlyList<ProgolMatchInput> matches, int budget, int maxDoubles, int maxTriples)
    {
        if (matches.Count == 0)
        {
            throw new ArgumentException("At least one match is required.", nameof(matches));
        }

        if (budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Budget must allow at least 1 combination.");
        }

        var scenarios = new[]
        {
            BuildScenario("Safe", matches, doublesRequested: 0, triplesRequested: 0, budget, contrarian: false),
            BuildScenario("Balanced", matches, doublesRequested: maxDoubles, triplesRequested: maxTriples, budget, contrarian: false),
            BuildScenario("Aggressive", matches, doublesRequested: maxDoubles + 2, triplesRequested: maxTriples + 1, budget, contrarian: false),
            BuildScenario("Contrarian", matches, doublesRequested: maxDoubles, triplesRequested: maxTriples, budget, contrarian: true),
        };

        return new ProgolOptimizationResult(scenarios);
    }

    private static ProgolScenario BuildScenario(
        string name,
        IReadOnlyList<ProgolMatchInput> matches,
        int doublesRequested,
        int triplesRequested,
        int budget,
        bool contrarian)
    {
        var triples = Math.Clamp(triplesRequested, 0, matches.Count);
        var doubles = Math.Clamp(doublesRequested, 0, matches.Count - triples);
        (triples, doubles) = CapToBudget(triples, doubles, budget);

        var rankedByUncertainty = matches
            .Select(m => (Match: m, Margin: TopTwoMargin(m)))
            .OrderBy(x => x.Margin)
            .Select(x => x.Match.MatchLabel)
            .ToList();

        var tripleLabels = rankedByUncertainty.Take(triples).ToHashSet();
        var doubleLabels = rankedByUncertainty.Skip(triples).Take(doubles).ToHashSet();

        var picks = new List<ProgolMatchPick>();
        foreach (var match in matches)
        {
            var ordered = OrderSelectionsByPreference(match, contrarian);
            var count = tripleLabels.Contains(match.MatchLabel) ? 3 : doubleLabels.Contains(match.MatchLabel) ? 2 : 1;
            var chosen = ordered.Take(count).ToList();

            picks.Add(new ProgolMatchPick(
                match.MatchLabel,
                chosen.Select(c => c.Label).ToList(),
                chosen.Sum(c => c.Probability)));
        }

        var totalCombinations = picks.Aggregate(1, (acc, p) => acc * p.Selections.Count);
        var estimatedFullHit = picks.Aggregate(1.0, (acc, p) => acc * p.HitProbability);

        return new ProgolScenario(name, picks, totalCombinations, estimatedFullHit);
    }

    /// <summary>Reduces (triples, doubles) greedily — dropping a triple before a double — until 2^doubles * 3^triples fits the budget.</summary>
    private static (int Triples, int Doubles) CapToBudget(int triples, int doubles, int budget)
    {
        while ((triples > 0 || doubles > 0) && Math.Pow(2, doubles) * Math.Pow(3, triples) > budget)
        {
            if (triples > 0)
            {
                triples--;
            }
            else
            {
                doubles--;
            }
        }

        return (triples, doubles);
    }

    private static double TopTwoMargin(ProgolMatchInput match)
    {
        var sorted = new[] { match.HomeProbability, match.DrawProbability, match.AwayProbability }
            .OrderByDescending(p => p)
            .ToArray();
        return sorted[0] - sorted[1];
    }

    private static List<(string Label, double Probability)> OrderSelectionsByPreference(ProgolMatchInput match, bool contrarian)
    {
        var options = new List<(string Label, double Probability)>
        {
            ("L", match.HomeProbability),
            ("E", match.DrawProbability),
            ("V", match.AwayProbability),
        };

        // Contrarian starts from the least-favored outcome — deliberately seeking upsets,
        // never just following the crowd (CLAUDE.md section 25's "no recomendar solo por ser favorito" spirit).
        return contrarian
            ? options.OrderBy(o => o.Probability).ToList()
            : options.OrderByDescending(o => o.Probability).ToList();
    }
}
