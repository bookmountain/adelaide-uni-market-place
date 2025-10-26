namespace Infrastructure.Configuration.Options;

/// <summary>
/// Marker for configuration option types that declare their configuration section name.
/// </summary>
public interface IConfigSection
{
    static abstract string SectionName { get; }
}
