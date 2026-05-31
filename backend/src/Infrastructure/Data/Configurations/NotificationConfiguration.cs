using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.ActorAnonHandleSnapshot).HasMaxLength(64);
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt });
        // Idempotency: at most one reply-notification per source comment.
        builder.HasIndex(n => n.SourceCommentId).IsUnique();
    }
}
