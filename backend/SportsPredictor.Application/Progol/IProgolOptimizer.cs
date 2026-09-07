namespace SportsPredictor.Application.Progol;

/// <summary>
/// CLAUDE.md section 30. Given L/E/V probabilities for the pool's matches, a budget
/// (max number of combinations affordable), and how many doubles/triples are allowed,
/// produces four scenarios: Safe, Balanced, Aggressive, Contrarian.
/// </summary>
public interface IProgolOptimizer
{
    ProgolOptimizationResult Optimize(IReadOnlyList<ProgolMatchInput> matches, int budget, int maxDoubles, int maxTriples);
}
