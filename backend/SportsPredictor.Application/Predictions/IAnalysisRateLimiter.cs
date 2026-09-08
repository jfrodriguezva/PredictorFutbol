namespace SportsPredictor.Application.Predictions;

/// <summary>
/// Caps how often POST /api/predictions/{matchId}/analyze can be called for the same
/// match, independent of the narrative cache — protects against runaway Claude API
/// cost from rapid repeated calls (e.g. accidental double-clicks or a scripted client),
/// even when each call has slightly different odds and so isn't a cache hit.
/// </summary>
public interface IAnalysisRateLimiter
{
    /// <summary>Throws <see cref="Common.Exceptions.RateLimitExceededException"/> if this match is over its call budget for the current window; otherwise records the call.</summary>
    void Check(Guid matchId);
}
