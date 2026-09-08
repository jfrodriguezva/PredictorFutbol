namespace SportsPredictor.Application.Common.Exceptions;

/// <summary>Thrown when a caller exceeds an endpoint's allowed call rate — maps to HTTP 429.</summary>
public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message)
    {
    }
}
