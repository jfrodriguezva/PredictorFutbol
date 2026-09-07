using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class FootballFixtureSyncServiceTests : IDisposable
{
    private const string SingleLeagueJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "league": { "id": 39, "name": "Premier League", "type": "League", "logo": "" },
              "country": { "name": "England", "code": "GB", "flag": "" },
              "seasons": [
                { "year": 2024, "start": "2024-08-11", "end": "2025-05-25", "current": true }
              ]
            }
          ]
        }
        """;

    private const string FixturesJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            {
              "fixture": { "id": 100001, "date": "2024-08-16T19:00:00+00:00", "status": { "short": "FT" }, "venue": { "id": 556, "name": "Old Trafford", "city": "Manchester" } },
              "teams": { "home": { "id": 33, "name": "Manchester United" }, "away": { "id": 34, "name": "Newcastle" } },
              "goals": { "home": 2, "away": 1 }
            },
            {
              "fixture": { "id": 100002, "date": "2025-01-20T15:00:00+00:00", "status": { "short": "NS" }, "venue": null },
              "teams": { "home": { "id": 40, "name": "Liverpool" }, "away": { "id": 33, "name": "Manchester United" } },
              "goals": { "home": null, "away": null }
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballFixtureSyncServiceTests()
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

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static (FootballReferenceSyncService reference, FootballFixtureSyncService fixtures) CreateServices(
        FakeHttpMessageHandler handler, SportsPredictorDbContext dbContext)
    {
        var optionsMonitor = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions
        {
            BaseUrl = "https://v3.football.api-sports.io",
            ApiKey = "test-key",
        });
        var authHandler = new ApiFootballAuthHandler(optionsMonitor) { InnerHandler = handler };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };
        var apiFootballClient = new ApiFootballClient(httpClient, NullLogger<ApiFootballClient>.Instance);

        return (
            new FootballReferenceSyncService(apiFootballClient, dbContext),
            new FootballFixtureSyncService(apiFootballClient, dbContext));
    }

    private async Task<Guid> CreateTrackedCompetitionAsync()
    {
        var (reference, _) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson)), _dbContext);
        var tracked = await reference.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);
        return tracked.Id;
    }

    [Fact]
    public async Task SyncFixturesAsync_UpsertsMatchesAndAutoCreatesTeamsAndVenue()
    {
        var trackedId = await CreateTrackedCompetitionAsync();
        var (_, fixtures) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson)), _dbContext);

        var result = await fixtures.SyncFixturesAsync(trackedId, CancellationToken.None);

        Assert.Equal(2, result.FixturesUpserted);
        Assert.Equal(3, result.TeamsCreated); // Man Utd, Newcastle, Liverpool — none existed yet
        Assert.Equal(1, result.VenuesCreated);
        Assert.Equal(0, result.SkippedNoSeasonMatch);
        Assert.Equal(2, _dbContext.Matches.Count());
    }

    [Fact]
    public async Task SyncFixturesAsync_MapsFinishedFixtureScoreAndStatus()
    {
        var trackedId = await CreateTrackedCompetitionAsync();
        var (_, fixtures) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson)), _dbContext);

        await fixtures.SyncFixturesAsync(trackedId, CancellationToken.None);

        var finished = _dbContext.Matches.Single(m => m.ExternalApiFootballId == 100001);
        Assert.Equal(MatchStatus.Finished, finished.Status);
        Assert.Equal(2, finished.HomeScore);
        Assert.Equal(1, finished.AwayScore);

        var scheduled = _dbContext.Matches.Single(m => m.ExternalApiFootballId == 100002);
        Assert.Equal(MatchStatus.Scheduled, scheduled.Status);
        Assert.Null(scheduled.HomeScore);
        Assert.Null(scheduled.VenueId);
    }

    [Fact]
    public async Task SyncFixturesAsync_CalledTwice_DoesNotDuplicateMatchesOrTeams()
    {
        var trackedId = await CreateTrackedCompetitionAsync();

        var (_, firstSync) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson)), _dbContext);
        await firstSync.SyncFixturesAsync(trackedId, CancellationToken.None);

        var (_, secondSync) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson)), _dbContext);
        var result = await secondSync.SyncFixturesAsync(trackedId, CancellationToken.None);

        Assert.Equal(2, result.FixturesUpserted);
        Assert.Equal(0, result.TeamsCreated); // all three already exist
        Assert.Equal(0, result.VenuesCreated);
        Assert.Equal(2, _dbContext.Matches.Count());
        Assert.Equal(3, _dbContext.Teams.Count());
    }

    [Fact]
    public async Task SyncFixturesAsync_UnknownTrackedCompetition_ThrowsNotFound()
    {
        var (_, fixtures) = CreateServices(FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson)), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => fixtures.SyncFixturesAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshRecentAndUpcomingFixturesAsync_CallsNextAndLast_AndUpsertsBoth()
    {
        var trackedId = await CreateTrackedCompetitionAsync();

        // First response answers the "next" (upcoming) call, second answers "last" (recent).
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson), JsonResponse(FixturesJson));
        var (_, fixtures) = CreateServices(handler, _dbContext);

        var result = await fixtures.RefreshRecentAndUpcomingFixturesAsync(trackedId, upcomingCount: 5, recentCount: 5, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("next=5", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("last=5", handler.Requests[1].RequestUri!.Query);
        // Same two fixtures appear in both responses in this test, so the second
        // batch is pure upsert (no new rows) — still processed, not double-counted oddly.
        Assert.Equal(4, result.FixturesUpserted);
        Assert.Equal(2, _dbContext.Matches.Count());
    }

    [Fact]
    public async Task RefreshRecentAndUpcomingFixturesAsync_ZeroCounts_SkipsThatCall()
    {
        var trackedId = await CreateTrackedCompetitionAsync();
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(FixturesJson));
        var (_, fixtures) = CreateServices(handler, _dbContext);

        await fixtures.RefreshRecentAndUpcomingFixturesAsync(trackedId, upcomingCount: 5, recentCount: 0, CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Contains("next=5", handler.Requests[0].RequestUri!.Query);
    }
}
