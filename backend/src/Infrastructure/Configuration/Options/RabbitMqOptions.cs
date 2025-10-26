using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class RabbitMqOptions : IConfigSection
{
    public static string SectionName => "RabbitMq";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = string.Empty;
}
