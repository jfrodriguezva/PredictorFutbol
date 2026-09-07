namespace SportsPredictor.Application.ExternalData;

/// <summary>
/// Application-facing, provider-agnostic contract for the football reference data this
/// phase integrates. Implemented by SportsPredictor.Infrastructure's API-Football client.
/// </summary>
public interface IApiFootballClient
{
    Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken cancellationToken);

    /// <summary>Reports whether a key is configured and the last quota snapshot recorded, without calling the provider.</summary>
    Task<ApiFootballStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Performs a real call (GET /countries) to verify connectivity and refresh the quota snapshot.</summary>
    Task<ApiFootballConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);
}
