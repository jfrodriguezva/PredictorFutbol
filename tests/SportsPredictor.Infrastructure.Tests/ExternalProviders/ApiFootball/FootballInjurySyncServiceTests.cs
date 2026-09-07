using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class FootballInjurySyncServiceTests : IDisposable
{
    private const string SingleLeagueJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "league": { "id": 39, "name": "Premier League", "type": "League", "logo": "" },
              "country": { "name": "England", "code": "GB", "flag": "" },
              "seasons": [ { "year": 2024, "start": "2024-08-11", "end": "2025-05-25", "current": true } ]
            }
          ]
        }
        """;

    private const string InjuriesJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            {
              "player": { "id": 501, "name": "Marcus Rashford", "type": "Questionable", "reason": "Hamstring Injury" },
              "team": { "id": 33, "name": "Manchester United" },
              "fixture": { "id": 100001 }
            },
            {
              "player": { "id": 502, "name": "Bruno Fernandes", "type": "Doubtful", "reason": "Illness" },
              "team": { "id": 33, "name": "Manchester United" },
              "fixture": null
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballInjurySyncServiceTests()
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

    private async Task<Guid> CreateTrackedCompetitionAsync()
    {
        var reference = new FootballReferenceSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(SingleLeagueJson))), _dbContext);
        var tracked = await reference.CreateTrackedCompetitionAsync(
            new CreateTrackedCompetitionRequest(ExternalLeagueId: 39, Season: 2024),
            CancellationToken.None);
        return tracked.Id;
    }

    [Fact]
    public async Task SyncInjuriesAsync_CreatesSnapshotsAndMinimalTeamAndPlayers()
    {
        var trackedId = await CreateTrackedCompetitionAsync();
        var service = new FootballInjurySyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(InjuriesJson))), _dbContext);

        var result = await service.SyncInjuriesAsync(trackedId, CancellationToken.None);

        Assert.Equal(2, result.SnapshotsCreated);
        Assert.Equal(1, result.TeamsCreated); // Man Utd, shared by both injuries
        Assert.Equal(2, result.PlayersCreated);
        Assert.Equal(2, _dbContext.InjurySnapshots.Count());
        Assert.Equal(1, _dbContext.Teams.Count());
        Assert.Equal(2, _dbContext.Players.Count());
    }

    [Fact]
    public async Task SyncInjuriesAsync_LinksMatchWhenFixtureKnown_NullWhenNot()
    {
        var trackedId = await CreateTrackedCompetitionAsync();

        // Seed a Match with ExternalApiFootballId 100001 so the first injury can link to it.
        var competitionId = _dbContext.TrackedCompetitions.Single().CompetitionId;
        var season = _dbContext.Seasons.Single();
        var homeTeam = new Team { SportId = SportConfiguration.FootballId, Name = "Home" };
        var awayTeam = new Team { SportId = SportConfiguration.FootballId, Name = "Away" };
        var match = new Match
        {
            ExternalApiFootballId = 100001,
            CompetitionId = competitionId,
            SeasonId = season.Id,
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            MatchDate = DateTime.UtcNow,
            Status = MatchStatus.Scheduled,
        };
        _dbContext.AddRange(homeTeam, awayTeam, match);
        await _dbContext.SaveChangesAsync();

        var service = new FootballInjurySyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(InjuriesJson))), _dbContext);
        await service.SyncInjuriesAsync(trackedId, CancellationToken.None);

        var linked = _dbContext.InjurySnapshots.Single(i => i.Type == "Questionable");
        var unlinked = _dbContext.InjurySnapshots.Single(i => i.Type == "Doubtful");
        Assert.Equal(match.Id, linked.MatchId);
        Assert.Null(unlinked.MatchId);
    }

    [Fact]
    public async Task SyncInjuriesAsync_UnknownTrackedCompetition_ThrowsNotFound()
    {
        var service = new FootballInjurySyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(InjuriesJson))), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SyncInjuriesAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
