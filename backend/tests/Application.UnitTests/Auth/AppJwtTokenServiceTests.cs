using System.IdentityModel.Tokens.Jwt;
using Infrastructure.Auth;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class AppJwtTokenServiceTests
{
    private static AppJwtTokenService NewService() => new(Options.Create(new AuthOptions
    {
        AppJwtIssuer = "http://localhost",
        AppJwtSigningKey = "local-development-signing-key-change-me-please",
        AllowedEmailDomain = "adelaide.edu.au",
        ActivationBaseUrl = "https://localhost:7123/api/auth/activate",
        AccessTokenMinutes = 15
    }));

    [Fact]
    public void IssueAccessToken_embeds_sub_email_and_role()
    {
        var service = NewService();
        var userId = Guid.NewGuid();

        var jwt = service.IssueAccessToken(userId, "student@adelaide.edu.au", "Student");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Contains(token.Claims, c => c.Type == "email" && c.Value == "student@adelaide.edu.au");
        Assert.Contains(token.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Student");
        Assert.True(token.ValidTo <= DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void GenerateRefreshToken_returns_distinct_opaque_values()
    {
        var service = NewService();

        var a = service.GenerateRefreshToken();
        var b = service.GenerateRefreshToken();

        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 32);
    }
}
