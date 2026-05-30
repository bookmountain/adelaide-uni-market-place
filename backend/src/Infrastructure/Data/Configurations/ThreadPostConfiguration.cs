using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadPostConfiguration : IEntityTypeConfiguration<ThreadPost>
{
    public void Configure(EntityTypeBuilder<ThreadPost> builder)
    {
        builder.ToTable("thread_posts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.IsAnonymous).IsRequired();
        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Body).IsRequired();
        builder.Property(p => p.LikeCount).IsRequired();
        builder.Property(p => p.CommentCount).IsRequired();
        builder.Property(p => p.LastActivityAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(p => p.IsPinned).IsRequired();
        builder.Property(p => p.IsLocked).IsRequired();
        builder.Property(p => p.IsDeleted).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.CategoryId, p.LastActivityAt });
        builder.HasIndex(p => new { p.AuthorUserId, p.CreatedAt });
    }
}
