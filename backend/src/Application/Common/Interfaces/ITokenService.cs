namespace Application.Common.Interfaces;

public interface ITokenService
{
    string IssueAccessToken(Guid userId, string email, string? role);
    string GenerateRefreshToken();
}
