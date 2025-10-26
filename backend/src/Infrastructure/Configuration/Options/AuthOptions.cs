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
}
