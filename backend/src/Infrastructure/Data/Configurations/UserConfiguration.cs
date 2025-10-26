using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private static readonly ValueConverter<AdelaideDepartment, string> DepartmentConverter = new(
        value => value.ToString(),
        value => ParseOrThrow<AdelaideDepartment>(value, nameof(User.Department)));

    private static readonly ValueConverter<AcademicDegree, string> DegreeConverter = new(
        value => value.ToString(),
        value => ParseOrThrow<AcademicDegree>(value, nameof(User.Degree)));

    private static readonly ValueConverter<UserSex, string> SexConverter = new(
        value => value.ToString(),
        value => ParseOrThrow<UserSex>(value, nameof(User.Sex)));

    private static readonly ValueConverter<Nationality?, string?> NationalityConverter = new(
        value => value.ToString(),
        value => string.IsNullOrWhiteSpace(value)
            ? null
            : ParseOrThrow<Nationality>(value!, nameof(User.Nationality)));

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(u => u.Department)
            .HasConversion(DepartmentConverter)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.Degree)
            .HasConversion(DegreeConverter)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.Sex)
            .HasConversion(SexConverter)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(512);

        builder.Property(u => u.Nationality)
            .HasConversion(NationalityConverter)
            .HasMaxLength(64);

        builder.Property(u => u.Age);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.ActivationToken)
            .HasMaxLength(128);

        builder.Property(u => u.ActivationTokenExpiresAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasMany(u => u.ItemsForSale)
            .WithOne(i => i.Seller)
            .HasForeignKey(i => i.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Orders)
            .WithOne(o => o.Buyer)
            .HasForeignKey(o => o.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static TEnum ParseOrThrow<TEnum>(string value, string propertyName)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unsupported value '{value}' for {propertyName}.");
    }
}
