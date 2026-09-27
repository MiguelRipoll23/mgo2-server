using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The listing half of the event testing tools: the teams a lobby holds, the
/// ones a player formed and the ones the tools created, all as rows.
/// <para>
/// It sits beside the fake-team endpoints rather than inside them because the
/// two answer different questions. Those carry a request to the lobby that will
/// act on it; this reads the rows directly, which is what a moderator is asking
/// about when they want to fill a team. Because a testing team is a row like
/// any other, this is the single list the page draws.
/// </para>
/// <para>
/// It is public for the same reason the fake-team endpoints are — the page that
/// drives it is public, and a page that asked for a bearer token would only
/// move the token from the URL bar into a form. It reads, and the one thing a
/// caller can do with what it learns is add players to a team that already
/// exists, which the fake-player endpoint already allowed by name.
/// </para>
/// </summary>
internal static class EventTeamEndpoints
{
    /// <summary>Maps the formed-team listing endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapEventTeamEndpoints(this RouteGroupBuilder group)
    {
        var teams = group.MapGroup("/event-teams")
            .WithTags("Event teams");

        teams.MapGet("/", ListAsync)
            .WithSummary("List the teams of a lobby")
            .WithDescription(
                "Answers with every team of the running lobby of a mode, in any state: the joinable "
                + "ones, the ones queued for an opponent and the ones that have been assigned a game. "
                + "A team the testing tools created is a row like any other, so it appears here beside "
                + "the ones a player formed.");
    }

    /// <summary>Lists the teams of the lobby a mode names.</summary>
    /// <param name="listing">Service the rows are read through.</param>
    /// <param name="modes">Service that resolves the lobby a mode names.</param>
    /// <param name="mode">Lobby mode to ask about.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ListAsync(
        EventTeamListingService listing,
        LobbyModeResolverService modes,
        int mode,
        CancellationToken cancellationToken)
    {
        // The mode names the lobby rather than the identifier, because a caller
        // of a testing tool knows which lobby it is looking at by what it
        // offers, not by a number it would have to look up first.
        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(mode)} lobby running."));
        }

        var teams = await listing.ListAsync(lobby.Identifier, cancellationToken);

        return Results.Ok(new EventTeamListingResult(
            mode,
            lobby.Identifier,
            [.. teams.Select(EventTeamEntry.Of)]));
    }
}
