using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Thin wrapper over the API-Football v3 HTTP surface. Scope: /countries, /leagues
/// (Phase 3); /teams (Phase 4); /fixtures, /standings (Phase 5); /injuries,
/// /fixtures/lineups, and next/last fixture filters for incremental refresh (Phase 6);
/// /odds, /predictions (Phase 7).
/// </summary>
public sealed class ApiFootballClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiFootballClient> _logger;

    public ApiFootballClient(HttpClient httpClient, ILogger<ApiFootballClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiFootballEnvelope<ApiFootballCountryModel>> GetCountriesAsync(CancellationToken cancellationToken) =>
        GetAsync<ApiFootballCountryModel>("countries", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballLeagueEntryModel>> GetLeaguesAsync(CancellationToken cancellationToken) =>
        GetAsync<ApiFootballLeagueEntryModel>("leagues", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballLeagueEntryModel>> GetLeagueByIdAsync(int externalLeagueId, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballLeagueEntryModel>($"leagues?id={externalLeagueId}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballTeamEntryModel>> GetTeamsAsync(int externalLeagueId, int season, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballTeamEntryModel>($"teams?league={externalLeagueId}&season={season}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballFixtureEntryModel>> GetFixturesAsync(int externalLeagueId, int season, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballFixtureEntryModel>($"fixtures?league={externalLeagueId}&season={season}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballStandingsResponseModel>> GetStandingsAsync(int externalLeagueId, int season, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballStandingsResponseModel>($"standings?league={externalLeagueId}&season={season}", cancellationToken);

    /// <summary>Next N fixtures for a league across any season — used for incremental "upcoming" refresh.</summary>
    public Task<ApiFootballEnvelope<ApiFootballFixtureEntryModel>> GetUpcomingFixturesAsync(int externalLeagueId, int count, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballFixtureEntryModel>($"fixtures?league={externalLeagueId}&next={count}", cancellationToken);

    /// <summary>Last N played fixtures for a league — used for incremental "recently finished" refresh.</summary>
    public Task<ApiFootballEnvelope<ApiFootballFixtureEntryModel>> GetRecentFixturesAsync(int externalLeagueId, int count, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballFixtureEntryModel>($"fixtures?league={externalLeagueId}&last={count}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballInjuryEntryModel>> GetInjuriesAsync(int externalLeagueId, int season, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballInjuryEntryModel>($"injuries?league={externalLeagueId}&season={season}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballLineupEntryModel>> GetLineupsAsync(int externalFixtureId, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballLineupEntryModel>($"fixtures/lineups?fixture={externalFixtureId}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballOddsResponseModel>> GetOddsAsync(int externalFixtureId, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballOddsResponseModel>($"odds?fixture={externalFixtureId}", cancellationToken);

    public Task<ApiFootballEnvelope<ApiFootballPredictionResponseModel>> GetPredictionsAsync(int externalFixtureId, CancellationToken cancellationToken) =>
        GetAsync<ApiFootballPredictionResponseModel>($"predictions?fixture={externalFixtureId}", cancellationToken);

    private async Task<ApiFootballEnvelope<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("API-Football request to {Path} failed with status {StatusCode}.", path, (int)response.StatusCode);
            throw new ApiFootballException(
                $"API-Football request to '{path}' failed with status {(int)response.StatusCode}.",
                response.StatusCode);
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiFootballEnvelope<T>>(cancellationToken);
        return envelope ?? new ApiFootballEnvelope<T>();
    }
}
