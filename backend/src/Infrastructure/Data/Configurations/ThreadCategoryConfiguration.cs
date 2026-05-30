using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadCategoryConfiguration : IEntityTypeConfiguration<ThreadCategory>
{
    public void Configure(EntityTypeBuilder<ThreadCategory> builder)
    {
        builder.ToTable("thread_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(512);
        builder.Property(c => c.IconKey).IsRequired().HasMaxLength(64);
        builder.Property(c => c.SortOrder).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}
