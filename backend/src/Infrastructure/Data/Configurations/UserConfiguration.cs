using System;
using System.Collections.Generic;
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
        value => ConvertRequired(value, DepartmentLookup, nameof(AdelaideDepartment)));

    private static readonly ValueConverter<AcademicDegree, string> DegreeConverter = new(
        value => value.ToString(),
        value => ConvertRequired(value, DegreeLookup, nameof(AcademicDegree)));

    private static readonly ValueConverter<UserSex, string> SexConverter = new(
        value => value.ToString(),
        value => ConvertRequired(value, SexLookup, nameof(UserSex)));

    private static readonly ValueConverter<Nationality?, string?> NationalityConverter = new(
        value => value.HasValue ? value.Value.ToString() : null,
        value => ConvertOptional(value, NationalityLookup, nameof(Nationality)));

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

    private static readonly IReadOnlyDictionary<string, AdelaideDepartment> DepartmentLookup = BuildLookup(
        Enum.GetValues<AdelaideDepartment>(),
        new (string alias, AdelaideDepartment value)[]
        {
            ("Computer Science", AdelaideDepartment.ComputerScience),
            ("Agricultural Science", AdelaideDepartment.AgriculturalScience),
            ("Chemical Engineering", AdelaideDepartment.ChemicalEngineering),
            ("Civil Engineering", AdelaideDepartment.CivilEnvironmentalAndMiningEngineering),
            ("Electrical Engineering", AdelaideDepartment.ElectricalAndElectronicEngineering),
            ("Mechanical Engineering", AdelaideDepartment.MechanicalEngineering),
            ("Environmental Science", AdelaideDepartment.EnvironmentalScienceAndManagement)
        });

    private static readonly IReadOnlyDictionary<string, AcademicDegree> DegreeLookup = BuildLookup(
        Enum.GetValues<AcademicDegree>(),
        new (string alias, AcademicDegree value)[]
        {
            ("Bachelor of IT", AcademicDegree.Bachelor),
            ("Bachelor of Science", AcademicDegree.Bachelor),
            ("Undergraduate", AcademicDegree.Bachelor),
            ("Masters", AcademicDegree.Master),
            ("Master's", AcademicDegree.Master),
            ("Postgraduate", AcademicDegree.Master),
            ("Doctorate", AcademicDegree.Doctor),
            ("PhD", AcademicDegree.Doctor)
        });

    private static readonly IReadOnlyDictionary<string, UserSex> SexLookup = BuildLookup(
        Enum.GetValues<UserSex>(),
        new (string alias, UserSex value)[]
        {
            ("Prefer not to say", UserSex.PreferNotToSay)
        });

    private static readonly IReadOnlyDictionary<string, Nationality> NationalityLookup = BuildLookup(
        Enum.GetValues<Nationality>(),
        new (string alias, Nationality value)[]
        {
            ("Australian", Nationality.Australia),
            ("United States of America", Nationality.UnitedStates),
            ("USA", Nationality.UnitedStates),
            ("UK", Nationality.UnitedKingdom),
            ("UAE", Nationality.UnitedArabEmirates),
            ("Republic of Korea", Nationality.SouthKorea)
        });

    private static TEnum ConvertRequired<TEnum>(string? value, IReadOnlyDictionary<string, TEnum> lookup, string label)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} value is required.");
        }

        var key = NormalizeKey(value);
        if (lookup.TryGetValue(key, out var matched))
        {
            return matched;
        }

        throw new InvalidOperationException($"Unsupported {label} value '{value}'.");
    }

    private static TEnum? ConvertOptional<TEnum>(string? value, IReadOnlyDictionary<string, TEnum> lookup, string label)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = NormalizeKey(value);
        if (lookup.TryGetValue(key, out var matched))
        {
            return matched;
        }

        throw new InvalidOperationException($"Unsupported {label} value '{value}'.");
    }

    private static IReadOnlyDictionary<string, TEnum> BuildLookup<TEnum>(IEnumerable<TEnum> values, IEnumerable<(string alias, TEnum value)> extras)
        where TEnum : struct, Enum
    {
        var dictionary = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            dictionary[NormalizeKey(value.ToString())] = value;
        }

        foreach (var (alias, mapped) in extras)
        {
            dictionary[NormalizeKey(alias)] = mapped;
        }

        return dictionary;
    }

    private static string NormalizeKey(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
