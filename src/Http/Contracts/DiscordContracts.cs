using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// One interaction Discord delivers to the registered endpoint: a ping that
/// proves the endpoint is alive, or a command a member used.
/// </summary>
public sealed class DiscordInteraction
{
    /// <summary>Kind of interaction.</summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>Identifier of the interaction, echoed nowhere but the reply.</summary>
    [JsonPropertyName("id")]
    public string? Identifier { get; set; }

    /// <summary>Token the reply is sent with instead of a bot token.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>Guild the interaction happened in, when it happened in one.</summary>
    [JsonPropertyName("guild_id")]
    public string? GuildIdentifier { get; set; }

    /// <summary>Member that used the command, when it is a guild interaction.</summary>
    [JsonPropertyName("member")]
    public DiscordInteractionMember? Member { get; set; }

    /// <summary>Command and the options it was used with.</summary>
    [JsonPropertyName("data")]
    public DiscordApplicationCommandData? Data { get; set; }
}

/// <summary>Member of a guild that used a command.</summary>
public sealed class DiscordInteractionMember
{
    /// <summary>Roles the member holds.</summary>
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = [];
}

/// <summary>The command of an application-command interaction.</summary>
public sealed class DiscordApplicationCommandData
{
    /// <summary>Name the command was registered with.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Options the command was used with.</summary>
    [JsonPropertyName("options")]
    public List<DiscordApplicationCommandOption>? Options { get; set; }
}

/// <summary>One option of a command.</summary>
public sealed class DiscordApplicationCommandOption
{
    /// <summary>Name the option was registered with.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Value the member supplied. It is read from the element rather than
    /// bound, because an option carries whatever type it was registered with.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}

/// <summary>Channel of a guild, as the channel endpoints report it.</summary>
public sealed class DiscordChannel
{
    /// <summary>Identifier of the channel.</summary>
    [JsonPropertyName("id")]
    public string? Identifier { get; set; }

    /// <summary>Name of the channel.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>Reply of an interaction, sent within the three seconds Discord waits.</summary>
/// <param name="Type">Kind of reply.</param>
/// <param name="Data">Content of the reply, when it carries any.</param>
public sealed record DiscordInteractionResponse(
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("data")] DiscordInteractionMessage? Data = null);

/// <summary>Message carried by a reply.</summary>
/// <param name="Content">Text of the message.</param>
/// <param name="Flags">Message flags; the ephemeral flag hides it from everyone else.</param>
public sealed record DiscordInteractionMessage(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("flags")] int? Flags = null);

/// <summary>Outcome of one interaction, as the endpoint answers it.</summary>
/// <param name="StatusCode">Status the endpoint responds with.</param>
/// <param name="Response">Reply Discord is given, when there is one.</param>
public sealed record DiscordInteractionResult(
    int StatusCode,
    DiscordInteractionResponse? Response = null);
