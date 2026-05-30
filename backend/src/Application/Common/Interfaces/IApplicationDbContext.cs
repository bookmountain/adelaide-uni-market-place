using Domain.Entities.Categories;
using Domain.Entities.Chats;
using Domain.Entities.Items;
using Domain.Entities.Orders;
using Domain.Entities.Outbox;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Item> Items { get; }
    DbSet<ListingImage> ListingImages { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ThreadCategory> ThreadCategories { get; }
    DbSet<ThreadPost> ThreadPosts { get; }
    DbSet<ThreadPostImage> ThreadPostImages { get; }
    DbSet<ThreadComment> ThreadComments { get; }
    DbSet<ThreadLike> ThreadLikes { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
