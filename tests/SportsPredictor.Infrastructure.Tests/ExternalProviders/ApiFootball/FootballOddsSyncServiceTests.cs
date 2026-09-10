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

public class FootballOddsSyncServiceTests : IDisposable
{
    private const string OddsJson = """
        {
          "results": 1,
          "errors": [],
          "response": [
            {
              "bookmakers": [
                {
                  "name": "Bet365",
                  "bets": [
                    {
                      "name": "Match Winner",
                      "values": [
                        { "value": "Home", "odd": "1.85" },
                        { "value": "Draw", "odd": "3.60" },
                        { "value": "Away", "odd": "4.20" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;
    private Match _match = null!;

    public FootballOddsSyncServiceTests()
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

    private async Task SeedMatchAsync()
    {
        var competition = new Competition { SportId = SportConfiguration.FootballId, Name = "L", CompetitionType = CompetitionType.League };
        var season = new Season { CompetitionId = competition.Id, Name = "2024", StartDate = new DateOnly(2024, 8, 1), EndDate = new DateOnly(2025, 5, 1) };
        var home = new Team { SportId = SportConfiguration.FootballId, Name = "Home" };
        var away = new Team { SportId = SportConfiguration.FootballId, Name = "Away" };
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
        _dbContext.AddRange(competition, season, home, away, _match);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task SyncOddsAsync_CreatesOneSnapshotPerValue_WithComputedImpliedProbability()
    {
        await SeedMatchAsync();
        var service = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);

        var result = await service.SyncOddsAsync(_match.Id, CancellationToken.None);

        Assert.Equal(3, result.SnapshotsCreated);
        Assert.Equal(3, _dbContext.OddsSnapshots.Count());
        var home = _dbContext.OddsSnapshots.Single(o => o.Selection == "Home");
        Assert.Equal(1.85m, home.DecimalOdds);
        Assert.Null(home.AmericanOdds);
        Assert.Equal(1.0 / 1.85, home.ImpliedProbability, precision: 4);
        Assert.Equal("Bet365", home.Sportsbook);
        Assert.Equal("Match Winner", home.Market);
    }

    [Fact]
    public async Task SyncOddsAsync_CalledTwice_AppendsInsteadOfOverwriting()
    {
        await SeedMatchAsync();
        var first = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);
        await first.SyncOddsAsync(_match.Id, CancellationToken.None);

        var second = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);
        await second.SyncOddsAsync(_match.Id, CancellationToken.None);

        Assert.Equal(6, _dbContext.OddsSnapshots.Count());
    }

    [Fact]
    public async Task SyncOddsAsync_BetValueSentAsJsonNumber_DoesNotThrow()
    {
        // Real-world case that surfaced against live Liga MX odds: for handicap/total-line
        // bet types, API-Football sends "value" as a bare JSON number (e.g. 2.5) instead of
        // a string, which crashed System.Text.Json deserialization before the fix.
        const string oddsWithNumericValueJson = """
            {
              "results": 1,
              "errors": [],
              "response": [
                {
                  "bookmakers": [
                    {
                      "name": "Bet365",
                      "bets": [
                        {
                          "name": "Match Winner",
                          "values": [
                            { "value": "Home", "odd": "1.85" }
                          ]
                        },
                        {
                          "name": "Asian Handicap",
                          "values": [
                            { "value": 2.5, "odd": "1.90" }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        await SeedMatchAsync();
        var service = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(oddsWithNumericValueJson))), _dbContext);

        var result = await service.SyncOddsAsync(_match.Id, CancellationToken.None);

        Assert.Equal(2, result.SnapshotsCreated);
        Assert.Contains(_dbContext.OddsSnapshots, o => o.Market == "Asian Handicap" && o.Selection == "2.5");
    }

    [Fact]
    public async Task SyncOddsAsync_MatchAlreadyPlayed_BackdatesCapturedAtToKickoff()
    {
        // Anti-leakage regression: DatasetBuilderService only ever considers odds with
        // CapturedAt <= match.MatchDate. Backfilling odds for an already-played match
        // days/weeks later with CapturedAt = "now" would silently make that data
        // invisible to training forever, even though the odds themselves are real.
        await SeedMatchAsync();
        var kickoff = DateTime.UtcNow.AddDays(-10);
        _match.MatchDate = kickoff;
        _match.Status = MatchStatus.Finished;
        _match.HomeScore = 1;
        _match.AwayScore = 0;
        await _dbContext.SaveChangesAsync();

        var service = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);
        var result = await service.SyncOddsAsync(_match.Id, CancellationToken.None);

        Assert.Equal(kickoff, result.CapturedAtUtc);
        Assert.All(_dbContext.OddsSnapshots, o => Assert.Equal(kickoff, o.CapturedAt));
    }

    [Fact]
    public async Task SyncOddsAsync_UpcomingMatch_CapturedAtIsNow()
    {
        await SeedMatchAsync();
        _match.MatchDate = DateTime.UtcNow.AddDays(5);
        await _dbContext.SaveChangesAsync();
        var before = DateTime.UtcNow;

        var service = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);
        await service.SyncOddsAsync(_match.Id, CancellationToken.None);

        var after = DateTime.UtcNow;
        Assert.All(_dbContext.OddsSnapshots, o => Assert.InRange(o.CapturedAt, before, after));
    }

    [Fact]
    public async Task SyncOddsAsync_UnknownMatch_ThrowsNotFound()
    {
        var service = new FootballOddsSyncService(CreateClient(FakeHttpMessageHandler.Sequence(JsonResponse(OddsJson))), _dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SyncOddsAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
