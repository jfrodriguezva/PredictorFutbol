using SportsPredictor.Application.Betting;
using Xunit;

namespace SportsPredictor.Application.Tests.Betting;

public class VigRemovalTests
{
    [Fact]
    public void RemoveVig_NormalizesToSumOfOne()
    {
        var raw = new List<double> { 0.45, 0.30, 0.30 }; // sums to 1.05 (5% vig)

        var result = VigRemoval.RemoveVig(raw);

        Assert.Equal(1.0, result.Sum(), 6);
    }

    [Fact]
    public void RemoveVig_PreservesRelativeProportions()
    {
        var raw = new List<double> { 0.6, 0.3, 0.3 };

        var result = VigRemoval.RemoveVig(raw);

        Assert.Equal(2.0, result[0] / result[1], 4);
    }
}
