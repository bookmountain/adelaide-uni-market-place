using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeding;

public sealed class DatabaseSeeder
{
    private static readonly Guid SeedCategoryId = Guid.Parse("00000000-0000-0000-0000-000000000101"); // Textbooks
    private static readonly Guid SeedItemId = Guid.Parse("20000000-0000-0000-0000-000000000001"); // Calculus II Textbook
    private const string SeedUserEmail = "student@adelaide.edu.au";

    private readonly MarketplaceDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        MarketplaceDbContext dbContext,
        IHostEnvironment environment,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await SeedAlreadyAppliedAsync(cancellationToken))
        {
            _logger.LogInformation("Seed data already present; skipping execution.");
            return;
        }

        var scriptPath = ResolveSeedScriptPath();
        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("Seed script not found at {Path}. Skipping database seed.", scriptPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sql))
        {
            _logger.LogInformation("Seed script {Path} is empty. Nothing to execute.", scriptPath);
            return;
        }

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        _logger.LogInformation("Executed seed script {Path}.", scriptPath);
    }

    private async Task<bool> SeedAlreadyAppliedAsync(CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == SeedCategoryId, cancellationToken);

        var itemExists = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(i => i.Id == SeedItemId, cancellationToken);

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == SeedUserEmail, cancellationToken);

        return categoryExists && itemExists && userExists;
    }

    private string ResolveSeedScriptPath()
    {
        // ContentRoot points to backend/src/Api during runtime.
        // Navigate back to the repository root to locate db/seed.sql.
        var basePath = _environment.ContentRootPath;
        var relative = Path.Combine("..", "..", "db", "seed.sql");
        return Path.GetFullPath(Path.Combine(basePath, relative));
    }
}
