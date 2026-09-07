namespace SportsPredictor.Application.Betting;

/// <summary>American ↔ Decimal ↔ Implied probability conversions (CLAUDE.md section 25).</summary>
public static class OddsConversion
{
    public static decimal AmericanToDecimal(int americanOdds) =>
        americanOdds > 0
            ? 1m + (americanOdds / 100m)
            : 1m + (100m / Math.Abs(americanOdds));

    public static int DecimalToAmerican(decimal decimalOdds)
    {
        if (decimalOdds <= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalOdds), "Decimal odds must be greater than 1.");
        }

        return decimalOdds >= 2m
            ? (int)Math.Round((decimalOdds - 1m) * 100m, MidpointRounding.AwayFromZero)
            : (int)Math.Round(-100m / (decimalOdds - 1m), MidpointRounding.AwayFromZero);
    }

    public static double DecimalToImpliedProbability(decimal decimalOdds)
    {
        if (decimalOdds <= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalOdds), "Decimal odds must be greater than 1.");
        }

        return (double)(1m / decimalOdds);
    }
}
