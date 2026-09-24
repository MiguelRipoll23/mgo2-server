using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>The response a directly reachable but unimplemented event screen expects.</summary>
/// <param name="ResponseCommand">Command the rejection is written as.</param>
/// <param name="ExpectedRequestSize">Exact request size the screen sends.</param>
public readonly record struct EventAdjacentRequest(ushort ResponseCommand, int ExpectedRequestSize);

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
            CommandConstants.ReserveTournamentEntry =>
                new EventAdjacentRequest(CommandConstants.ReserveTournamentEntryResult, 4),
            CommandConstants.GetEventAdjacentDetail =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentDetailResult, 5),
            CommandConstants.GetEventAdjacentState =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentStateResult, 1),
            CommandConstants.GetEventAdjacentEntry =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentEntryResult, 4),
            CommandConstants.GetEventAdjacentTeam =>
                new EventAdjacentRequest(CommandConstants.GetEventAdjacentTeamResult, 1),
            _ => default,
        };

        return request.ResponseCommand != 0;
    }
}
