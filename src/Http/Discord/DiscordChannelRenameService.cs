using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Renames one channel of the guild inside the limit Discord puts on the
/// endpoint: a channel may be renamed twice in ten minutes, and a name that
/// moves more often than that waits for the next of those two renames instead of
/// asking for a third and being refused.
/// </summary>
/// <remarks>
/// Asking for a name returns at once rather than waiting for Discord, so
/// whatever reported the change is not held up by a call to a third party. A
/// task of this service makes the call when the window allows it, carrying the
/// name that was asked for last, and a name Discord refuses is asked for again
/// rather than lost.
/// </remarks>
/// <param name="restClient">REST side of the integration, which owns the channel calls.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordChannelRenameService(
    DiscordRestClientService restClient,
    IOptions<DiscordOptions> options,
    ILogger<DiscordChannelRenameService> logger)
{
    /// <summary>
    /// Renames Discord allows a channel inside its window. They are spent
    /// rather than negotiated: a name that finds the window empty waits for the
    /// next one instead of being refused.
    /// </summary>
    private const int RenamesPerWindow = 2;

    /// <summary>Window the renames of the channel are counted in.</summary>
    private readonly TimeSpan renameWindow =
        TimeSpan.FromMilliseconds(options.Value.RenameWindowMilliseconds);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Lock stateGate = new();

    private string? channelIdentifier;
    private string? appliedName;

    /// <summary>Name waiting for a rename, which the last change decides.</summary>
    private string? wantedName;

    /// <summary>Whether a task is waiting out the window to rename the channel.</summary>
    private bool renaming;

    /// <summary>Renames spent since the window opened.</summary>
    private int spentRenames;

    /// <summary>When the window of the spent renames opened.</summary>
    private DateTimeOffset windowOpenedAt = DateTimeOffset.MinValue;

    /// <summary>Adopts the channel every name of this service applies to.</summary>
    /// <param name="identifier">Identifier of the channel.</param>
    public void Adopt(string identifier)
    {
        lock (stateGate)
        {
            channelIdentifier = identifier;
            appliedName = null;
        }
    }

    /// <summary>
    /// Renames the channel at once, which is what the start of a deployment
    /// does: its window is one nobody has spent a rename of yet.
    /// </summary>
    /// <param name="name">Name to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the name.</returns>
    public async Task<bool> ApplyAsync(string name, CancellationToken cancellationToken)
    {
        lock (stateGate)
        {
            SpendRenameLocked();
        }

        return await RenameAsync(name, cancellationToken);
    }

    /// <summary>
    /// Asks for the channel to carry a name. The name that arrives last is the
    /// one the channel ends up with: a name already waiting for its window is
    /// replaced, and a name that came back to the one the channel already
    /// carries cancels the one that was waiting.
    /// </summary>
    /// <param name="name">Name to apply.</param>
    public void Request(string name)
    {
        lock (stateGate)
        {
            if (channelIdentifier is null || name == appliedName)
            {
                wantedName = null;
                return;
            }

            wantedName = name;
            if (renaming)
            {
                return;
            }

            renaming = true;
        }

        // Not awaited: the caller asked for the name, not for the call.
        _ = PublishLatestNameAsync();
    }

    /// <summary>
    /// Renames the channel to the name asked for last, as soon as Discord allows
    /// another rename, and ends once the channel carries that name.
    /// </summary>
    private async Task PublishLatestNameAsync()
    {
        try
        {
            while (true)
            {
                string? name = null;
                TimeSpan wait;

                lock (stateGate)
                {
                    if (wantedName is null || wantedName == appliedName)
                    {
                        // The flag is cleared inside the lock, so a name that
                        // arrives as this task ends starts the next one.
                        renaming = false;
                        return;
                    }

                    if (TrySpendRenameLocked(out wait))
                    {
                        name = wantedName;
                        wantedName = null;
                    }
                }

                if (name is null)
                {
                    // The window is spent, so the name waits for the next one
                    // rather than being refused by Discord.
                    await Task.Delay(wait);
                    continue;
                }

                if (!await RenameAsync(name, CancellationToken.None))
                {
                    // Discord left the channel with the name it had, so this one
                    // is asked for again instead of being lost.
                    lock (stateGate)
                    {
                        wantedName ??= name;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            lock (stateGate)
            {
                renaming = false;
            }

            logger.LogError(exception, "The Discord channel could not be renamed to {ChannelName}", channelIdentifier);
        }
    }

    /// <summary>
    /// Spends one of the renames Discord allows a channel in its window, or
    /// reports how long the next one is. The limit belongs to the endpoint, so a
    /// name that finds the window spent waits for it to open again.
    /// </summary>
    /// <param name="wait">How long until the next rename is allowed.</param>
    /// <returns>Whether a rename is allowed now.</returns>
    private bool TrySpendRenameLocked(out TimeSpan wait)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - windowOpenedAt >= renameWindow)
        {
            windowOpenedAt = now;
            spentRenames = 0;
        }

        if (spentRenames < RenamesPerWindow)
        {
            spentRenames++;
            wait = TimeSpan.Zero;
            return true;
        }

        wait = renameWindow - (now - windowOpenedAt);
        return false;
    }

    /// <summary>Spends one rename of the window without asking for a name.</summary>
    private void SpendRenameLocked() => TrySpendRenameLocked(out _);

    /// <summary>
    /// Renames the channel, one call at a time, and records the name Discord
    /// accepted, so a name the channel already carries is not asked for again. A
    /// refusal leaves the name unrecorded, so it is asked for at the next change
    /// or at the next window.
    /// </summary>
    /// <param name="name">Name to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the name.</returns>
    private async Task<bool> RenameAsync(string name, CancellationToken cancellationToken)
    {
        var channel = channelIdentifier;
        if (channel is null)
        {
            return false;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var applied = await restClient.RenameChannelAsync(channel, name, cancellationToken);
            if (applied)
            {
                lock (stateGate)
                {
                    appliedName = name;
                }
            }

            return applied;
        }
        finally
        {
            gate.Release();
        }
    }
}
