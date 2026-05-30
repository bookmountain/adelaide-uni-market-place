using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Users;

public sealed class UpdateProfileRequest
{
    [MaxLength(280)]
    public string? Bio { get; init; }

    public bool AppearInDrawPool { get; init; }
}
