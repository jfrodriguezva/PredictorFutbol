using Microsoft.Extensions.Caching.Memory;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Infrastructure.Predictions;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Predictions;

public class AnalysisRateLimiterTests
{
    [Fact]
    public void Check_AllowsUpToFiveCallsPerMatchWithinWindow()
    {
        var limiter = new AnalysisRateLimiter(new MemoryCache(new MemoryCacheOptions()));
        var matchId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            limiter.Check(matchId);
        }
    }

    [Fact]
    public void Check_SixthCallWithinWindow_Throws()
    {
        var limiter = new AnalysisRateLimiter(new MemoryCache(new MemoryCacheOptions()));
        var matchId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            limiter.Check(matchId);
        }

        Assert.Throws<RateLimitExceededException>(() => limiter.Check(matchId));
    }

    [Fact]
    public void Check_DifferentMatches_TrackedIndependently()
    {
        var limiter = new AnalysisRateLimiter(new MemoryCache(new MemoryCacheOptions()));

        for (var i = 0; i < 5; i++)
        {
            limiter.Check(Guid.NewGuid());
        }

        // A brand-new match should still have its own fresh budget.
        limiter.Check(Guid.NewGuid());
    }
}
