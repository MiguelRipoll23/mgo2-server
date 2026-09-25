using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>The response a directly reachable but unimplemented event screen expects.</summary>
/// <param name="ResponseCommand">Command the rejection is written as.</param>
/// <param name="ExpectedRequestSize">Exact request size the screen sends, or unknown.</param>
public readonly record struct EventAdjacentRequest(ushort ResponseCommand, int ExpectedRequestSize)
{
    /// <summary>Whether an arrival has the shape the known screen sends.</summary>
    /// <param name="payloadLength">Length of the arrival's payload.</param>
    /// <returns>Whether the arrival may be answered as this screen.</returns>
    public bool IsExpectedShape(int payloadLength)
    {
        // A screen whose shape was never established accepts every arrival: with
        // no length to compare against, there is nothing that could be the wrong
        // screen.
        var shapeWasEstablished = ExpectedRequestSize != EventAdjacentRequestUtils.UnknownRequestSize;
        return !shapeWasEstablished || payloadLength == ExpectedRequestSize;
    }
}

/// <summary>
/// The seven event screens whose success body has not been recovered. Each one
/// still has to be answered: leaving the request slot pending is what the client
/// renders as a stall, whereas an explicit four-byte result moves it on. The
/// expected size is part of the mapping rather than re-derived per handler,
/// because a request that arrives with another shape is not the screen this
/// table describes and is refused with the generic code instead.
/// </summary>
public static class EventAdjacentRequestUtils
{
    /// <summary>
    /// Marks an unrecovered screen whose request shape is not established either.
    /// Every arrival is answered as the screen, because with no shape to compare
    /// against there is nothing to tell apart.
    /// </summary>
    public const int UnknownRequestSize = -1;

    /// <summary>Returns the rejection one command expects, when it is a known screen.</summary>
    /// <param name="command">Inbound command identifier.</param>
    /// <param name="request">Resolved rejection, when the command is known.</param>
    public static bool TryResolve(ushort command, out EventAdjacentRequest request)
    {
        request = command switch
        {
            CommandConstants.GetSurvivalAdjacentList =>
                new EventAdjacentRequest(CommandConstants.GetSurvivalAdjacentListResult, 8),
            CommandConstants.GetTournamentAdjacentList =>
                new EventAdjacentRequest(CommandConstants.GetTournamentAdjacentListResult, 9),
            CommandConstants.GetEventAdjacentDetail =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentDetailResult, 5),
            CommandConstants.GetEventAdjacentState =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentStateResult, 1),
            CommandConstants.GetEventAdjacentEntry =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentEntryResult, 4),
            CommandConstants.GetEventAdjacentTeam =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentTeamResult, 1),
            CommandConstants.SyncEventViewState =>
                new EventAdjacentRequest(CommandConstants.SyncEventViewStateResult, UnknownRequestSize),
            _ => default,
        };

        return request.ResponseCommand != 0;
    }
}
