using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the room a test hosts its pairing in, and the claim it takes on it.
/// Both are what a real dedicated room and its lease row are, read as the same
/// thing, so a drift here would leave a pairing waiting for a host that was
/// standing right there.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventHostRoomMemoryTests
{
    private const int LobbyIdentifier = 12;

    [Fact]
    public void A_host_room_is_eligible_for_a_match_and_holds_nobody_else()
    {
        var memory = new EventHostRoomMemoryService();
        var host = new CharacterMemoryService().Create();

        var room = memory.CreateHostRoom(LobbyIdentifier, host, EventHostEligibilityUtils.SurvivalHostName);

        // The room answers the three questions the assignment asks of every room,
        // which is what makes a test's own room indistinguishable from a row.
        Assert.True(TestIdentifierUtils.IsTest(room.Identifier));
        Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(room.Name, room.Common));
        Assert.True(EventHostEligibilityUtils.IsIdle(
            room.HostIdentifier,
            room.Players.Select(player => player.CharacterIdentifier)));
        Assert.True(EventHostEligibilityUtils.AcceptsMatch(
            EventConstants.SurvivalSelector,
            EventConstants.SurvivalSelector,
            room.MaximumPlayers,
            participantCount: 4));

        // The host is in the room, because the idle rule reads a roster and an
        // absent host cannot start a game.
        Assert.Equal(host.Identifier, room.HostIdentifier);
        Assert.Equal([host.Identifier], room.Players.Select(player => player.CharacterIdentifier));
        Assert.Equal(LobbyIdentifier, room.LobbyIdentifier);
    }

    [Fact]
    public void A_room_is_listed_only_in_the_lobby_it_belongs_to()
    {
        var memory = new EventHostRoomMemoryService();
        var host = new CharacterMemoryService().Create();
        var room = memory.CreateHostRoom(LobbyIdentifier, host, EventHostEligibilityUtils.SurvivalHostName);
        memory.CreateHostRoom(13, new CharacterMemoryService().Create(), EventHostEligibilityUtils.SurvivalHostName);

        Assert.Equal([room], memory.List(LobbyIdentifier));
        Assert.True(memory.Holds(room.Identifier));
        Assert.Same(room, memory.Find(room.Identifier));
    }

    [Fact]
    public void A_room_plays_one_match_and_a_match_plays_in_one_room()
    {
        var memory = new EventHostRoomMemoryService();
        var host = new CharacterMemoryService().Create();
        var room = memory.CreateHostRoom(LobbyIdentifier, host, EventHostEligibilityUtils.SurvivalHostName);
        var other = memory.CreateHostRoom(LobbyIdentifier, new CharacterMemoryService().Create(), EventHostEligibilityUtils.SurvivalHostName);

        var claim = memory.TryClaim(11, room.Identifier, activeStateIdentifier: 11, sequence: 1);
        Assert.NotNull(claim);

        // The two refusals the lease's unique indexes make, made here: a room that
        // is already playing, and a match that already has a room.
        Assert.Null(memory.TryClaim(12, room.Identifier, activeStateIdentifier: 12, sequence: 1));
        Assert.Null(memory.TryClaim(11, other.Identifier, activeStateIdentifier: 11, sequence: 1));
    }

    [Fact]
    public void A_claim_is_read_back_by_match_and_by_room_and_released_once()
    {
        var memory = new EventHostRoomMemoryService();
        var room = memory.CreateHostRoom(
            LobbyIdentifier,
            new CharacterMemoryService().Create(),
            EventHostEligibilityUtils.SurvivalHostName);
        var claim = memory.TryClaim(11, room.Identifier, activeStateIdentifier: 11, sequence: 1);
        Assert.NotNull(claim);
        Assert.Equal(claim, memory.FindClaimByMatch(11));

        // A room identifier is all a caller has, so the match it was taken for
        // comes back with the claim.
        var found = memory.FindClaimByRoom(room.Identifier);
        Assert.True(found.HasValue);
        Assert.Equal(11, found!.Value.MatchIdentifier);
        Assert.Equal(claim, found.Value.Claim);

        // Releasing is one room's claim, so the room takes another match after and
        // the released match reads as finished.
        Assert.True(memory.Release(room.Identifier));
        Assert.False(memory.Release(room.Identifier));
        Assert.Null(memory.FindClaimByMatch(11));
        Assert.Null(memory.FindClaimByRoom(room.Identifier));
        Assert.NotNull(memory.TryClaim(12, room.Identifier, activeStateIdentifier: 12, sequence: 1));
    }

    [Fact]
    public void Reset_forgets_every_room_and_claim()
    {
        var memory = new EventHostRoomMemoryService();
        var room = memory.CreateHostRoom(
            LobbyIdentifier,
            new CharacterMemoryService().Create(),
            EventHostEligibilityUtils.SurvivalHostName);
        memory.TryClaim(11, room.Identifier, activeStateIdentifier: 11, sequence: 1);

        memory.Reset();

        Assert.False(memory.Holds(room.Identifier));
        Assert.Empty(memory.List(LobbyIdentifier));
        Assert.Null(memory.FindClaimByMatch(11));
    }
}
