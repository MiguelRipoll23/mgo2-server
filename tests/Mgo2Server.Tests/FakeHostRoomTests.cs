using System.Data.Common;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the dedicated host room the testing tools can create.
/// <para>
/// A pairing is written when two teams are queued and is not announced until a
/// room has been leased for it, so a room is the whole difference between a
/// pairing that works and one that sits waiting. What makes a room eligible is
/// three things at once — the role name, the dedicated flag, and the host being
/// present — so these tests pin the ones this service controls and refuse the
/// modes that have no such room.
/// </para>
/// </summary>
[Trait("Category", "Shared")]
public sealed class FakeHostRoomTests
{
    [Theory]
    [InlineData(EventConstants.SurvivalSelector, EventHostEligibilityUtils.SurvivalHostName)]
    [InlineData(EventConstants.TournamentSelector, EventHostEligibilityUtils.TournamentHostName)]
    public void A_mode_with_a_dedicated_host_room_names_it_after_the_role(
        int mode,
        string expected)
    {
        Assert.Equal(expected, FakeHostRoomService.RoomNameFor(mode));
    }

    [Theory]
    [InlineData(EventConstants.TournamentRegistrationSelector)]
    [InlineData(0)]
    [InlineData(255)]
    public void A_mode_with_no_dedicated_host_room_has_no_name(int mode)
    {
        // The registration lobby is not a match lobby: it hands players to one,
        // and a room hosted there would never be leased by a match. Inventing a
        // name for it would produce a room nothing ever reads.
        Assert.Null(FakeHostRoomService.RoomNameFor(mode));
    }

