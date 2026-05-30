using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Auth;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
