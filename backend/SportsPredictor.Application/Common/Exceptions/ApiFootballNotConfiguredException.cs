namespace SportsPredictor.Application.Common.Exceptions;

/// <summary>Thrown when an API-Football call is attempted without API_FOOTBALL_KEY configured.</summary>
public sealed class ApiFootballNotConfiguredException : ApiFootballException
{
    public ApiFootballNotConfiguredException()
        : base("API-Football is not configured. Set the API_FOOTBALL_KEY environment variable.")
    {
    }
}
