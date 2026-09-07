using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Infrastructure.Caching;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class ApiFootballSyncServiceTests : IDisposable
{
    private const string CountriesJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            { "name": "England", "code": "GB", "flag": "https://media.api-sports.io/flags/gb.svg" },
            { "name": "Spain", "code": "ES", "flag": "https://media.api-sports.io/flags/es.svg" }
          ]
        }
        """;

    private const string LeaguesJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "league": { "id": 39, "name": "Premier League", "type": "League", "logo": "https://media.api-sports.io/football/leagues/39.png" },
              "country": { "name": "England", "code": "GB", "flag": "https://media.api-sports.io/flags/gb.svg" },
              "seasons": [
                { "year": 2023, "current": false },
                { "year": 2024, "current": true }
              ]
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public ApiFootballSyncServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SportsPredictorDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SportsPredictorDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static ApiFootballSyncService CreateService(FakeHttpMessageHandler handler, SportsPredictorDbContext dbContext, string? apiKey = "test-key")
    {
        var optionsMonitor = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions
        {
            BaseUrl = "https://v3.football.api-sports.io",
            ApiKey = apiKey,
        });

        // Chain through the real auth handler so tests exercise the same
        // "missing key fails fast" behavior production wiring produces.
        var authHandler = new ApiFootballAuthHandler(optionsMonitor) { InnerHandler = handler };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };
        var apiFootballClient = new ApiFootballClient(httpClient, NullLogger<ApiFootballClient>.Instance);
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

        return new ApiFootballSyncService(apiFootballClient, cache, dbContext, optionsMonitor);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task GetCountriesAsync_MapsResponseToDtos()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(CountriesJson));
        var service = CreateService(handler, _dbContext);

        var countries = await service.GetCountriesAsync(CancellationToken.None);

        Assert.Equal(2, countries.Count);
        Assert.Contains(countries, c => c.Name == "England" && c.Code == "GB");
    }

    [Fact]
    public async Task GetCountriesAsync_SecondCall_UsesCacheAndDoesNotHitHttpAgain()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(CountriesJson));
        var service = CreateService(handler, _dbContext);

        await service.GetCountriesAsync(CancellationToken.None);
        await service.GetCountriesAsync(CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetLeaguesAsync_MapsCurrentSeasonYear()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(LeaguesJson));
        var service = CreateService(handler, _dbContext);

        var leagues = await service.GetLeaguesAsync(CancellationToken.None);

        var league = Assert.Single(leagues);
        Assert.Equal(39, league.ExternalLeagueId);
        Assert.Equal("Premier League", league.Name);
        Assert.Equal(2024, league.CurrentSeasonYear);
    }

    [Fact]
    public async Task GetStatusAsync_NoApiKey_ReportsDisconnected()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(CountriesJson));
        var service = CreateService(handler, _dbContext, apiKey: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Connected);
    }

    [Fact]
    public async Task TestConnectionAsync_NoApiKey_ThrowsNotConfigured()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(CountriesJson));
        var service = CreateService(handler, _dbContext, apiKey: null);

        await Assert.ThrowsAsync<ApiFootballNotConfiguredException>(() => service.TestConnectionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsCountriesReturnedCount()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(CountriesJson));
        var service = CreateService(handler, _dbContext);

        var result = await service.TestConnectionAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.CountriesReturned);
    }
}
