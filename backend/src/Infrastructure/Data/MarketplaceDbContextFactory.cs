using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

public sealed class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("Postgres__ConnectionString")
            ?? "Host=localhost;Database=marketplace;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MarketplaceDbContext(options);
    }
}
