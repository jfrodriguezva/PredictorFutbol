using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class FootballReferenceSyncServiceTests : IDisposable
{
    private const string SingleLeagueJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "league": { "id": 39, "name": "Premier League", "type": "League", "logo": "https://media.api-sports.io/football/leagues/39.png" },
              "country": { "name": "England", "code": "GB", "flag": "https://media.api-sports.io/flags/gb.svg" },
              "seasons": [
                { "year": 2024, "start": "2024-08-11", "end": "2025-05-25", "current": true }
              ]
            }
          ]
        }
        """;

    private const string TeamsJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            {
              "team": { "id": 33, "name": "Manchester United", "country": "England" },
              "venue": { "id": 556, "name": "Old Trafford", "city": "Manchester" }
            },
            {
              "team": { "id": 40, "name": "Liverpool", "country": "England" },
              "venue": { "id": 550, "name": "Anfield", "city": "Liverpool" }
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballReferenceSyncServiceTests()
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

    private static FootballReferenceSyncService CreateService(FakeHttpMessageHandler handler, SportsPredictorDbContext dbContext, string? apiKey = "test-key")
    {
        var optionsMonitor = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions
        {
            BaseUrl = "https://v3.football.api-sports.io",
            ApiKey = apiKey,
        });
        var authHandler = new ApiFootballAuthHandler(optionsMonitor) { InnerHandler = handler };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };
        var apiFootballClient = new ApiFootballClient(httpClient, NullLogger<ApiFootballClient>.Instance);

        return new FootballReferenceSyncService(apiFootballClient, dbContext);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task CreateTrackedCompetitionAsync_UnknownLeague_CreatesCompetitionSeasonAndTrackedRow()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson));
        var service = CreateService(handler, _dbContext);

        var dto = await service.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        Assert.Equal(39, dto.ExternalLeagueId);
        Assert.Equal("Premier League", dto.CompetitionName);
        Assert.True(dto.Enabled);

        var competition = Assert.Single(_dbContext.Competitions);
        Assert.Equal(39, competition.ExternalApiFootballId);

        var season = Assert.Single(_dbContext.Seasons);
        Assert.Equal("2024/2025", season.Name);
        Assert.Equal(new DateOnly(2024, 8, 11), season.StartDate);
    }

    [Fact]
    public async Task CreateTrackedCompetitionAsync_CalledTwiceForSameLeagueAndSeason_IsIdempotent()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson), JsonResponse(SingleLeagueJson));
        var service = CreateService(handler, _dbContext);
        var request = new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024);

        var first = await service.CreateTrackedCompetitionAsync(request, CancellationToken.None);
        var second = await service.CreateTrackedCompetitionAsync(request, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_dbContext.Competitions);
        Assert.Single(_dbContext.TrackedCompetitions);
    }

    [Fact]
    public async Task SyncTeamsAsync_UpsertsTeamsAndVenues()
    {
        var leagueHandler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson));
        var setupService = CreateService(leagueHandler, _dbContext);
        var tracked = await setupService.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        var teamsHandler = FakeHttpMessageHandler.Sequence(JsonResponse(TeamsJson));
        var syncService = CreateService(teamsHandler, _dbContext);

        var result = await syncService.SyncTeamsAsync(tracked.Id, CancellationToken.None);

        Assert.Equal(2, result.TeamsUpserted);
        Assert.Equal(2, result.VenuesUpserted);
        Assert.Equal(2, _dbContext.Teams.Count());
        Assert.Equal(2, _dbContext.Venues.Count());
        Assert.Contains(_dbContext.Teams, t => t.ExternalApiFootballId == 33 && t.Name == "Manchester United");
        Assert.Contains(_dbContext.Venues, v => v.ExternalApiFootballId == 556 && v.Name == "Old Trafford");
    }

    [Fact]
    public async Task SyncTeamsAsync_CalledTwice_DoesNotDuplicateTeams()
    {
        var leagueHandler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson));
        var setupService = CreateService(leagueHandler, _dbContext);
        var tracked = await setupService.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        var firstSync = CreateService(FakeHttpMessageHandler.Sequence(JsonResponse(TeamsJson)), _dbContext);
        await firstSync.SyncTeamsAsync(tracked.Id, CancellationToken.None);

        var secondSync = CreateService(FakeHttpMessageHandler.Sequence(JsonResponse(TeamsJson)), _dbContext);
        await secondSync.SyncTeamsAsync(tracked.Id, CancellationToken.None);

        Assert.Equal(2, _dbContext.Teams.Count());
        Assert.Equal(2, _dbContext.Venues.Count());
    }

    [Fact]
    public async Task SyncTeamsAsync_TwoTeamsShareTheSameVenueInOneBatch_DoesNotThrow()
    {
        // Real-world case that surfaced against live Liga MX data: two clubs can share
        // one stadium. Before the fix, the second team's venue lookup only queried the
        // database (missed the first team's not-yet-saved venue) and tried to insert a
        // duplicate, violating Venues.ExternalApiFootballId's unique constraint.
        const string sharedVenueTeamsJson = """
            {
              "results": 2,
              "errors": [],
              "response": [
                { "team": { "id": 101, "name": "Club A", "country": "Mexico" }, "venue": { "id": 900, "name": "Shared Stadium", "city": "CDMX" } },
                { "team": { "id": 102, "name": "Club B", "country": "Mexico" }, "venue": { "id": 900, "name": "Shared Stadium", "city": "CDMX" } }
              ]
            }
            """;

        var leagueHandler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson));
        var setupService = CreateService(leagueHandler, _dbContext);
        var tracked = await setupService.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        var syncService = CreateService(FakeHttpMessageHandler.Sequence(JsonResponse(sharedVenueTeamsJson)), _dbContext);

        var result = await syncService.SyncTeamsAsync(tracked.Id, CancellationToken.None);

        Assert.Equal(2, result.TeamsUpserted);
        Assert.Equal(2, result.VenuesUpserted); // both teams report the venue, but...
        Assert.Single(_dbContext.Venues); // ...only one row actually gets persisted
    }

    [Fact]
    public async Task SyncTeamsAsync_UnknownTrackedCompetition_ThrowsNotFound()
    {
        var service = CreateService(FakeHttpMessageHandler.Sequence(JsonResponse(TeamsJson)), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SyncTeamsAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesFlagAndPersists()
    {
        var leagueHandler = FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson));
        var setupService = CreateService(leagueHandler, _dbContext);
        var tracked = await setupService.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        var updated = await setupService.SetEnabledAsync(tracked.Id, false, CancellationToken.None);

        Assert.False(updated.Enabled);
        Assert.False(_dbContext.TrackedCompetitions.Single().Enabled);
    }
}
