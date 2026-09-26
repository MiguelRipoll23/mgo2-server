using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why a fake host room could not be created.</summary>
public enum FakeHostRoomOutcome
{
    /// <summary>The room was created.</summary>
    Created,

    /// <summary>The mode is not one an event host room exists for.</summary>
    NotAnEventHost,

    /// <summary>No character was available to host the room.</summary>
    NoHostCharacter,
}

/// <summary>What creating a fake host room did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Room">The room, when one was made.</param>
public readonly record struct FakeHostRoomResult(
    FakeHostRoomOutcome Outcome,
    FakeHostRoom? Room);

/// <summary>
/// Holds the dedicated event host rooms that exist only in this lobby's memory,
/// so a pairing can be given somewhere to go without a client sitting in a game
/// to make one.
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
/// So this makes the room in memory, beside the real ones rather than instead of
/// them. The rooms here are projected into the same <see cref="Game"/> a real
/// room is, so everything downstream — the eligibility rule, the claim and the
/// assignment packets — reads one kind of room and cannot tell them apart. The
/// rooms a player really opens keep working exactly as they did; these simply
/// join them in the pool.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class FakeHostRoomService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, FakeHostRoom> rooms = [];
    private int nextGameIdentifier = FakePlayerIdentifierUtils.FirstFakeIdentifier;

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
    /// The in-memory host rooms of a lobby, in the order they were created.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    public IReadOnlyList<FakeHostRoom> ListRooms(int lobbyIdentifier)
    {
        lock (gate)
        {
            return [.. rooms.Values
                .Where(room => room.Room.LobbyIdentifier == lobbyIdentifier)
                .OrderBy(room => room.Room.Identifier)];
        }
    }

    /// <summary>Returns one in-memory room, when it is held here.</summary>
    /// <param name="gameIdentifier">Room to find.</param>
    public FakeHostRoom? FindRoom(int gameIdentifier)
    {
        lock (gate)
        {
            return rooms.TryGetValue(gameIdentifier, out var room) ? room : null;
        }
    }

    /// <summary>
    /// Creates a dedicated host room of a mode in a lobby.
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
            return new FakeHostRoomResult(FakeHostRoomOutcome.NotAnEventHost, null);
        }

        var host = hostCharacterIdentifier > 0
            ? await FindHostAsync(hostCharacterIdentifier, cancellationToken)
            : await FindFirstHostAsync(cancellationToken);

        if (host <= 0)
        {
            return new FakeHostRoomResult(FakeHostRoomOutcome.NoHostCharacter, null);
        }

        // The identifier comes from the fake range, so it can never collide with
        // a real room's — a games identifier is a database sequence and will not
        // reach a billion — and so a claim naming one is recognisable as a claim
        // on a room that exists in this process only.
        var room = FakeHostRoom.Create(
            NextGameIdentifier(),
            lobbyIdentifier,
            mode,
            host,
            hostName);

        lock (gate)
        {
            rooms[room.Room.Identifier] = room;
        }

        return new FakeHostRoomResult(FakeHostRoomOutcome.Created, room);
    }

    /// <summary>Forgets every in-memory room. For tests only.</summary>
    public void Reset()
    {
        lock (gate)
        {
            rooms.Clear();
        }
    }

    private int NextGameIdentifier()
    {
        lock (gate)
        {
            return nextGameIdentifier++;
        }
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
        // roster spells "nobody here", so a host of zero is indistinguishable
        // from an absent one and the room would be created and then never
        // eligible. Skipping it is also what keeps zero usable as the "no
        // character found" answer below.
        //
        // The gameplay server's own character is the one this lands on in
        // practice, since it is created first, but nothing here depends on that.
        return await context.Characters
            .Where(character => character.Identifier > 0)
            .OrderBy(character => character.Identifier)
            .Select(character => character.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
