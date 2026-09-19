using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// One frame of the Discord gateway protocol. Every frame carries an operation
/// code; a dispatch (opcode 0) also carries the event name and its data.
/// </summary>
public sealed class GatewayFrame
{
    /// <summary>Operation code of the frame.</summary>
    [JsonPropertyName("op")]
    public int OpCode { get; set; }

    /// <summary>Sequence number of a dispatch, echoed in the heartbeats.</summary>
    [JsonPropertyName("s")]
    public int? Sequence { get; set; }

    /// <summary>Name of a dispatched event.</summary>
    [JsonPropertyName("t")]
    public string? EventName { get; set; }

    /// <summary>Event data, read according to the operation code.</summary>
    [JsonPropertyName("d")]
    public JsonElement? Data { get; set; }
}

/// <summary>Hello the gateway opens every connection with.</summary>
/// <param name="HeartbeatInterval">Interval in milliseconds between heartbeats.</param>
public sealed record DiscordGatewayHello(
    [property: JsonPropertyName("heartbeat_interval")] int HeartbeatInterval);

/// <summary>Heartbeat the client sends within the interval the hello set.</summary>
/// <param name="OpCode">Operation code of the heartbeat.</param>
/// <param name="Sequence">Sequence of the last frame received, or <c>null</c>.</param>
public sealed record DiscordGatewayHeartbeat(
    [property: JsonPropertyName("op")] int OpCode = 1,
    [property: JsonPropertyName("d")] int? Sequence = null);

/// <summary>Request the client sends to the gateway besides a heartbeat.</summary>
/// <param name="OpCode">Operation code of the request.</param>
/// <param name="Data">Payload of the request.</param>
public sealed record DiscordGatewayRequest(
    [property: JsonPropertyName("op")] int OpCode,
    [property: JsonPropertyName("d")] object Data);

/// <summary>Payload of the identify request, which opens a session.</summary>
/// <param name="Token">Bot token of the application.</param>
/// <param name="Intents">Intents the events are subscribed with.</param>
/// <param name="Properties">Connection metadata Discord asks every identify to carry.</param>
public sealed record DiscordIdentifyData(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("intents")] int Intents,
    [property: JsonPropertyName("properties")] DiscordConnectionProperties Properties);

/// <summary>Connection metadata of a gateway session.</summary>
/// <param name="OperatingSystem">Operating system the bot runs on.</param>
/// <param name="Browser">Browser value Discord records for the session.</param>
/// <param name="Device">Device value Discord records for the session.</param>
public sealed record DiscordConnectionProperties(
    [property: JsonPropertyName("os")] string OperatingSystem,
    [property: JsonPropertyName("browser")] string Browser,
    [property: JsonPropertyName("device")] string Device);

/// <summary>Payload of the resume request, sent over a session that was dropped.</summary>
/// <param name="Token">Bot token of the application.</param>
/// <param name="SessionIdentifier">Identifier of the session to resume.</param>
/// <param name="Sequence">Last sequence number of the dropped session.</param>
public sealed record DiscordResumeData(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("session_id")] string SessionIdentifier,
    [property: JsonPropertyName("seq")] int Sequence);

/// <summary>Data of the ready event, which names the session that was opened.</summary>
/// <param name="SessionIdentifier">Identifier of the session.</param>
/// <param name="ResumeGatewayUrl">URL the session is resumed over after a disconnect.</param>
public sealed record DiscordReadyData(
    [property: JsonPropertyName("session_id")] string SessionIdentifier,
    [property: JsonPropertyName("resume_gateway_url")] string? ResumeGatewayUrl = null);

/// <summary>
/// One interaction the gateway delivers: a slash command a member of a guild
/// used.
/// </summary>
public sealed class DiscordInteraction
{
    /// <summary>Identifier of the interaction.</summary>
    [JsonPropertyName("id")]
    public string? Identifier { get; set; }

    /// <summary>Token that authorizes the callback that answers the interaction.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>Kind of interaction.</summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>Guild the interaction happened in.</summary>
    [JsonPropertyName("guild_id")]
    public string? GuildIdentifier { get; set; }

    /// <summary>Channel the interaction happened in, where the reply is written.</summary>
    [JsonPropertyName("channel_id")]
    public string? ChannelIdentifier { get; set; }

    /// <summary>Member that used the command.</summary>
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

/// <summary>Body of the channel message the bot sends over the REST API.</summary>
/// <param name="Content">Text of the message.</param>
/// <param name="AllowedMentions">Mentions the message is allowed to trigger.</param>
public sealed record DiscordChannelMessageBody(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("allowed_mentions")] DiscordAllowedMentions AllowedMentions);

/// <summary>Mentions a bot-written message may trigger.</summary>
/// <param name="Parse">Kinds of mention the message is allowed to parse.</param>
public sealed record DiscordAllowedMentions(
    [property: JsonPropertyName("parse")] List<string> Parse);

/// <summary>Body of the callback that answers an interaction.</summary>
/// <param name="Type">Kind of response.</param>
/// <param name="Data">Content the response carries.</param>
public sealed record DiscordInteractionResponse(
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("data")] DiscordInteractionResponseData Data);

/// <summary>Message an interaction response carries.</summary>
/// <param name="Content">Text of the message.</param>
/// <param name="Flags">Flags of the message, such as ephemeral.</param>
public sealed record DiscordInteractionResponseData(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("flags")] int Flags);

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

/// <summary>Body of the call that creates a channel.</summary>
/// <param name="Name">Name the channel is created with.</param>
/// <param name="Type">Channel type of the created channel.</param>
public sealed record DiscordChannelCreateBody(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] int Type);

/// <summary>Body of the call that renames a channel.</summary>
/// <remarks>
/// It carries the name only. The type of the modify endpoint converts a text
/// channel into an announcement one, and any other value Discord refuses.
/// </remarks>
/// <param name="Name">Name to give the channel.</param>
public sealed record DiscordChannelEditBody([property: JsonPropertyName("name")] string Name);

/// <summary>Request the API accepts to send a message from the bot.</summary>
public sealed class DiscordMessageSendRequest
{
    /// <summary>Channel the message is written in.</summary>
    [Required]
    public required string ChannelId { get; set; }

    /// <summary>Text of the message.</summary>
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Content { get; set; }
}