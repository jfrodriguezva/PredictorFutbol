using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Datasets;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Datasets;

public class DatasetBuilderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;
    private readonly DatasetBuilderService _service;

    private Competition _competition = null!;
    private Season _season = null!;
    private Team _home = null!;
    private Team _away = null!;
    private TrackedCompetition _tracked = null!;

    public DatasetBuilderServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SportsPredictorDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SportsPredictorDbContext(options);
        _dbContext.Database.EnsureCreated();
        _service = new DatasetBuilderService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task SeedCompetitionAsync()
    {
        _competition = new Competition { SportId = SportConfiguration.FootballId, Name = "Test League", CompetitionType = CompetitionType.League };
        _season = new Season { CompetitionId = _competition.Id, Name = "2024", StartDate = new DateOnly(2024, 8, 1), EndDate = new DateOnly(2025, 5, 1) };
        _home = new Team { SportId = SportConfiguration.FootballId, Name = "Home FC" };
        _away = new Team { SportId = SportConfiguration.FootballId, Name = "Away FC" };
        _tracked = new TrackedCompetition { CompetitionId = _competition.Id, ExternalLeagueId = 39, Season = 2024 };
        _dbContext.AddRange(_competition, _season, _home, _away, _tracked);
        await _dbContext.SaveChangesAsync();
    }

    private Match AddFinishedMatch(DateTime matchDate, Team home, Team away, int homeScore, int awayScore)
    {
        var match = new Match
        {
            CompetitionId = _competition.Id,
            SeasonId = _season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            MatchDate = matchDate,
            Status = MatchStatus.Finished,
            HomeScore = homeScore,
            AwayScore = awayScore,
        };
        _dbContext.Matches.Add(match);
        return match;
    }

    [Fact]
    public async Task BuildFeaturesAsync_ComputesStandingFromResultsStrictlyBeforeKickoff_NotAfter()
    {
        // API-Football's /standings only exposes the CURRENT table, so a captured
        // snapshot can never satisfy "before kickoff" for an already-played match.
        // Instead, the table-before-kickoff is derived from match results already
        // in SQLite — this test locks in that it uses only results strictly before
        // the target match's kickoff (anti-leakage) and computes points correctly.
        await SeedCompetitionAsync();
        var other = new Team { SportId = SportConfiguration.FootballId, Name = "Other FC" };
        _dbContext.Teams.Add(other);

        var baseDate = new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc);
        AddFinishedMatch(baseDate.AddDays(0), _home, other, 2, 0);  // home win: +3
        AddFinishedMatch(baseDate.AddDays(7), other, _home, 1, 1);  // draw: +1
        var kickoff = baseDate.AddDays(14);
        var match = AddFinishedMatch(kickoff, _home, _away, 2, 1);

        // Played AFTER kickoff: must never count toward "before kickoff" points (anti-leakage).
        AddFinishedMatch(kickoff.AddDays(7), _home, other, 5, 0);
        await _dbContext.SaveChangesAsync();

        var result = await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        Assert.Equal(4, result.MatchesProcessed); // all finished matches in the season get features, not just the target
        var pointsBefore = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.HomePointsBefore);
        Assert.Equal(4, pointsBefore.NumericValue); // 3 (win) + 1 (draw), excludes the post-kickoff match
        Assert.True(pointsBefore.AvailableAt <= match.MatchDate);
    }

    [Fact]
    public async Task BuildFeaturesAsync_ComputesFormPointsFromLastFiveFinishedMatches()
    {
        await SeedCompetitionAsync();
        var other = new Team { SportId = SportConfiguration.FootballId, Name = "Other FC" };
        _dbContext.Teams.Add(other);

        // Home team: win, win, draw, loss, win = 3+3+1+0+3 = 10, before the target match.
        var baseDate = new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc);
        AddFinishedMatch(baseDate.AddDays(0), _home, other, 2, 0);  // win
        AddFinishedMatch(baseDate.AddDays(7), other, _home, 0, 3);  // home away win
        AddFinishedMatch(baseDate.AddDays(14), _home, other, 1, 1); // draw
        AddFinishedMatch(baseDate.AddDays(21), other, _home, 2, 0); // home away loss
        AddFinishedMatch(baseDate.AddDays(28), _home, other, 4, 0); // win
        var target = AddFinishedMatch(baseDate.AddDays(35), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var form = _dbContext.FeatureValues.Single(f => f.MatchId == target.Id && f.FeatureName == FootballFeatureNames.HomeFormPointsLast5);
        Assert.Equal(10, form.NumericValue);
    }

    [Fact]
    public async Task BuildFeaturesAsync_CountsInjuriesOnlyWithinFourteenDayWindow()
    {
        await SeedCompetitionAsync();
        var kickoff = new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        var match = AddFinishedMatch(kickoff, _home, _away, 1, 0);
        var player = new Player { TeamId = _home.Id, Name = "Player A" };
        _dbContext.Players.Add(player);

        _dbContext.InjurySnapshots.Add(new InjurySnapshot
        {
            PlayerId = player.Id, TeamId = _home.Id, Type = "Injured", CapturedAt = kickoff.AddDays(-5), Source = "API-Football",
        });
        _dbContext.InjurySnapshots.Add(new InjurySnapshot
        {
            PlayerId = player.Id, TeamId = _home.Id, Type = "Injured", CapturedAt = kickoff.AddDays(-20), Source = "API-Football", // outside window
        });
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var count = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.HomeInjuriesCount);
        Assert.Equal(1, count.NumericValue);
    }

    [Fact]
    public async Task BuildFeaturesAsync_AveragesMarketImpliedProbabilityAcrossBookmakers()
    {
        await SeedCompetitionAsync();
        var kickoff = new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        var match = AddFinishedMatch(kickoff, _home, _away, 1, 0);
        _dbContext.OddsSnapshots.Add(new OddsSnapshot { MatchId = match.Id, Sportsbook = "Bet365", Market = "Match Winner", Selection = "Home", DecimalOdds = 2.0m, ImpliedProbability = 0.50, CapturedAt = kickoff.AddDays(-1) });
        _dbContext.OddsSnapshots.Add(new OddsSnapshot { MatchId = match.Id, Sportsbook = "Pinnacle", Market = "Match Winner", Selection = "Home", DecimalOdds = 2.5m, ImpliedProbability = 0.40, CapturedAt = kickoff.AddDays(-1) });
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var feature = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.MarketImpliedHomeProbability);
        Assert.Equal(0.45, feature.NumericValue, precision: 4);
    }

    [Fact]
    public async Task ExportDatasetCsvAsync_ProducesRowPerMatchWithResultAndFeatureColumns()
    {
        await SeedCompetitionAsync();
        var kickoff = new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        AddFinishedMatch(kickoff, _home, _away, 2, 1);
        await _dbContext.SaveChangesAsync();
        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var csv = await _service.ExportDatasetCsvAsync(_tracked.Id, CancellationToken.None);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + 1 match
        Assert.Contains("result", lines[0]);
        Assert.Contains(FootballFeatureNames.HomePointsBefore, lines[0]);
        Assert.Contains("Home FC", lines[1]);
        Assert.Contains(",H,", "," + lines[1].Split(',')[6] + ","); // result column = Home win
    }

    [Fact]
    public async Task BuildFeaturesAsync_UnknownTrackedCompetition_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.BuildFeaturesAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task BuildFeaturesAsync_EloDefaultsTo1500ForBothTeamsOnTheirFirstEverMatch()
    {
        await SeedCompetitionAsync();
        var match = AddFinishedMatch(new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var homeElo = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.HomeEloBefore);
        var awayElo = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.AwayEloBefore);
        Assert.Equal(1500.0, homeElo.NumericValue);
        Assert.Equal(1500.0, awayElo.NumericValue);
    }

    [Fact]
    public async Task BuildFeaturesAsync_EloRisesForATeamAfterAWin_AndCarriesIntoItsNextMatch()
    {
        // Elo is a running rating carried across the team's whole history — winning the
        // first match should raise the winner's rating going into its NEXT match, which
        // must be reflected even though that next match is against a third team (Elo is
        // not season/opponent scoped, unlike the points-based standing feature).
        await SeedCompetitionAsync();
        var third = new Team { SportId = SportConfiguration.FootballId, Name = "Third FC" };
        _dbContext.Teams.Add(third);

        AddFinishedMatch(new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 3, 0); // home wins big
        var nextMatch = AddFinishedMatch(new DateTime(2024, 9, 8, 15, 0, 0, DateTimeKind.Utc), _home, third, 1, 1);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var homeEloBeforeSecondMatch = _dbContext.FeatureValues.Single(f => f.MatchId == nextMatch.Id && f.FeatureName == FootballFeatureNames.HomeEloBefore);
        Assert.True(homeEloBeforeSecondMatch.NumericValue > 1500.0, $"expected > 1500 after a win, got {homeEloBeforeSecondMatch.NumericValue}");
    }

    [Fact]
    public async Task BuildFeaturesAsync_HeadToHeadDefaultsToNeutral_WithNoPriorMeeting()
    {
        await SeedCompetitionAsync();
        var match = AddFinishedMatch(new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var h2h = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.HeadToHeadHomeWinRate);
        Assert.Equal(0.5, h2h.NumericValue);
    }

    [Fact]
    public async Task BuildFeaturesAsync_HeadToHeadCountsAWinRegardlessOfWhichSideTheTeamPlayedOnBefore()
    {
        await SeedCompetitionAsync();
        // Earlier meeting: _away hosted and beat _home. In the target match _home hosts —
        // so from THIS match's home team's perspective, that earlier result was a loss.
        AddFinishedMatch(new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc), _away, _home, 2, 0);
        var target = AddFinishedMatch(new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var h2h = _dbContext.FeatureValues.Single(f => f.MatchId == target.Id && f.FeatureName == FootballFeatureNames.HeadToHeadHomeWinRate);
        Assert.Equal(0.0, h2h.NumericValue); // home team lost their only prior meeting
    }

    [Fact]
    public async Task BuildFeaturesAsync_RestDaysDefaultsToSevenWithNoPriorMatch_OtherwiseCountsRealDays()
    {
        await SeedCompetitionAsync();
        var firstMatch = AddFinishedMatch(new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        var secondMatch = AddFinishedMatch(new DateTime(2024, 9, 5, 15, 0, 0, DateTimeKind.Utc), _home, _away, 0, 1);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var firstRest = _dbContext.FeatureValues.Single(f => f.MatchId == firstMatch.Id && f.FeatureName == FootballFeatureNames.HomeRestDays);
        Assert.Equal(7.0, firstRest.NumericValue);

        var secondRest = _dbContext.FeatureValues.Single(f => f.MatchId == secondMatch.Id && f.FeatureName == FootballFeatureNames.HomeRestDays);
        Assert.Equal(4.0, secondRest.NumericValue);
    }

    [Fact]
    public async Task BuildFeaturesAsync_VenueSpecificFormOnlyCountsMatchesPlayedOnThatSide()
    {
        // _home lost its only AWAY match but won its only HOME match — overall form
        // would blend both, but home_form_at_home_last5 must reflect only the home win.
        await SeedCompetitionAsync();
        var third = new Team { SportId = SportConfiguration.FootballId, Name = "Third FC" };
        _dbContext.Teams.Add(third);

        AddFinishedMatch(new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc), third, _home, 3, 0); // _home lost away
        AddFinishedMatch(new DateTime(2024, 9, 8, 15, 0, 0, DateTimeKind.Utc), _home, third, 2, 0); // _home won at home
        var target = AddFinishedMatch(new DateTime(2024, 9, 15, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var homeAtHomeForm = _dbContext.FeatureValues.Single(f => f.MatchId == target.Id && f.FeatureName == FootballFeatureNames.HomeFormAtHomeLast5);
        Assert.Equal(3.0, homeAtHomeForm.NumericValue); // only the home win counts, not the away loss
    }

    [Fact]
    public async Task BuildFeaturesAsync_PoissonDrawProbabilityIsHigherForTwoLowScoringTeams()
    {
        // Two teams that consistently draw 0-0/1-1 (low scoring, evenly matched) should
        // get a materially higher Poisson draw probability than two high-scoring teams —
        // this is the whole point of the feature: distinguishing "low scoring" from
        // "closely matched," which Elo/points/rank cannot do on their own.
        await SeedCompetitionAsync();
        var lowA = new Team { SportId = SportConfiguration.FootballId, Name = "Low A" };
        var lowB = new Team { SportId = SportConfiguration.FootballId, Name = "Low B" };
        var highA = new Team { SportId = SportConfiguration.FootballId, Name = "High A" };
        var highB = new Team { SportId = SportConfiguration.FootballId, Name = "High B" };
        _dbContext.Teams.AddRange(lowA, lowB, highA, highB);

        var baseDate = new DateTime(2024, 9, 1, 15, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            AddFinishedMatch(baseDate.AddDays(i * 3), lowA, lowB, 1, 0);
            AddFinishedMatch(baseDate.AddDays(i * 3 + 1), highA, highB, 3, 3);
        }

        var lowScoringTarget = AddFinishedMatch(baseDate.AddDays(20), lowA, lowB, 0, 0);
        var highScoringTarget = AddFinishedMatch(baseDate.AddDays(21), highA, highB, 2, 2);
        await _dbContext.SaveChangesAsync();

        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var lowScoringDrawProb = _dbContext.FeatureValues.Single(f => f.MatchId == lowScoringTarget.Id && f.FeatureName == FootballFeatureNames.PoissonDrawProbability);
        var highScoringDrawProb = _dbContext.FeatureValues.Single(f => f.MatchId == highScoringTarget.Id && f.FeatureName == FootballFeatureNames.PoissonDrawProbability);
        Assert.True(lowScoringDrawProb.NumericValue > highScoringDrawProb.NumericValue,
            $"expected low-scoring matchup ({lowScoringDrawProb.NumericValue}) to have a higher draw probability than high-scoring ({highScoringDrawProb.NumericValue})");
    }

    [Fact]
    public async Task BuildFeaturesAsync_FlagsCupCompetitionsButNotLeagues()
    {
        await SeedCompetitionAsync();
        var match = AddFinishedMatch(new DateTime(2024, 10, 1, 15, 0, 0, DateTimeKind.Utc), _home, _away, 1, 0);
        await _dbContext.SaveChangesAsync();
        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var leagueFlag = _dbContext.FeatureValues.Single(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.IsCupCompetition);
        Assert.Equal(0.0, leagueFlag.NumericValue);

        _competition.CompetitionType = CompetitionType.Cup;
        await _dbContext.SaveChangesAsync();
        await _service.BuildFeaturesAsync(_tracked.Id, CancellationToken.None);

        var cupFlag = _dbContext.FeatureValues
            .Where(f => f.MatchId == match.Id && f.FeatureName == FootballFeatureNames.IsCupCompetition)
            .OrderByDescending(f => f.CapturedAt)
            .First();
        Assert.Equal(1.0, cupFlag.NumericValue);
    }
}
