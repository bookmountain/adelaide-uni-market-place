using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration.Options;

public sealed class StripeOptions : IConfigSection
{
    public static string SectionName => "Stripe";

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; init; } = string.Empty;
}
