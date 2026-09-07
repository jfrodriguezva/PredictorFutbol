using SportsPredictor.Application.Betting;
using Xunit;

namespace SportsPredictor.Application.Tests.Betting;

public class OddsConversionTests
{
    [Theory]
    [InlineData(150, 2.5)]
    [InlineData(100, 2.0)]
    [InlineData(-150, 1.6666666666666667)]
    [InlineData(-200, 1.5)]
    public void AmericanToDecimal_ConvertsCorrectly(int american, double expectedDecimal)
    {
        var result = OddsConversion.AmericanToDecimal(american);

        Assert.Equal((decimal)expectedDecimal, result, 4);
    }

    [Theory]
    [InlineData(2.5, 150)]
    [InlineData(2.0, 100)]
    [InlineData(1.5, -200)]
    public void DecimalToAmerican_ConvertsCorrectly(decimal decimalOdds, int expectedAmerican)
    {
        var result = OddsConversion.DecimalToAmerican(decimalOdds);

        Assert.Equal(expectedAmerican, result);
    }

    [Fact]
    public void DecimalToImpliedProbability_TwoPointZero_IsOneHalf()
    {
        var result = OddsConversion.DecimalToImpliedProbability(2.0m);

        Assert.Equal(0.5, result, 4);
    }

    [Fact]
    public void DecimalToImpliedProbability_AtOrBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OddsConversion.DecimalToImpliedProbability(1.0m));
    }
}
