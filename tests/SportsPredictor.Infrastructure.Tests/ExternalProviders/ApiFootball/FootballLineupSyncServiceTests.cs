using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class FootballLineupSyncServiceTests : IDisposable
{
    private const string LineupsJson = """
        {
          "results": 2,
          "errors": [],
          "response": [
            { "team": { "id": 33, "name": "Manchester United" }, "formation": "4-2-3-1", "coach": { "id": 1, "name": "Erik ten Hag" } },
            { "team": { "id": 34, "name": "Newcastle" }, "formation": "4-3-3", "coach": { "id": 2, "name": "Eddie Howe" } }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;
    private Match _match = null!;

    public FootballLineupSyncServiceTests()
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

    private async Task SeedMatchWithKnownTeamsAsync()
    {
        var sport = new Sport { Name = "TestSport" };
        var competition = new Competition { SportId = SportConfiguration.FootballId, Name = "L", CompetitionType = CompetitionType.League };
        var season = new Season { CompetitionId = competition.Id, Name = "2024", StartDate = new DateOnly(2024, 8, 1), EndDate = new DateOnly(2025, 5, 1) };
        var home = new Team { SportId = SportConfiguration.FootballId, ExternalApiFootballId = 33, Name = "Manchester United" };
        var away = new Team { SportId = SportConfiguration.FootballId, ExternalApiFootballId = 34, Name = "Newcastle" };
        _match = new Match
        {
            ExternalApiFootballId = 100001,
            CompetitionId = competition.Id,
            SeasonId = season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            MatchDate = DateTime.UtcNow,
            Status = MatchStatus.Scheduled,
        };
        _dbContext.AddRange(sport, competition, season, home, away, _match);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task SyncLineupsAsync_CreatesOneSnapshotPerTeam()
    {
        await SeedMatchWithKnownTeamsAsync();
        var service = new FootballLineupSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(LineupsJson))), _dbContext);

        var result = await service.SyncLineupsAsync(_match.Id, CancellationToken.None);

        Assert.Equal(2, result.SnapshotsCreated);
        Assert.Equal(2, _dbContext.LineupSnapshots.Count());
        Assert.Contains(_dbContext.LineupSnapshots, l => l.Formation == "4-2-3-1" && l.CoachName == "Erik ten Hag");
    }

    [Fact]
    public async Task SyncLineupsAsync_UnknownMatch_ThrowsNotFound()
    {
        var service = new FootballLineupSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(LineupsJson))), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SyncLineupsAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
