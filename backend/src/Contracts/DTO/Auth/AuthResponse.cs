using Domain.Shared.Enums;

namespace Contracts.DTO.Auth;

public sealed record AuthResponse(string Token, string RefreshToken, AuthUserDto User);

public sealed record AuthUserDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    AdelaideDepartment Department,
    AcademicDegree Degree,
    UserSex Sex,
    string? AvatarUrl,
    Nationality? Nationality,
    int? Age,
    string? Bio,
    bool AppearInDrawPool,
    bool IsAdmin);
