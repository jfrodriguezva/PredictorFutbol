using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Tests.Persistence;

/// <summary>
/// An isolated in-memory SQLite database per test, with the schema built directly
/// from the EF Core model (EnsureCreated, not migrations) so tests exercise the same
/// keys/indexes/constraints the real migration produces.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SportsPredictorDbContext Context { get; }

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SportsPredictorDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new SportsPredictorDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
