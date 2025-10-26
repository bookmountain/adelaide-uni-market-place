using Domain.Entities.Categories;
using Domain.Entities.Items;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Seeding;

public sealed class DatabaseSeeder
{
    private const string SeedPasswordHash = "$2a$11$.bgaTMiXjFxfZvHMAKcq3OQYEsoK6jhXqnCgpDwubTSD1c9uAYKyC"; // ChangeMe123!
    private static readonly Guid SeedUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static readonly Category[] DefaultCategories =
    {
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000101"), "Textbooks", "textbooks"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000102"), "Electronics", "electronics"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000103"), "Furniture", "furniture"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000104"), "Stationery", "stationery"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000105"), "Clothing", "clothing"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000106"), "Sports & Recreation", "sports-recreation"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000107"), "Transport", "transport"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000108"), "Events & Tickets", "events-tickets"),
        new Category(Guid.Parse("00000000-0000-0000-0000-000000000109"), "Miscellaneous", "miscellaneous")
    };

    private readonly Dictionary<string, Guid> _categoryLookup = new(StringComparer.OrdinalIgnoreCase);

    private readonly MarketplaceDbContext _dbContext;

    public DatabaseSeeder(MarketplaceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCategories(cancellationToken);
        var sellerId = await SeedUserAsync(cancellationToken);
        await SeedItemsAsync(sellerId, cancellationToken);
    }

    private async Task SeedCategories(CancellationToken cancellationToken)
    {
        foreach (var category in DefaultCategories)
        {
            var existing = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == category.Slug, cancellationToken);

            if (existing is null)
            {
                await _dbContext.Categories.AddAsync(category, cancellationToken);
                _categoryLookup[category.Slug] = category.Id;
            }
            else
            {
                _categoryLookup[existing.Slug] = existing.Id;
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Guid> SeedUserAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == "student@adelaide.edu.au", cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.Activate();
                existing.UpdatePassword(SeedPasswordHash);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return existing.Id;
        }

        var user = new User(
            SeedUserId,
            "student@adelaide.edu.au",
            "Seed Student",
            DateTimeOffset.UtcNow,
            role: "Student",
            passwordHash: SeedPasswordHash,
            department: AdelaideDepartment.ComputerScience,
            degree: AcademicDegree.Bachelor,
            sex: UserSex.PreferNotToSay,
            avatarUrl: "https://images.unsplash.com/photo-1527980965255-d3b416303d12?w=640",
            nationality: Nationality.Australia,
            age: 21,
            isActive: true);

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    private async Task SeedItemsAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        if (await _dbContext.Items.AnyAsync(cancellationToken))
        {
            return;
        }

        var textbooksId = await GetCategoryIdAsync("textbooks", cancellationToken);
        var electronicsId = await GetCategoryIdAsync("electronics", cancellationToken);

        var calculusBook = new Item(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            sellerId,
            textbooksId,
            "Calculus II Textbook",
            "Lightly highlighted second-year calculus book in great condition.",
            45.00m,
            ItemStatus.Active,
            DateTimeOffset.UtcNow.AddDays(-7));
        calculusBook.AddImage(new ListingImage(Guid.NewGuid(), calculusBook.Id, "https://images.unsplash.com/photo-1524995997946-a1c2e315a42f?w=800", 1));
        calculusBook.AddImage(new ListingImage(Guid.NewGuid(), calculusBook.Id, "https://images.unsplash.com/photo-1522204538344-d26c3a3b271e?w=800", 2));

        var laptop = new Item(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            sellerId,
            electronicsId,
            "13\" Ultrabook Laptop",
            "Lightweight laptop, perfect for lectures. Includes charger and sleeve.",
            620.00m,
            ItemStatus.Active,
            DateTimeOffset.UtcNow.AddDays(-3));
        laptop.AddImage(new ListingImage(Guid.NewGuid(), laptop.Id, "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=800", 1));
        laptop.AddImage(new ListingImage(Guid.NewGuid(), laptop.Id, "https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?w=800", 2));

        await _dbContext.Items.AddRangeAsync(new[] { calculusBook, laptop }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> GetCategoryIdAsync(string slug, CancellationToken cancellationToken)
    {
        if (_categoryLookup.TryGetValue(slug, out var id))
        {
            return id;
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken)
            ?? throw new InvalidOperationException($"Category with slug '{slug}' not found.");

        _categoryLookup[slug] = category.Id;
        return category.Id;
    }
}
