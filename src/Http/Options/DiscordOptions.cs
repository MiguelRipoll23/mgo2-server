namespace Mgo2Server.Http.Options;

/// <summary>
/// Options of the Discord integration. They are read from the flat upper-case
/// environment variables named in the setup documentation, and the whole
/// integration stays switched off until <see cref="Enabled"/> asks for it.
/// </summary>
/// <remarks>
/// Discord never becomes a requirement of the deployment: a Discord that is
/// unreachable, misconfigured or switched off leaves the HTTP API and every
/// lobby running exactly as they run without it.
/// </remarks>
public sealed class DiscordOptions
{
    /// <summary>Name of the command the moderators broadcast flash news with.</summary>
    public const string FlashCommandName = "flash";

    /// <summary>Name of the command the moderators send an official message with.</summary>
    public const string MessageCommandName = "message";

    /// <summary>Name of the command the moderators schedule events with.</summary>
    public const string ScheduleCommandName = "event";

    /// <summary>Name of the command the moderators fill an event with.</summary>
    public const string FakePlayerCommandName = "fake-player";

    /// <summary>Gateway the client connects to by default.</summary>
    public const string DefaultGatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";

    /// <summary>
    /// Intents the gateway session is identified with. The guilds intent is the
    /// one the slash commands and the channel of a guild arrive under.
    /// </summary>
    public const int GatewayIntents = 1 << 0;

    /// <summary>Whether the integration runs at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Bot token of the application, used to identify on the gateway.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>WebSocket URL of the gateway the bot connects to.</summary>
    public string GatewayUrl { get; set; } = DefaultGatewayUrl;

    /// <summary>
    /// Identifier of the guild the commands belong to. Left empty, every guild
    /// the bot is in may use the staff commands.
    /// </summary>
    public string GuildIdentifier { get; set; } = string.Empty;

    /// <summary>Identifier of the role that may use the staff commands.</summary>
    public string ModeratorRoleIdentifier { get; set; } = string.Empty;

    /// <summary>Identifier of the other role that may use the staff commands.</summary>
    public string ManagerRoleIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the channel the player count is published in. Left empty,
    /// the channel is found by its name in the guild and created when the guild
    /// has none.
    /// </summary>
    public string PlayerCountChannelIdentifier { get; set; } = string.Empty;

    /// <summary>Base URL of the Discord REST API, used for the one call the integration makes.</summary>
    public string ApiBaseUrl { get; set; } = "https://discord.com/api/v10";

    /// <summary>
    /// User agent every call is sent with. Discord refuses a call that does not
    /// identify itself.
    /// </summary>
    public string UserAgent { get; set; } =
        "mgo2-server (https://github.com/MiguelRipoll23/mgo2-server, 1.0.0)";

    /// <summary>How long one call to the Discord API may take before it is abandoned.</summary>
    public int RequestTimeoutMilliseconds { get; set; } = 10000;

    /// <summary>
    /// How long a connection or disconnection is held back before it is written
    /// in the player count channel. A player who moves from one lobby to
    /// another reports both a departure and an arrival in quick succession; the
    /// window lets the two cancel out instead of cluttering the channel.
    /// </summary>
    public int PresenceCoalesceMilliseconds { get; set; } = 10000;

    /// <summary>Whether the integration can reach Discord at all.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BotToken);

    /// <summary>Whether the integration can publish the player count.</summary>
    public bool IsPlayerCountConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(GuildIdentifier);

    /// <summary>
    /// Reports whether the roles of a command user allow the staff commands
    /// (the flash and the official message). An unconfigured role allows
    /// nobody, so a deployment that forgot to name its staff roles does not
    /// hand the commands to everyone.
    /// </summary>
    /// <param name="roleIdentifiers">Roles of the user that used the command.</param>
    public bool AllowsStaffCommand(IEnumerable<string> roleIdentifiers)
    {
        var allowed = new[] { ModeratorRoleIdentifier, ManagerRoleIdentifier }
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .ToHashSet(StringComparer.Ordinal);

        return allowed.Count > 0 &&
            roleIdentifiers.Any(role => allowed.Contains(role));
    }
}
