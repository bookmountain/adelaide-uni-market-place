using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Common;

public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDb(SqliteConnection connection, MarketplaceDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public MarketplaceDbContext Context { get; }

    public static async Task<TestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MarketplaceDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDb(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
