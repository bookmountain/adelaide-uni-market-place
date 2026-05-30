using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadPostImageConfiguration : IEntityTypeConfiguration<ThreadPostImage>
{
    public void Configure(EntityTypeBuilder<ThreadPostImage> builder)
    {
        builder.ToTable("thread_post_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.R2Key).IsRequired().HasMaxLength(512);
        builder.Property(i => i.Ordinal).IsRequired();
        builder.HasIndex(i => i.PostId);
    }
}
