using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;
}
