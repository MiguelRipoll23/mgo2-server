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

    /// <summary>Whether the integration runs at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Bot token of the application, used to call the REST API.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Identifier of the application, used to register its commands.</summary>
    public string ApplicationIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Hex-encoded Ed25519 public key of the application, used to verify the
    /// signature Discord puts on every interaction it delivers.
    /// </summary>
    public string ApplicationPublicKey { get; set; } = string.Empty;

    /// <summary>Identifier of the guild the commands and the channel belong to.</summary>
    public string GuildIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the channel the player count is published in. Left empty,
    /// the channel is found by its name in the guild and created when the guild
    /// has none.
    /// </summary>
    public string PlayerCountChannelIdentifier { get; set; } = string.Empty;

    /// <summary>Identifier of the role that may use the flash command.</summary>
    public string ModeratorRoleIdentifier { get; set; } = string.Empty;

    /// <summary>Identifier of the other role that may use the flash command.</summary>
    public string ManagerRoleIdentifier { get; set; } = string.Empty;

    /// <summary>Base URL of the Discord REST API.</summary>
    public string ApiBaseUrl { get; set; } = "https://discord.com/api/v10";

    /// <summary>
    /// User agent every call is sent with. Discord refuses a call that does not
    /// identify itself.
    /// </summary>
    public string UserAgent { get; set; } =
        "mgo2-server (https://github.com/MiguelRipoll23/mgo2-server, 1.0.0)";

    /// <summary>How long one call to the Discord API may take before it is abandoned.</summary>
    public int RequestTimeoutMilliseconds { get; set; } = 10000;

    /// <summary>Whether the integration can answer interactions.</summary>
    public bool IsInteractionConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BotToken) &&
        !string.IsNullOrWhiteSpace(ApplicationIdentifier) &&
        !string.IsNullOrWhiteSpace(ApplicationPublicKey);

    /// <summary>Whether the integration can publish the player count.</summary>
    public bool IsPlayerCountConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(GuildIdentifier);

    /// <summary>
    /// Reports whether the roles of an interaction user allow the flash
    /// command. An unconfigured role allows nobody, so a deployment that forgot
    /// to name its staff roles does not hand the command to everyone.
    /// </summary>
    /// <param name="roleIdentifiers">Roles of the user that used the command.</param>
    public bool AllowsFlashCommand(IEnumerable<string> roleIdentifiers)
    {
        var allowed = new[] { ModeratorRoleIdentifier, ManagerRoleIdentifier }
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .ToHashSet(StringComparer.Ordinal);

        return allowed.Count > 0 &&
            roleIdentifiers.Any(role => allowed.Contains(role));
    }
}
