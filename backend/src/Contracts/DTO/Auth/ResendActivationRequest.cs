using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Auth;

public sealed class ResendActivationRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

