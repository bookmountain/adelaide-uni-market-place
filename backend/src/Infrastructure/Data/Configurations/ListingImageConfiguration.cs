using Domain.Entities.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ListingImageConfiguration : IEntityTypeConfiguration<ListingImage>
{
    public void Configure(EntityTypeBuilder<ListingImage> builder)
    {
        builder.ToTable("listing_images");

        builder.HasKey(li => li.Id);

        builder.Property(li => li.Url)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(li => li.SortOrder)
            .IsRequired();

        builder.HasOne(li => li.Item)
            .WithMany(i => i.Images)
            .HasForeignKey(li => li.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(li => new { li.ItemId, li.SortOrder })
            .IsUnique();
    }
}
