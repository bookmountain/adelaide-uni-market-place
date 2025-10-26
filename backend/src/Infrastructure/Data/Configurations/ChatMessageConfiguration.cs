using Domain.Entities.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(cm => cm.Id);

        builder.Property(cm => cm.ThreadId)
            .IsRequired();

        builder.Property(cm => cm.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(cm => cm.SentAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasOne(cm => cm.FromUser)
            .WithMany()
            .HasForeignKey(cm => cm.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cm => cm.ToUser)
            .WithMany()
            .HasForeignKey(cm => cm.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cm => cm.Item)
            .WithMany()
            .HasForeignKey(cm => cm.ItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(cm => new { cm.ThreadId, cm.SentAt });
    }
}
