using Domain.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(e => e.PublishedAt).HasColumnType("timestamp with time zone");
        // Publisher polls unpublished rows oldest-first.
        builder.HasIndex(e => new { e.PublishedAt, e.OccurredAt });
    }
}
