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

public static class AuthUserDtoFactory
{
    public static AuthUserDto FromUser(Domain.Entities.Users.User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role,
        user.Department,
        user.Degree,
        user.Sex,
        user.AvatarUrl,
        user.Nationality,
        user.Age,
        user.Bio,
        user.AppearInDrawPool,
        user.IsAdmin);
}
