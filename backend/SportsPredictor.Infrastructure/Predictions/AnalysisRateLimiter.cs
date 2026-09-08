using Microsoft.Extensions.Caching.Memory;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Predictions;

namespace SportsPredictor.Infrastructure.Predictions;

/// <summary>
/// Fixed-window rate limiter backed directly by <see cref="IMemoryCache"/> — not
/// <c>ICacheService</c>, whose single <c>GetOrCreateAsync</c> method is cache-or-compute
/// and can't express a counter that increments on every call within a window.
/// </summary>
public sealed class AnalysisRateLimiter : IAnalysisRateLimiter
{
    private const int MaxCallsPerWindow = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public AnalysisRateLimiter(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Check(Guid matchId)
    {
        var key = $"analyze-rate-limit:{matchId}";
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;

        var current = Interlocked.Increment(ref counter.Value);
        if (current > MaxCallsPerWindow)
        {
            throw new RateLimitExceededException(
                $"Too many analysis requests for this match — limit is {MaxCallsPerWindow} per {Window.TotalMinutes:0} minutes. Try again later.");
        }
    }

    private sealed class Counter
    {
        public int Value;
    }
}
