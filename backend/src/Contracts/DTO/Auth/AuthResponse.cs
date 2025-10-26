using Domain.Shared.Enums;

namespace Contracts.DTO.Auth;

public sealed record AuthResponse(string Token, AuthUserDto User);

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
    int? Age);
