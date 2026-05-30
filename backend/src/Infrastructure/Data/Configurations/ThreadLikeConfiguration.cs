using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadLikeConfiguration : IEntityTypeConfiguration<ThreadLike>
{
    public void Configure(EntityTypeBuilder<ThreadLike> builder)
    {
        builder.ToTable("thread_likes");
        builder.HasKey(l => new { l.UserId, l.TargetType, l.TargetId });
        builder.Property(l => l.TargetType).HasConversion<int>().IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(l => new { l.TargetType, l.TargetId });
    }
}
