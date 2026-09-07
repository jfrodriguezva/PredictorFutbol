using Microsoft.EntityFrameworkCore;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

public class MatchConstraintTests
{
    [Fact]
    public async Task Match_HomeTeamEqualsAwayTeam_ThrowsOnSave()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        var competition = TestDataBuilder.Competition(sport);
        var season = TestDataBuilder.Season(competition);
        var team = TestDataBuilder.Team(sport, "Same Team");
        db.Context.AddRange(sport, competition, season, team);
        await db.Context.SaveChangesAsync();

        db.Context.Matches.Add(new Match
        {
            CompetitionId = competition.Id,
            SeasonId = season.Id,
            HomeTeamId = team.Id,
            AwayTeamId = team.Id,
            MatchDate = DateTime.UtcNow.AddDays(1),
            Status = MatchStatus.Scheduled,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Match_UnknownCompetitionId_ThrowsOnSave()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        var competition = TestDataBuilder.Competition(sport);
        var season = TestDataBuilder.Season(competition);
        var home = TestDataBuilder.Team(sport, "Home");
        var away = TestDataBuilder.Team(sport, "Away");
        db.Context.AddRange(sport, competition, season, home, away);
        await db.Context.SaveChangesAsync();

        db.Context.Matches.Add(new Match
        {
            CompetitionId = Guid.NewGuid(), // does not exist
            SeasonId = season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            MatchDate = DateTime.UtcNow.AddDays(1),
            Status = MatchStatus.Scheduled,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Match_ValidGraph_SavesSuccessfully()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        var competition = TestDataBuilder.Competition(sport);
        var season = TestDataBuilder.Season(competition);
        var home = TestDataBuilder.Team(sport, "Home");
        var away = TestDataBuilder.Team(sport, "Away");
        var match = TestDataBuilder.Match(competition, season, home, away);
        db.Context.AddRange(sport, competition, season, home, away, match);

        await db.Context.SaveChangesAsync();

        var saved = await db.Context.Matches.FindAsync(match.Id);
        Assert.NotNull(saved);
        Assert.Equal(MatchStatus.Scheduled, saved!.Status);
        Assert.Null(saved.HomeScore);
        Assert.Null(saved.AwayScore);
    }
}
