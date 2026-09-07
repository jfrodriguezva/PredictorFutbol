using Microsoft.EntityFrameworkCore;
using SportsPredictor.Domain.Entities;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

public class UniqueConstraintTests
{
    [Fact]
    public async Task Sport_DuplicateName_ThrowsOnSave()
    {
        using var db = new SqliteTestDatabase();
        var name = $"Duplicate-{Guid.NewGuid()}";
        db.Context.Sports.Add(new Sport { Name = name });
        await db.Context.SaveChangesAsync();

        db.Context.Sports.Add(new Sport { Name = name });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Team_TwoTeamsWithNullExternalId_BothSaveSuccessfully()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        db.Context.Sports.Add(sport);
        db.Context.Teams.AddRange(
            TestDataBuilder.Team(sport, "Team A"),
            TestDataBuilder.Team(sport, "Team B"));

        var affected = await db.Context.SaveChangesAsync();

        Assert.Equal(3, affected); // sport + 2 teams
    }

    [Fact]
    public async Task Team_DuplicateExternalApiFootballId_ThrowsOnSave()
    {
        using var db = new SqliteTestDatabase();
        var sport = TestDataBuilder.Sport();
        db.Context.Sports.Add(sport);
        db.Context.Teams.Add(new Team { SportId = sport.Id, Name = "Team A", ExternalApiFootballId = 42 });
        await db.Context.SaveChangesAsync();

        db.Context.Teams.Add(new Team { SportId = sport.Id, Name = "Team B", ExternalApiFootballId = 42 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }
}
