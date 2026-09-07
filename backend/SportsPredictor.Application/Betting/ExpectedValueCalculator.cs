namespace SportsPredictor.Application.Betting;

public enum ValueCategory
{
    NegativeValue,
    Neutral,
    Value,
    StrongValue,
}

/// <summary>EV = model_probability * decimal_odds - 1 (CLAUDE.md section 25). Never recommend on favorite status alone.</summary>
public static class ExpectedValueCalculator
{
    private const double NeutralBand = 0.02;
    private const double StrongValueThreshold = 0.10;

    public static double Calculate(double modelProbability, decimal decimalOdds) =>
        (modelProbability * (double)decimalOdds) - 1.0;

    public static ValueCategory Categorize(double expectedValue) => expectedValue switch
    {
        > StrongValueThreshold => ValueCategory.StrongValue,
        > NeutralBand => ValueCategory.Value,
        >= -NeutralBand => ValueCategory.Neutral,
        _ => ValueCategory.NegativeValue,
    };
}
