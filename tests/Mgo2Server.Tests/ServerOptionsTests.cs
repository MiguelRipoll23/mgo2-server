using Mgo2Server.Shared.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the windows a room is listed within. A room list is what a client
/// joins from, so the window is the promise that a room on it is a room that
/// still answers — which is a promise about the host's own beat, not a number
/// chosen for looking tidy.
/// </summary>
[Trait("Category", "Shared")]
public sealed class ServerOptionsTests
{
    [Fact]
    public void The_room_window_is_two_client_beats()
    {
        var options = new ServerOptions();

        // The host's ping report is sent every half minute, so a minute is a
        // room that has missed both of its beats: one lost report does not take
        // a live room off the browser, and a host that has gone does.
        Assert.Equal(60, options.GameStaleSeconds);
    }

    [Fact]
    public void A_dedicated_host_beats_inside_the_window_it_is_listed_within()
    {
        var options = new ServerOptions();

        // Its beat has to land inside the window, or the host delists its own
        // match between two of its beats however healthy the host is.
        Assert.True(
            options.MatchHeartbeatIntervalSeconds < options.GameStaleSeconds,
            $"the match beat of {options.MatchHeartbeatIntervalSeconds}s does not land inside " +
            $"the window of {options.GameStaleSeconds}s");
    }
}
