using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class PostgresOptions : IConfigSection
{
    public static string SectionName => "Postgres";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;
}
