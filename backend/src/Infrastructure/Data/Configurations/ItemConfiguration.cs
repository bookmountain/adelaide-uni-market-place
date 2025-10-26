using Domain.Entities.Items;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(i => i.Description)
            .IsRequired();

        builder.Property(i => i.Price)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(i => new { i.SellerId, i.Status });

        builder.HasOne(i => i.Seller)
            .WithMany(u => u.ItemsForSale)
            .HasForeignKey(i => i.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(i => i.Images)
            .HasField("_images")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.Images)
            .WithOne(li => li.Item)
            .HasForeignKey(li => li.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
