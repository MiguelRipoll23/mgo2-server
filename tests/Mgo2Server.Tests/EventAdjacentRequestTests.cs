using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the table of unrecovered event screens. Each command must still be
/// answered, or the request slot stays pending and the client renders it as a
/// stall; the table maps each screen to its result command and request shape.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventAdjacentRequestTests
{
    [Fact]
    public void A_known_screen_accepts_only_its_request_shape()
    {
        Assert.True(EventAdjacentRequestUtils.TryResolve(
            CommandConstants.GetSurvivalAdjacentList, out var request));
        Assert.Equal(CommandConstants.GetSurvivalAdjacentListResult, request.ResponseCommand);
        Assert.True(request.IsExpectedShape(8));
        Assert.False(request.IsExpectedShape(7));
    }

    [Fact]
    public void The_view_state_sync_is_answered_regardless_of_shape()
    {
        // The sync's request shape has not been recovered either, so there is
        // nothing to compare an arrival against and every one is the screen.
        Assert.True(EventAdjacentRequestUtils.TryResolve(
            CommandConstants.SyncEventViewState, out var request));
        Assert.Equal(CommandConstants.SyncEventViewStateResult, request.ResponseCommand);
        Assert.True(request.IsExpectedShape(0));
        Assert.True(request.IsExpectedShape(99));
    }

    [Fact]
    public void The_tournament_reservation_has_a_real_handler()
    {
        // It was once answered as an unrecovered screen; a real handler serves
        // it now, so the table no longer holds a shadow registration for it.
        Assert.False(EventAdjacentRequestUtils.TryResolve(
            CommandConstants.ReserveTournamentEntry, out _));
    }

    [Fact]
    public void An_unknown_command_has_no_mapping()
    {
        Assert.False(EventAdjacentRequestUtils.TryResolve(0x4999, out var request));
        Assert.Equal(0, request.ResponseCommand);
    }
}