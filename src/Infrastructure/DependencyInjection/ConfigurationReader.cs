using Microsoft.Extensions.Configuration;

namespace Mgo2Server.Infrastructure.DependencyInjection;

/// <summary>
/// Reads the flat environment variables the servers are configured with. Every
/// setting is a single upper-case name without separators, so a variable is
/// written exactly once and is never split into a section and a key.
/// </summary>
internal static class ConfigurationReader
{
    /// <summary>Reads a text setting.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <param name="name">Name of the setting.</param>
    /// <returns>The value, or <c>null</c> when it was not set.</returns>
    public static string? ReadText(this IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Reads a whole-number setting.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <param name="name">Name of the setting.</param>
    /// <param name="fallback">Value used when the setting is absent or malformed.</param>
    public static int ReadNumber(this IConfiguration configuration, string name, int fallback) =>
        int.TryParse(configuration.ReadText(name), out var value) ? value : fallback;

    /// <summary>Reads a boolean setting.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <param name="name">Name of the setting.</param>
    /// <param name="fallback">Value used when the setting is absent or malformed.</param>
    public static bool ReadFlag(this IConfiguration configuration, string name, bool fallback) =>
        bool.TryParse(configuration.ReadText(name), out var value) ? value : fallback;
}
