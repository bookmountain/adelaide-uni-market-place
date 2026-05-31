using Domain.Entities.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ModerationAuditConfiguration : IEntityTypeConfiguration<ModerationAudit>
{
    public void Configure(EntityTypeBuilder<ModerationAudit> builder)
    {
        builder.ToTable("moderation_audits");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TargetType).HasConversion<int>().IsRequired();
        builder.Property(a => a.Action).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Reason).HasMaxLength(256);
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(a => a.AdminUserId);
    }
}
