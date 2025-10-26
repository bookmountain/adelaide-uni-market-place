using System.ComponentModel.DataAnnotations;
using Domain.Shared.Enums;

namespace Contracts.DTO.Auth;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(512)]
    [Url]
    public string? AvatarUrl { get; init; }

    [Required]
    public AdelaideDepartment Department { get; init; }

    [Required]
    public AcademicDegree Degree { get; init; }

    [Required]
    public UserSex Sex { get; init; }

    public Nationality? Nationality { get; init; }

    public int? Age { get; init; }
}
