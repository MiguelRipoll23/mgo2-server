using System.Text.Json;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Reads the options a slash command arrives with.
/// <para>
/// Discord sends every option as JSON, and a value arrives as a string or a
/// number or a boolean depending on the type the command was registered with
/// rather than depending on what it means. Reading them apart in one place is
/// what lets a command ask for "the teams option" and get a number back
/// without each handler repeating the same three-way test, and without one of
/// them mistaking a number for the string it was registered as.
/// </para>
/// <para>
/// Every reader returns null rather than throwing when the option is absent or
/// is the wrong kind, because a command decides for itself what a missing
/// option means: some are required and some have a default, and only the
/// command knows which.
/// </para>
/// </summary>
public static class DiscordInteractionOptionUtils
{
    /// <summary>Reads an option Discord declares as a string.</summary>
    /// <param name="interaction">Interaction to read from.</param>
    /// <param name="name">Name of the option.</param>
    public static string? Text(Contracts.DiscordInteraction interaction, string name) =>
        Raw(interaction, name) is { ValueKind: JsonValueKind.String } option
            ? option.GetString()
            : null;

    /// <summary>Reads a numeric option that has to fit a count or an index.</summary>
    /// <param name="interaction">Interaction to read from.</param>
    /// <param name="name">Name of the option.</param>
    public static int? Number(Contracts.DiscordInteraction interaction, string name)
    {
        var value = Raw(interaction, name);
        return value is { ValueKind: JsonValueKind.Number } number
            && number.TryGetInt64(out var parsed)
            && parsed is >= int.MinValue and <= int.MaxValue
                ? (int)parsed
                : null;
    }

    /// <summary>Reads a numeric option that has to fit a count, with a floor and a ceiling.</summary>
    /// <param name="interaction">Interaction to read from.</param>
    /// <param name="name">Name of the option.</param>
    /// <param name="minimum">Smallest value accepted.</param>
    /// <param name="maximum">Largest value accepted.</param>
    public static int? BoundedNumber(
        Contracts.DiscordInteraction interaction,
        string name,
        int minimum,
        int maximum)
    {
        var value = Number(interaction, name);
        return value is not null && value >= minimum && value <= maximum ? value : null;
    }

    /// <summary>Reads an option Discord declares as a boolean.</summary>
    /// <param name="interaction">Interaction to read from.</param>
    /// <param name="name">Name of the option.</param>
    public static bool? Flag(Contracts.DiscordInteraction interaction, string name) =>
        Raw(interaction, name)?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    /// <summary>Reads an option as the JSON it arrived as.</summary>
    /// <param name="interaction">Interaction to read from.</param>
    /// <param name="name">Name of the option.</param>
    public static JsonElement? Raw(Contracts.DiscordInteraction interaction, string name) =>
        interaction.Data?.Options?
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                name,
                StringComparison.OrdinalIgnoreCase))?
            .Value;
}
