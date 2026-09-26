using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The pairing half of the event testing tools: the matches a lobby is holding
/// right now, and the room each is waiting for or holding.
/// <para>
/// It sits beside the team listing rather than inside it because it answers a
/// different question. That one asks which teams exist; this one asks what the
/// entry pipeline has done with them. The gap between the two is where a
/// moderator is left guessing: a pair of teams that have not matched, a pair
/// that has matched, and a pair that has matched and is waiting for a host all
/// show nothing at all on a client, because the match-found packet is only sent
/// once a room has been leased for it.
/// </para>
/// <para>
/// It is read-only and public for the same reason the team listing is: it is a
/// testing device, the page that drives it is public, and there is nothing here
/// a caller could change.
/// </para>
/// </summary>
internal static class EventMatchEndpoints
{
    /// <summary>Maps the pairing listing endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapEventMatchEndpoints(this RouteGroupBuilder group)
    {
        var matches = group.MapGroup("/event-matches")
            .WithTags("Event matches");

        matches.MapGet("/", ListAsync)
            .WithSummary("List the live pairings of a lobby")
            .WithDescription(
                "Answers with the pairings a lobby is holding: the two teams of each and whether it is "
                + "waiting for a host or has been given one. A pairing is only announced to its teams "
                + "once a room has been leased for it, so this is the only place a pairing that has "
                + "happened but is not visible in game can be seen.");
    }

    /// <summary>Lists the live pairings of the lobby a mode names.</summary>
    /// <param name="listing">Service the rows are read through.</param>
    /// <param name="modes">Service that resolves the lobby a mode names.</param>
    /// <param name="mode">Lobby mode to ask about.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ListAsync(
        EventMatchListingService listing,
        LobbyModeResolverService modes,
        int mode,
        CancellationToken cancellationToken)
    {
        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(mode)} lobby running."));
        }

        var matches = await listing.ListAsync(lobby.Identifier, cancellationToken);

        return Results.Ok(new EventMatchListingResult(
            mode,
            lobby.Identifier,
            [.. matches.Select(EventMatchEntry.Of)]));
    }
}