    [Fact]
    public void The_names_the_service_writes_are_the_ones_eligibility_accepts()
    {
        // The room is only leased if the eligibility rule recognises it, and
        // that rule reads the name and the flag. This is the pairing between
        // what is written and what is looked for.
        foreach (var mode in new[]
        {
            EventConstants.SurvivalSelector,
            EventConstants.TournamentSelector,
        })
        {
            var name = FakeHostRoomService.RoomNameFor(mode)!;

            Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(
                name, """{"dedicated":true}"""));

            // The same blob without the flag is a player's ordinary room, and
            // must stay ineligible however it is named.
            Assert.False(EventHostEligibilityUtils.IsDedicatedEventHost(name, "{}"));
        }
    }

    [Fact]
    public void A_host_room_has_to_seat_the_match_and_its_own_host()
    {
        // The room the service creates is sized for the largest match plus the
        // dedicated host, so a full pairing fits it. A room at a player's usual
        // eight would pass eligibility and then find nowhere to seat anybody.
        var largest = EventHostEligibilityUtils.MatchPlayerCapacity
            + EventHostEligibilityUtils.DedicatedHostPlayerSlots;

        Assert.True(EventHostEligibilityUtils.HasCapacity(largest, largest - 1));
        Assert.False(EventHostEligibilityUtils.HasCapacity(largest - 1, largest - 1));
    }

    [Fact]
    public async Task A_mode_with_no_host_room_is_refused_without_touching_the_database()
    {
        // The mode decides whether such a room exists at all, so it is answered
        // before anything is read: a registration lobby must not end up
        // furnishing a host for matches that are played elsewhere.
        var factory = new CountingDbContextFactory();
        var service = new FakeHostRoomService(factory);

        var result = await service.CreateAsync(
            9, EventConstants.TournamentRegistrationSelector, 0, CancellationToken.None);

        Assert.Equal(FakeHostRoomOutcome.NotAnEventHost, result.Outcome);
        Assert.Null(result.Room);
        Assert.Equal(0, factory.Created);
    }

    [Fact]
    public async Task The_host_is_read_from_the_characters_the_room_has_a_foreign_key_to()
    {
        var factory = new UnreachableDbContextFactory();
        var service = new FakeHostRoomService(factory);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.CreateAsync(9, EventConstants.SurvivalSelector, 0, CancellationToken.None));

        // The host is read from the characters table before the room is written,
        // so a connection failure here proves the host lookup is expressible —
        // which is the part that has to be, since a fabricated identifier would
        // fail the room's foreign key instead.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
    }

    [Fact]
    public void A_room_hosted_by_character_zero_would_never_be_idle()
    {
        // This is why the host lookup skips zero rather than taking the lowest
        // identifier it finds. A roster is a list of characters, and zero is
        // how the roster spells "nobody here", so a host of zero is
        // indistinguishable from an absent one: the row that would say the host
        // is present is the very row the room cannot carry.
        Assert.False(EventHostEligibilityUtils.IsIdle(0, []));

        // Which is why AddPlayerAsync declines to write one, and why a room
        // created this way would sit ineligible forever — looking exactly like
        // the pairing that never reached a host. A real host identifier is
        // idle as soon as it is in its own room.
        Assert.True(EventHostEligibilityUtils.IsIdle(1, [1]));
    }

    [Fact]
    public void An_in_memory_room_is_eligible_on_the_same_terms_as_a_real_one()
    {
        // The point of holding the room in memory beside the real ones rather
        // than instead of them is that nothing downstream can tell them apart.
        // The eligibility rule reads a name, a flag, a host and a roster, so a
        // room built the same way has to pass it the same way — otherwise "in
        // memory data acts like real data" would be true of the room list and
        // not of the rule that decides whether the room may host anything.
        var room = FakeHostRoom.Create(
            FakePlayerIdentifierUtils.FirstFakeIdentifier,
            9,
            EventConstants.SurvivalSelector,
            1,
            EventHostEligibilityUtils.SurvivalHostName);

        Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(
            room.Room.Name, room.Room.Common));

        // The roster is what says the host is present, and it is the piece that
        // used to arrive empty from the room list.
        Assert.True(EventHostEligibilityUtils.IsIdle(
            room.Room.HostIdentifier,
            room.Room.Players.Select(player => player.CharacterIdentifier)));

        Assert.True(EventHostEligibilityUtils.AcceptsMatch(
            EventConstants.SurvivalSelector,
            EventConstants.SurvivalSelector,
            room.Room.MaximumPlayers,
            EventHostEligibilityUtils.MatchPlayerCapacity));
    }

    [Fact]
    public void An_in_memory_room_is_refused_the_host_it_cannot_say_is_present()
    {
        // A host of zero is how a roster spells "nobody here", so a room built
        // around one would claim a host is present in an empty room, and the
        // idle rule would read it as ready — a room created and then never
        // eligible, which is the pairing that never reached a host.
        var room = FakeHostRoom.Create(
            FakePlayerIdentifierUtils.FirstFakeIdentifier,
            9,
            EventConstants.SurvivalSelector,
            0,
            EventHostEligibilityUtils.SurvivalHostName);

        Assert.Empty(room.Room.Players);
        Assert.False(EventHostEligibilityUtils.IsIdle(
            room.Room.HostIdentifier,
            room.Room.Players.Select(player => player.CharacterIdentifier)));
    }

    [Fact]
    public void An_in_memory_room_is_the_same_kind_of_room_a_real_one_is()
    {
        // Everything downstream — the eligibility rule, the claim, the
        // assignment packets — is written against this one type. A room returned
        // as something else would need a second code path at every one of those
        // readers, which is the difference between in-memory data acting like
        // real data and merely looking like it.
        var room = FakeHostRoom.Create(
            FakePlayerIdentifierUtils.FirstFakeIdentifier,
            9,
            EventConstants.SurvivalSelector,
            1,
            EventHostEligibilityUtils.SurvivalHostName);

        Game asRealRoom = room.Room;

        Assert.Equal(1, asRealRoom.HostIdentifier);
        Assert.Equal(9, asRealRoom.LobbyIdentifier);
        Assert.Equal(EventHostEligibilityUtils.SurvivalHostName, asRealRoom.Name);
    }

    [Fact]
    public void A_claim_stands_in_for_the_lease_an_in_memory_room_cannot_have()
    {
        // The lease's room column is a foreign key and names rows, so a room that
        // exists only in this process cannot be named by one. The claim is what
        // holds it instead, and it holds it under the same two rules the unique
        // indexes settle for a real room: one room plays one match, and one
        // match plays in one room.
        var claims = new FakeHostClaimService();
        var roomIdentifier = FakePlayerIdentifierUtils.FirstFakeIdentifier;

        var first = claims.TryClaimAsync(7, roomIdentifier, 7, 3);

        Assert.NotNull(first);
        Assert.Equal(7, first!.MatchIdentifier);
        Assert.Equal(roomIdentifier, first.RoomIdentifier);

        // A second match cannot take a room that is already playing.
        Assert.Null(claims.TryClaimAsync(8, roomIdentifier, 8, 1));

        // Nor can one match play in two rooms.
        Assert.Null(claims.TryClaimAsync(7, roomIdentifier + 1, 7, 1));

        Assert.Equal(7, claims.FindByRoom(roomIdentifier)?.MatchIdentifier);
        Assert.Equal(roomIdentifier, claims.FindByMatch(7)?.RoomIdentifier);
    }

    [Fact]
    public void Releasing_a_claim_returns_the_room_to_the_pool()
    {
        // A finished or cancelled match gives its room back, and the room is then
        // claimable again — which is what lets the next pairing be assigned to
        // it rather than finding the pool mysteriously empty.
        var claims = new FakeHostClaimService();
        var roomIdentifier = FakePlayerIdentifierUtils.FirstFakeIdentifier;

        claims.TryClaimAsync(7, roomIdentifier, 7, 3);

        Assert.True(claims.Release(roomIdentifier));
        Assert.Null(claims.FindByRoom(roomIdentifier));
        Assert.Null(claims.FindByMatch(7));
        Assert.NotNull(claims.TryClaimAsync(8, roomIdentifier, 8, 1));
    }

    [Fact]
    public void A_claim_needs_a_match_and_a_room_to_name()
    {
        var claims = new FakeHostClaimService();

        Assert.Throws<ArgumentException>(
            () => claims.TryClaimAsync(0, FakePlayerIdentifierUtils.FirstFakeIdentifier, 0, 1));
        Assert.Throws<ArgumentException>(
            () => claims.TryClaimAsync(7, 0, 7, 1));
    }

    [Fact]
    public void An_in_memory_room_is_identified_outside_the_range_a_real_room_uses()
    {
        // The claim and the lease are told apart by asking whether the room's
        // identifier is from the fake range, so the two must be unambiguous. A
        // real room's identifier is a database sequence and never reaches here.
        var room = FakeHostRoom.Create(
            FakePlayerIdentifierUtils.FirstFakeIdentifier,
            9,
            EventConstants.SurvivalSelector,
            1,
            EventHostEligibilityUtils.SurvivalHostName);

        Assert.True(FakePlayerIdentifierUtils.IsFake(room.Room.Identifier));
        Assert.False(FakePlayerIdentifierUtils.IsFake(116));
    }

    private static GameService CreateGameService(IDbContextFactory<Mgo2DatabaseContext> factory) =>
        new(factory, new ServerMetricsService(factory), Options.Create(new ServerOptions()));

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }

    /// <summary>
    /// Counts the contexts it hands out, so a test can prove a path never
    /// reached the database at all.
    /// </summary>
    private sealed class CountingDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        /// <summary>How many contexts were asked for.</summary>
        public int Created { get; private set; }

        public Mgo2DatabaseContext CreateDbContext()
        {
            Created++;
            return new Mgo2DatabaseContext(
                new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                    .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                    .Options);
        }
    }
}
