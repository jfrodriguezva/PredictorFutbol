using SportsPredictor.Application.Betting;
using Xunit;

namespace SportsPredictor.Application.Tests.Betting;

public class ExpectedValueCalculatorTests
{
    [Fact]
    public void Calculate_FairOdds_IsZero()
    {
        // model says 50%, market offers exactly break-even decimal odds of 2.0
        var ev = ExpectedValueCalculator.Calculate(0.5, 2.0m);

        Assert.Equal(0.0, ev, 6);
    }

    [Fact]
    public void Calculate_ModelMoreConfidentThanMarket_IsPositive()
    {
        var ev = ExpectedValueCalculator.Calculate(0.6, 2.0m);

        Assert.True(ev > 0);
    }

    [Theory]
    [InlineData(0.15, ValueCategory.StrongValue)]
    [InlineData(0.05, ValueCategory.Value)]
    [InlineData(0.0, ValueCategory.Neutral)]
    [InlineData(-0.2, ValueCategory.NegativeValue)]
    public void Categorize_ReturnsExpectedBand(double ev, ValueCategory expected)
    {
        Assert.Equal(expected, ExpectedValueCalculator.Categorize(ev));
    }

    [Fact]
    public void Categorize_NeverRecommendsSolelyBecauseFavorite()
    {
        // A heavy favorite (90%) at odds that don't cover it is still negative EV.
        var ev = ExpectedValueCalculator.Calculate(0.9, 1.05m);

        Assert.Equal(ValueCategory.NegativeValue, ExpectedValueCalculator.Categorize(ev));
    }
}
