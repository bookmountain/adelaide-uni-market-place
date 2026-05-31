using Domain.Entities.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadReportConfiguration : IEntityTypeConfiguration<ThreadReport>
{
    public void Configure(EntityTypeBuilder<ThreadReport> builder)
    {
        builder.ToTable("thread_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TargetType).HasConversion<int>().IsRequired();
        builder.Property(r => r.Reason).HasConversion<int>().IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(r => r.ReviewedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.TargetType, r.TargetId });
    }
}
