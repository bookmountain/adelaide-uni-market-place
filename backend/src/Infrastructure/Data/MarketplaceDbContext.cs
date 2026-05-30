using Application.Common.Interfaces;
using Domain.Entities.Categories;
using Domain.Entities.Chats;
using Domain.Entities.Items;
using Domain.Entities.Orders;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class MarketplaceDbContext : DbContext, IApplicationDbContext
{
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ThreadCategory> ThreadCategories => Set<ThreadCategory>();
    public DbSet<ThreadPost> ThreadPosts => Set<ThreadPost>();
    public DbSet<ThreadPostImage> ThreadPostImages => Set<ThreadPostImage>();
    public DbSet<ThreadComment> ThreadComments => Set<ThreadComment>();
    public DbSet<ThreadLike> ThreadLikes => Set<ThreadLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);
    }
}
