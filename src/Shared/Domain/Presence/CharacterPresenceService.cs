using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Presence;

/// <summary>
/// Which lobby each character is connected to, right now.
/// <para>
/// Every lobby runs its own process and the sessions it holds are its own, so a
/// roster, a search result or a clan list that lists a player connected
/// elsewhere has no answer to give. This is the shared answer: the row is
/// written when a character enters a lobby and removed when it leaves.
/// </para>
/// <para>
/// <b>The rows are a claim about a live connection, not durable state.</b>
/// Losing one costs a reconnect and nothing else, which is why the recovery
/// below is deliberately blunt: a process clears its own lobby's rows at
/// startup, where a timeout would only reach the same answer by waiting.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class CharacterPresenceService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
{
    /// <summary>
    /// How long a row may go untouched before the sweep removes it. This only
    /// catches a process that died and never came back — one that restarts
    /// clears its own rows at boot — so it does not need to be tight, and must
    /// not be: a tight bound evicts live players during a pause long enough to
    /// skip a beat or two.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How often the heartbeat runs. Four beats fit inside <see cref="StaleAfter"/>,
    /// so three consecutive misses are needed before a live player is evicted.
    /// </summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Records that a character is now in a lobby.
    /// <para>
    /// An upsert, and the half of the lobby-hop race that claims rather than
    /// releases: a hop is two processes racing, the destination recording the
    /// arrival against the origin recording the departure, in either order.
    /// The insert claims the character unconditionally, so the destination wins
    /// whatever the order; <see cref="LeaveAsync"/> is the side that checks who
    /// owns the row.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Character that entered.</param>
    /// <param name="lobbyIdentifier">Lobby it entered.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task EnterAsync(
        int characterIdentifier,
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // A read followed by an insert or an update would let two processes both
        // find no row and both insert, so the upsert is stated to the database
        // instead. `since` moves on a hop because it means "entered this lobby",
        // not "came online".
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            insert into character_presence (character_id, lobby_id, since, last_seen)
            values ({characterIdentifier}, {lobbyIdentifier}, now(), now())
            on conflict (character_id) do update
                set lobby_id = excluded.lobby_id, since = now(), last_seen = now()
            """,
            cancellationToken);
    }

    /// <summary>
    /// Removes a character's presence, but only while this lobby still owns it.
    /// <para>
    /// The ownership test is the whole point of the method and must not be
    /// dropped as redundant. A disconnect from the origin lobby can be
    /// processed after the destination has recorded the arrival; without the
    /// test that late delete erases a presence that is currently correct, and
    /// the player disappears from every list in the game until they reconnect.
    /// The symptom depends on process timing, which is what makes it hard to
    /// reproduce and easy to misattribute.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Character that left.</param>
    /// <param name="lobbyIdentifier">Lobby it is leaving.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether a row was removed, which is false when the character had already moved on.</returns>
    public async Task<bool> LeaveAsync(
        int characterIdentifier,
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var removed = await context.CharacterPresence
            .Where(presence =>
                presence.CharacterIdentifier == characterIdentifier &&
                presence.LobbyIdentifier == lobbyIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    /// <summary>
    /// Drops every row of a lobby, for that lobby's process to call at startup.
    /// <para>
    /// Unconditionally correct, because nobody is connected to a process that
    /// has just started. That is what makes it better than trusting the sweep
    /// for the ordinary failure: a hard kill, a restart or a crash loop
    /// recovers exactly and immediately, where a timeout would leave every one
    /// of those players listed as present until it expired.
    /// </para>
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby whose rows are cleared.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>
    /// How many rows were cleared, which is worth logging: a count above zero
    /// after a clean shutdown means departures were not being processed.
    /// </returns>
    public async Task<int> ClearLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CharacterPresence
            .Where(presence => presence.LobbyIdentifier == lobbyIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Marks the given characters as still connected, in one statement.
    /// <para>
    /// One round trip per beat however many players the lobby holds. A character
    /// whose row was swept is not resurrected here — the update touches rows
    /// that exist and creates nothing — because resurrecting one would hide a
    /// sweep that is too aggressive instead of surfacing it.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifiers">Characters that are connected.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many rows were touched; fewer than were asked for means rows went missing.</returns>
    public async Task<int> HeartbeatAsync(
        IReadOnlyCollection<int> characterIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifiers.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        // Copied into a list so the membership test is one the provider translates
        // directly, rather than an interface's extension method whose translation is
        // left to the query pipeline.
        List<int> identifiers = [.. characterIdentifiers];
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CharacterPresence
            .Where(presence => identifiers.Contains(presence.CharacterIdentifier))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(presence => presence.LastSeen, now),
                cancellationToken);
    }

    /// <summary>
    /// Removes rows that have gone untouched for longer than <see cref="StaleAfter"/>.
    /// <para>
    /// Scoped to no lobby on purpose: the point is to clean up after a process
    /// that is not running, so a surviving process has to do it on its behalf.
    /// Every lobby runs it and the delete is idempotent, so whichever gets there
    /// first wins and the rest are no-ops.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many stale rows were removed.</returns>
    public async Task<int> ReapStaleAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - StaleAfter;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CharacterPresence
            .Where(presence => presence.LastSeen < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Where each of the given characters is, omitting those who are not
    /// connected anywhere.
    /// <para>
    /// The bulk form exists because every consumer is a list screen — a roster,
    /// a search result, a clan — and asking per row would be one query per entry
    /// for up to a hundred entries. It is two statements per batch rather than
    /// one join: which lobby is presence, which room is the room roster, and
    /// saying each plainly beats one statement with two left joins whose
    /// translation is harder to be sure of than the result.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifiers">Characters to look up.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IReadOnlyDictionary<int, CharacterLocation>> FindLocationsAsync(
        IReadOnlyCollection<int> characterIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifiers.Count == 0)
        {
            return new Dictionary<int, CharacterLocation>();
        }

        // Copied into a list so the membership test is one the provider translates
        // directly, as above.
        List<int> identifiers = [.. characterIdentifiers];
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var presences = await context.CharacterPresence
            .AsNoTracking()
            .Where(presence => identifiers.Contains(presence.CharacterIdentifier))
            .Join(
                context.Lobbies,
                presence => presence.LobbyIdentifier,
                lobby => lobby.Identifier,
                (presence, lobby) => new
                {
                    presence.CharacterIdentifier,
                    presence.LobbyIdentifier,
                    lobby.Name,
                    lobby.SubtypeIdentifier,
                })
            .ToListAsync(cancellationToken);

        var rooms = await context.GamePlayers
            .AsNoTracking()
            .Where(player => identifiers.Contains(player.CharacterIdentifier))
            .Join(
                context.Games,
                player => player.GameIdentifier,
                game => game.Identifier,
                (player, game) => new
                {
                    player.CharacterIdentifier,
                    game.Identifier,
                    game.Name,
                    game.LobbyIdentifier,
                })
            .ToListAsync(cancellationToken);

        var locations = new Dictionary<int, CharacterLocation>();
        foreach (var presence in presences)
        {
            // A room counts only while it belongs to the lobby the character is
            // in: a row from a lobby they have left is not where they are. Two
            // rows cannot happen by design, but the roster is cleaned up on
            // leave rather than constrained to one, so keeping the first is
            // arbitrary and deterministic rather than throwing over one row.
            var room = rooms
                .Where(candidate =>
                    candidate.CharacterIdentifier == presence.CharacterIdentifier &&
                    candidate.LobbyIdentifier == presence.LobbyIdentifier)
                .OrderBy(candidate => candidate.Identifier)
                .FirstOrDefault();

            locations[presence.CharacterIdentifier] = new CharacterLocation(
                presence.LobbyIdentifier,
                presence.Name,
                presence.SubtypeIdentifier,
                room?.Identifier ?? 0,
                room?.Name ?? string.Empty);
        }

        return locations;
    }

    /// <summary>How many characters are recorded in a lobby.</summary>
    /// <param name="lobbyIdentifier">Lobby to count.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> CountInLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CharacterPresence
            .CountAsync(presence => presence.LobbyIdentifier == lobbyIdentifier, cancellationToken);
    }
}
