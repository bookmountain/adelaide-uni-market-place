using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadCommentConfiguration : IEntityTypeConfiguration<ThreadComment>
{
    public void Configure(EntityTypeBuilder<ThreadComment> builder)
    {
        builder.ToTable("thread_comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.IsAnonymous).IsRequired();
        builder.Property(c => c.Body).IsRequired();
        builder.Property(c => c.LikeCount).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ThreadPost>()
            .WithMany()
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ThreadComment>()
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.PostId, c.CreatedAt });
        builder.HasIndex(c => c.ParentCommentId);
    }
}
