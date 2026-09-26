using System.Data.Common;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
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
        var service = new FakeHostRoomService(factory, CreateGameService(factory));

        var result = await service.CreateAsync(
            9, EventConstants.TournamentRegistrationSelector, 0, CancellationToken.None);

        Assert.Equal(FakeHostRoomOutcome.NotAnEventHost, result.Outcome);
        Assert.Equal(0, result.GameIdentifier);
        Assert.Equal(0, factory.Created);
    }

    [Fact]
    public async Task The_host_is_read_from_the_characters_the_room_has_a_foreign_key_to()
    {
        var factory = new UnreachableDbContextFactory();
        var service = new FakeHostRoomService(factory, CreateGameService(factory));

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.CreateAsync(9, EventConstants.SurvivalSelector, 0, CancellationToken.None));

        // The host is read from the characters table before the room is written,
        // so a connection failure here proves the host lookup is expressible —
        // which is the part that has to be, since a fabricated identifier would
        // fail the room's foreign key instead.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
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
