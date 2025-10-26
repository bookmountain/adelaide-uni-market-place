using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class R2Options : IConfigSection
{
    public static string SectionName => "R2";

    [Required(AllowEmptyStrings = false)]
    public string AccountId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string AccessKeyId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SecretAccessKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Bucket { get; init; } = string.Empty;
}
