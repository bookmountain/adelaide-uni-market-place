using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class ElasticsearchOptions : IConfigSection
{
    public static string SectionName => "Elastic";

    [Required(AllowEmptyStrings = false)]
    public string Uri { get; init; } = string.Empty;
}
