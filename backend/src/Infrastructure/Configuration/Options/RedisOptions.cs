using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class RedisOptions : IConfigSection
{
    public static string SectionName => "Redis";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;
}
