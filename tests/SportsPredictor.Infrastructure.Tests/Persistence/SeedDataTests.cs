using System.Linq;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

public class SeedDataTests
{
    [Fact]
    public void Database_SeedsFootballSport()
    {
        using var db = new SqliteTestDatabase();

        var football = db.Context.Sports.Single(s => s.Id == SportConfiguration.FootballId);

        Assert.Equal("Football", football.Name);
    }
}
