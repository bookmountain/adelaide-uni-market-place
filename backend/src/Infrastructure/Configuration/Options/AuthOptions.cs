using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class AuthOptions : IConfigSection
{
    public static string SectionName => "Auth";

    [Required(AllowEmptyStrings = false)]
    public string AppJwtIssuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "App JWT signing key must be at least 32 characters.")]
    public string AppJwtSigningKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string AllowedEmailDomain { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [Url]
    public string ActivationBaseUrl { get; init; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 14;

    [Range(1, 100)]
    public int LoginMaxFailuresPerEmail { get; init; } = 5;

    [Range(1, 200)]
    public int LoginMaxFailuresPerIp { get; init; } = 10;

    [Range(1, 1440)]
    public int LoginFailureWindowMinutes { get; init; } = 15;
}
