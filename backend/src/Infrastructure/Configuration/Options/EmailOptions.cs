using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class EmailOptions : IConfigSection
{
    public static string SectionName => "Email";

    [Required, EmailAddress]
    public string SenderAddress { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SenderName { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public bool UseStartTls { get; init; } = true;

    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;
}
