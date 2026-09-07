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

public class FootballStandingsSyncServiceTests : IDisposable
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

    private const string TeamsJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            { "team": { "id": 33, "name": "Manchester United", "country": "England" }, "venue": { "id": 556, "name": "Old Trafford", "city": "Manchester" } },
            { "team": { "id": 34, "name": "Newcastle", "country": "England" }, "venue": { "id": 557, "name": "St James Park", "city": "Newcastle" } }
          ]
        }
        """;

    private const string StandingsJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "league": {
                "standings": [
                  [
                    {
                      "rank": 1,
                      "team": { "id": 33, "name": "Manchester United" },
                      "points": 45,
                      "goalsDiff": 20,
                      "form": "WWDWL",
                      "all": { "played": 20, "win": 14, "draw": 3, "lose": 3, "goals": { "for": 40, "against": 20 } }
                    },
                    {
                      "rank": 2,
                      "team": { "id": 34, "name": "Newcastle" },
                      "points": 40,
                      "goalsDiff": 10,
                      "form": "WDLWW",
                      "all": { "played": 20, "win": 12, "draw": 4, "lose": 4, "goals": { "for": 35, "against": 25 } }
                    },
                    {
                      "rank": 3,
                      "team": { "id": 999, "name": "Unknown FC" },
                      "points": 10,
                      "goalsDiff": -15,
                      "form": null,
                      "all": { "played": 20, "win": 2, "draw": 4, "lose": 14, "goals": { "for": 15, "against": 30 } }
                    }
                  ]
                ]
              }
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballStandingsSyncServiceTests()
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

    private static ApiFootballClient CreateClient(FakeHttpMessageHandler handler)
    {
        var optionsMonitor = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions
        {
            BaseUrl = "https://v3.football.api-sports.io",
            ApiKey = "test-key",
        });
        var authHandler = new ApiFootballAuthHandler(optionsMonitor) { InnerHandler = handler };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };
        return new ApiFootballClient(httpClient, NullLogger<ApiFootballClient>.Instance);
    }

    private async Task<Guid> CreateTrackedCompetitionWithTeamsAsync()
    {
        var reference = new FootballReferenceSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson))), _dbContext);
        var tracked = await reference.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);

        var reference2 = new FootballReferenceSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(TeamsJson))), _dbContext);
        await reference2.SyncTeamsAsync(tracked.Id, CancellationToken.None);

        return tracked.Id;
    }

    [Fact]
    public async Task SyncStandingsAsync_CreatesOneSnapshotPerKnownTeam_SkipsUnknownTeam()
    {
        var trackedId = await CreateTrackedCompetitionWithTeamsAsync();
        var service = new FootballStandingsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(StandingsJson))), _dbContext);

        var result = await service.SyncStandingsAsync(trackedId, CancellationToken.None);

        Assert.Equal(2, result.SnapshotsCreated); // Man Utd + Newcastle
        Assert.Equal(1, result.SkippedUnknownTeam); // "Unknown FC" was never synced via sync-teams
        Assert.Equal(2, _dbContext.StandingSnapshots.Count());
    }

    [Fact]
    public async Task SyncStandingsAsync_MapsRankPointsAndForm()
    {
        var trackedId = await CreateTrackedCompetitionWithTeamsAsync();
        var service = new FootballStandingsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(StandingsJson))), _dbContext);

        await service.SyncStandingsAsync(trackedId, CancellationToken.None);

        var manUtdTeamId = _dbContext.Teams.Single(t => t.ExternalApiFootballId == 33).Id;
        var snapshot = _dbContext.StandingSnapshots.Single(s => s.TeamId == manUtdTeamId);
        Assert.Equal(1, snapshot.Rank);
        Assert.Equal(45, snapshot.Points);
        Assert.Equal(20, snapshot.GoalDifference);
        Assert.Equal("WWDWL", snapshot.Form);
        Assert.Equal(20, snapshot.Played);
    }

    [Fact]
    public async Task SyncStandingsAsync_CalledTwice_AppendsNewSnapshotsInsteadOfOverwriting()
    {
        var trackedId = await CreateTrackedCompetitionWithTeamsAsync();

        var firstSync = new FootballStandingsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(StandingsJson))), _dbContext);
        await firstSync.SyncStandingsAsync(trackedId, CancellationToken.None);

        var secondSync = new FootballStandingsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(StandingsJson))), _dbContext);
        await secondSync.SyncStandingsAsync(trackedId, CancellationToken.None);

        // Snapshots are append-only by design (CLAUDE.md section 10) — never overwritten.
        Assert.Equal(4, _dbContext.StandingSnapshots.Count());
    }

    [Fact]
    public async Task SyncStandingsAsync_UnknownTrackedCompetition_ThrowsNotFound()
    {
        var service = new FootballStandingsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(StandingsJson))), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SyncStandingsAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
