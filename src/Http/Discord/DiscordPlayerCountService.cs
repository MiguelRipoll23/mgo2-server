using System.Text.RegularExpressions;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Publishes the global player count of the deployment in a dedicated Discord
/// channel: the channel is created once, renamed whenever the count moves, and
/// every connection and disconnection is written in it as a message.
/// </summary>
/// <remarks>
/// It is an observer of the coordinator, so it needs the global count rather
/// than the count of one lobby, which is why the integration belongs to the
/// HTTP API. None of its failures reach the coordinator: a Discord that refuses
/// a rename or a message is logged and forgotten. A connection and its opposite
/// are held back for a short window, so a player who moves between lobbies does
/// not read as a departure followed by an arrival.
/// </remarks>
/// <param name="restClient">REST side of the integration, which owns the channel calls.</param>
/// <param name="presence">Counts the coordinator owns, read for the settled total.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class DiscordPlayerCountService(
    DiscordRestClientService restClient,
    LobbyPresenceService presence,
    IOptions<DiscordOptions> options,
    ILogger<DiscordPlayerCountService> logger) : IPlayerPresenceObserver
{
    private readonly DiscordOptions options = options.Value;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Lock pendingGate = new();
    private readonly Dictionary<int, PendingPresence> pending = [];
    private readonly SemaphoreSlim flushGate = new(1, 1);

    private string? channelIdentifier;
    private string? appliedName;
    private long pendingGenerations;

    /// <summary>Builds the name of the channel for a number of players.</summary>
    /// <param name="totalPlayers">Players connected across every lobby.</param>
    public static string FormatChannelName(int totalPlayers) => $"players [{totalPlayers}]";

    /// <summary>Finds the channel this integration manages, creating it when the guild has none.</summary>
    /// <param name="totalPlayers">Count the channel is named with.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task InitializeAsync(int totalPlayers, CancellationToken cancellationToken)
    {
        if (!options.IsPlayerCountConfigured)
        {
            logger.LogInformation(
                "The Discord player count is not published; DISCORD_BOT_TOKEN and DISCORD_GUILD_ID are required");
            return;
        }

        var name = FormatChannelName(totalPlayers);
        channelIdentifier = await ResolveChannelAsync(name, cancellationToken);

        if (channelIdentifier is null)
        {
            logger.LogWarning("The Discord player count channel is unavailable; the count is not published");
            return;
        }

        appliedName = await RenameAsync(channelIdentifier, name, cancellationToken) ? name : null;
        logger.LogInformation(
            "The Discord player count channel {ChannelIdentifier} publishes {TotalPlayers} players",
            channelIdentifier,
            totalPlayers);
    }

    /// <inheritdoc />
    public Task PlayerPresenceChangedAsync(
        PlayerPresenceNotification notification,
        CancellationToken cancellationToken)
    {
        long generation;
        lock (pendingGate)
        {
            if (!pending.TryGetValue(notification.CharacterIdentifier, out var entry))
            {
                entry = new PendingPresence();
                pending[notification.CharacterIdentifier] = entry;
            }

            // A connection and a disconnection of the same character that fall
            // inside one window are one move between lobbies, so they cancel
            // out and nothing is written.
            entry.Change += notification.Connected ? 1 : -1;
            entry.CharacterName = notification.CharacterName;

            generation = ++pendingGenerations;
            entry.Generation = generation;
        }

        // Not awaited: the coordinator reports a change rather than waiting for
        // the window to close.
        _ = FlushAfterWindowAsync(notification.CharacterIdentifier, generation);
        return Task.CompletedTask;
    }

    /// <summary>Writes one coalesced presence change once its window has closed.</summary>
    /// <param name="characterIdentifier">Character the change belongs to.</param>
    /// <param name="generation">Window the change was scheduled in.</param>
    private async Task FlushAfterWindowAsync(int characterIdentifier, long generation)
    {
        try
        {
            await Task.Delay(options.PresenceCoalesceMilliseconds);

            int change;
            string characterName;
            lock (pendingGate)
            {
                if (!pending.TryGetValue(characterIdentifier, out var entry) ||
                    entry.Generation != generation)
                {
                    return;
                }

                pending.Remove(characterIdentifier);
                change = entry.Change;
                characterName = entry.CharacterName;
            }

            if (change == 0)
            {
                return;
            }

            await flushGate.WaitAsync();
            try
            {
                await SendMessageAsync(
                    $"{characterName} {(change > 0 ? "connected" : "disconnected")}",
                    CancellationToken.None);

                // The settled total, so the dip of a move never reaches the
                // channel name.
                await PlayerTotalChangedAsync(presence.TotalPlayers, CancellationToken.None);
            }
            finally
            {
                flushGate.Release();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A Discord player presence change could not be published");
        }
    }

    /// <inheritdoc />
    public async Task PlayerTotalChangedAsync(int totalPlayers, CancellationToken cancellationToken)
    {
        if (channelIdentifier is null)
        {
            return;
        }

        var name = FormatChannelName(totalPlayers);
        if (name == appliedName)
        {
            return;
        }

        appliedName = await RenameAsync(channelIdentifier, name, cancellationToken) ? name : null;
    }

    /// <summary>
    /// Returns the channel the count is published in: the configured one, the
    /// one a previous run created, or a new one.
    /// </summary>
    /// <param name="name">Name the channel should carry.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<string?> ResolveChannelAsync(string name, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.PlayerCountChannelIdentifier))
        {
            return options.PlayerCountChannelIdentifier;
        }

        var channels = await restClient.ListGuildChannelsAsync(cancellationToken);
        if (channels is null)
        {
            return null;
        }

        // The channel is remembered by its name rather than by a row of the
        // database, so a restart adopts the one it created instead of adding a
        // second one.
        var existing = channels.FirstOrDefault(channel => IsPlayerCountChannelName(channel.Name));
        if (existing?.Identifier is { Length: > 0 } identifier)
        {
            return identifier;
        }

        logger.LogInformation("The guild holds no player count channel; creating one");
        return await restClient.CreateChannelAsync(name, cancellationToken);
    }

    /// <summary>Writes one message in the channel of the count.</summary>
    /// <param name="content">Text of the message.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task SendMessageAsync(string content, CancellationToken cancellationToken)
    {
        if (channelIdentifier is null)
        {
            return;
        }

        await restClient.SendChannelMessageAsync(channelIdentifier, content, cancellationToken);
    }

    /// <summary>
    /// Renames the channel, one call at a time. Discord limits how often a
    /// channel may be renamed, so the calls are serialized and a refusal only
    /// means the name is applied again at the next change.
    /// </summary>
    /// <param name="channel">Identifier of the channel.</param>
    /// <param name="name">Name to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<bool> RenameAsync(string channel, string name, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await restClient.RenameChannelAsync(channel, name, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reports whether a channel name is one this integration could have
    /// applied, so a channel it created before a restart is found again.
    /// </summary>
    /// <remarks>
    /// Discord stores a text channel name lowercased with its spaces as
    /// dashes, so the channel this integration created as
    /// <c>players [0]</c> is listed back as <c>players-[0]</c>; both spellings
    /// are accepted, or a restart would create a second channel.
    /// </remarks>
    /// <param name="name">Name of a channel.</param>
    private static bool IsPlayerCountChannelName(string? name) =>
        name is not null && PlayerCountChannelNamePattern().IsMatch(name);

    [GeneratedRegex(@"^players[ -]\[\d+\]$", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerCountChannelNamePattern();

    /// <summary>A presence change held back until its window closes.</summary>
    private sealed class PendingPresence
    {
        /// <summary>Connections minus disconnections seen inside the window.</summary>
        public int Change { get; set; }

        /// <summary>Name of the character, taken from the latest change.</summary>
        public string CharacterName { get; set; } = string.Empty;

        /// <summary>Window the latest change belongs to; an older flush is dropped.</summary>
        public long Generation { get; set; }
    }
}