using SportsPredictor.Domain.Entities;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

public class FeatureValueTests
{
    [Fact]
    public async Task FeatureValue_AvailableAt_RoundTripsAsUtc()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        var competition = TestDataBuilder.Competition(sport);
        var season = TestDataBuilder.Season(competition);
        var home = TestDataBuilder.Team(sport, "Home");
        var away = TestDataBuilder.Team(sport, "Away");
        var match = TestDataBuilder.Match(competition, season, home, away);
        var availableAt = DateTime.UtcNow.AddDays(-1);

        db.Context.AddRange(sport, competition, season, home, away, match);
        db.Context.FeatureValues.Add(new FeatureValue
        {
            MatchId = match.Id,
            FeatureName = "elo_diff",
            NumericValue = 12.5,
            AvailableAt = availableAt,
            CapturedAt = DateTime.UtcNow,
        });
        await db.Context.SaveChangesAsync();

        db.Context.ChangeTracker.Clear();
        var saved = db.Context.FeatureValues.Single();

        // The anti-leakage rule (CLAUDE.md #19) depends on AvailableAt being
        // comparable to Match.MatchDate as an absolute instant, not local time.
        Assert.True(saved.AvailableAt < match.MatchDate);
    }
}
