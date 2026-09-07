using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

/// <summary>Builds a minimal, valid reference-data graph for persistence tests.</summary>
internal static class TestDataBuilder
{
    public static Sport Sport() => new() { Name = $"Sport-{Guid.NewGuid()}" };

    public static Competition Competition(Sport sport) => new()
    {
        SportId = sport.Id,
        Name = "Test League",
        CompetitionType = CompetitionType.League,
    };

    public static Season Season(Competition competition) => new()
    {
        CompetitionId = competition.Id,
        Name = "2025/2026",
        StartDate = new DateOnly(2025, 8, 1),
        EndDate = new DateOnly(2026, 5, 31),
    };

    public static Team Team(Sport sport, string name) => new()
    {
        SportId = sport.Id,
        Name = name,
    };

    public static Match Match(Competition competition, Season season, Team home, Team away) => new()
    {
        CompetitionId = competition.Id,
        SeasonId = season.Id,
        HomeTeamId = home.Id,
        AwayTeamId = away.Id,
        MatchDate = DateTime.UtcNow.AddDays(1),
        Status = MatchStatus.Scheduled,
    };
}
