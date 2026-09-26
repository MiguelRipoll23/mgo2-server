using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence;
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
/// <param name="GameIdentifier">Room that was created, when one was.</param>
/// <param name="RoomName">Name the room carries.</param>
/// <param name="HostCharacterIdentifier">Character hosting the room.</param>
public readonly record struct FakeHostRoomResult(
    FakeHostRoomOutcome Outcome,
    int GameIdentifier,
    string RoomName,
    int HostCharacterIdentifier);

/// <summary>
/// Creates a dedicated event host room, so a paired match can reach a host
/// without a player sitting in a game client to make one.
/// <para>
/// A pairing does not announce itself. The match row is written when two teams
/// are queued, but the match-found packet is only pushed once a room has been
/// leased, and a room is only leased from one that is named for the role, says
/// it is dedicated, and is sitting idle with its host present. All three of
/// those are things a real host client does by existing, so a pairing made from
/// the testing tools sits in "paired, waiting for a host" until somebody opens
/// a dedicated room by hand — which reads from the page as a pairing that never
/// worked, when in fact it paired correctly and simply has nowhere to go.
/// </para>
/// <para>
/// So this writes the room. It is a testing device and it says so: the room is
/// named for the role the eligibility rule looks for and carries the dedicated
/// flag, and its host is a real character because the room's host column is a
/// foreign key and the sweep requires the host to be present in the room. The
/// host is therefore a character that exists, and the room is removed with the
/// testing tools rather than left behind.
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
    /// Creates the dedicated host room of a mode in a lobby.
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
        var roomName = RoomNameFor(mode);
        if (roomName is null)
        {
            return new FakeHostRoomResult(FakeHostRoomOutcome.NotAnEventHost, 0, string.Empty, 0);
        }

        var host = hostCharacterIdentifier > 0
            ? await FindHostAsync(hostCharacterIdentifier, cancellationToken)
            : await FindFirstHostAsync(cancellationToken);

        if (host <= 0)
        {
            return new FakeHostRoomResult(
                FakeHostRoomOutcome.NoHostCharacter, 0, roomName, hostCharacterIdentifier);
        }

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = host;
            room.LobbyIdentifier = lobbyIdentifier;
            room.Name = roomName;

            // A match of up to sixteen players plus the dedicated host has to
            // fit, so the room is created at the largest size the eligibility
            // rule will accept rather than at a player's usual eight.
            room.MaximumPlayers =
                EventHostEligibilityUtils.MatchPlayerCapacity
                + EventHostEligibilityUtils.DedicatedHostPlayerSlots;
            room.Comment = "Created by the event testing tools.";

            // The flag the host-eligibility rule reads. The room is dedicated to
            // the mode, which is what makes it eligible for that mode's matches
            // and nothing else.
            room.Common = """{"dedicated":true}""";
        }, cancellationToken);

        // The sweep requires the host to be present in the room, and the room's
        // own roster row is what says so.
        await gameService.AddPlayerAsync(game.Identifier, host, cancellationToken);

        return new FakeHostRoomResult(
            FakeHostRoomOutcome.Created, game.Identifier, roomName, host);
    }

    private async Task<int> FindHostAsync(
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var exists = await context.Characters
            .AnyAsync(character => character.Identifier == characterIdentifier, cancellationToken);
        return exists ? characterIdentifier : 0;
    }

    private async Task<int> FindFirstHostAsync(CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        // The lowest identifier is used because the room needs a character that
        // exists and nothing else about it: the sweep only asks whether the
        // host is in the room, and this row is what puts it there. Zero is the
        // "there is no character at all" answer, which the caller refuses on.
        return await context.Characters
            .OrderBy(character => character.Identifier)
            .Select(character => character.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
