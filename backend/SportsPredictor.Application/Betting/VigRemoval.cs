namespace SportsPredictor.Application.Betting;

/// <summary>
/// Removes the bookmaker's overround (vig) from a mutually exclusive market's raw
/// implied probabilities, so they sum to exactly 1 (CLAUDE.md section 25).
/// </summary>
public static class VigRemoval
{
    public static IReadOnlyList<double> RemoveVig(IReadOnlyList<double> rawImpliedProbabilities)
    {
        var sum = rawImpliedProbabilities.Sum();
        if (sum <= 0)
        {
            throw new ArgumentException("Implied probabilities must sum to a positive value.", nameof(rawImpliedProbabilities));
        }

        return rawImpliedProbabilities.Select(p => p / sum).ToList();
    }
}
