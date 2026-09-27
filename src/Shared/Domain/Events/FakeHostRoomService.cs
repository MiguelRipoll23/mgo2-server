using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why a dedicated event host room could not be created.</summary>
public enum FakeHostRoomOutcome
{
    /// <summary>The room was created.</summary>
    Created,

    /// <summary>The mode is not one an event host room exists for.</summary>
    NotAnEventHost,

    /// <summary>No character was available to host the room.</summary>
    NoHostCharacter,
}

/// <summary>What creating a dedicated event host room did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Room">The room, when one was made.</param>
/// <param name="HostIdentifier">Character hosting the room, when one was made.</param>
public readonly record struct FakeHostRoomResult(
    FakeHostRoomOutcome Outcome,
    Game? Room,
    int HostIdentifier);

/// <summary>
/// Creates the dedicated event host room a pairing needs, as an ordinary row in
/// the games table rather than as anything held in memory.
/// <para>
/// A pairing does not announce itself. The match row is written when two teams
/// are queued, but the match-found packet is only pushed once a room has been
/// claimed, and a room is only claimed from one that is named for the role, says
/// it is dedicated, and is sitting idle with its host present. All three of
/// those are things a real host client does merely by existing, so a pairing
/// made from the testing tools sat in "paired, waiting for a host" until
/// somebody opened a dedicated room by hand — which reads from the page as a
/// pairing that never worked, when in fact it paired correctly and simply had
/// nowhere to go.
/// </para>
/// <para>
/// So this opens the room the way a host client would: it writes a real games
/// row, with the host in its roster. The eligibility rule, the lease and the
/// assignment packets then read one kind of room, because there is only one kind
/// now — a row. A room that only half-existed, or one the process forgot on a
/// restart, was a pairing waiting forever; a row outlives the process that made
/// it.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="gameService">Service that owns the rooms.</param>
public sealed class FakeHostRoomService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    GameService gameService)
    : DomainService(contextFactory)
{
    /// <summary>
    /// The name a room carries to host a mode, or null when the mode is not one
    /// an event host room exists for.
    /// </summary>
    /// <param name="mode">Mode of the lobby the room would host.</param>
    public static string? RoomNameFor(int mode) => mode switch
    {
        EventConstants.SurvivalSelector => EventHostEligibilityUtils.SurvivalHostName,
        EventConstants.TournamentSelector => EventHostEligibilityUtils.TournamentHostName,
        _ => null,
    };

    /// <summary>
    /// Creates a dedicated host room of a mode in a lobby, as a real row.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="mode">Mode the room hosts.</param>
    /// <param name="hostCharacterIdentifier">Character that hosts the room, or zero to pick one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeHostRoomResult> CreateAsync(
        int lobbyIdentifier,
        int mode,
        int hostCharacterIdentifier = 0,
        CancellationToken cancellationToken = default)
    {
        var hostName = RoomNameFor(mode);
        if (hostName is null)
        {
            return new FakeHostRoomResult(FakeHostRoomOutcome.NotAnEventHost, null, 0);
        }

        var host = hostCharacterIdentifier > 0
            ? await FindHostAsync(hostCharacterIdentifier, cancellationToken)
            : await FindFirstHostAsync(cancellationToken);

        if (host <= 0)
        {
            return new FakeHostRoomResult(FakeHostRoomOutcome.NoHostCharacter, null, 0);
        }

        var now = DateTimeOffset.UtcNow;
        var room = await gameService.CreateAsync(
            game =>
            {
                game.HostIdentifier = host;
                game.LobbyIdentifier = lobbyIdentifier;
                game.Name = hostName;

                // The room is sized for the largest match the eligibility rule
                // accepts plus the dedicated host's own slot. A room at a
                // player's usual eight would pass the capacity check and then
                // find nowhere to seat anybody.
                game.MaximumPlayers =
                    EventHostEligibilityUtils.MatchPlayerCapacity
                    + EventHostEligibilityUtils.DedicatedHostPlayerSlots;
                game.Comment = "Created by the event testing tools.";

                // The flag the host-eligibility rule reads. The room is dedicated
                // to one mode, which is what makes it eligible for that mode's
                // matches and nothing else.
                game.Common = """{"dedicated":true}""";

                // Freshly stamped, because the room list only publishes rooms
                // whose host has written to them recently, and a row that was
                // created but never stamped would be invisible to the very sweep
                // it exists to feed.
                game.CreatedAt = now;
                game.UpdatedAt = now;

                // The sweep requires the host to be present in the room, and the
                // roster is what says so. A character at or below zero is not a
                // host at all: the roster spells zero as "nobody here", so a row
                // carrying it would claim a host is present in an empty room and
                // the idle rule would read that room as ready.
                game.Players.Add(new GamePlayer
                {
                    CharacterIdentifier = host,
                    JoinedAt = now,
                });
            },
            cancellationToken);

        return new FakeHostRoomResult(FakeHostRoomOutcome.Created, room, host);
    }

    private async Task<int> FindHostAsync(
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        // A named host has to exist, because a character that is not there is a
        // host the room cannot say is present, and an absent host is a room that
        // is never idle — which is a pairing that never reaches a host.
        var exists = await context.Characters
            .AnyAsync(
                character => character.Identifier == characterIdentifier
                    && character.Identifier > 0,
                cancellationToken);
        return exists ? characterIdentifier : 0;
    }

    private async Task<int> FindFirstHostAsync(CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        // The lowest identifier that can actually host a room, which is the
        // lowest one above zero. Zero is excluded deliberately: it is how a
        // roster spells "nobody here", so a host of zero would be
        // indistinguishable from an absent one and the room would be created
        // and then never eligible.
        return await context.Characters
            .Where(character => character.Identifier > 0)
            .OrderBy(character => character.Identifier)
            .Select(character => character.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
