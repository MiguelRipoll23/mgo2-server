using Mgo2Server.Shared.Domain.News;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Announces a Survival team's winning streak to the lobby it is playing in.
/// <para>
/// The announcement is a ticker line, and a ticker line belongs to the mode it
/// celebrates: a Tournament win is the bracket's own business, so only a
/// Survival team's streak is written here. The audience is the lobby's players
/// rather than the winner's own team, because the team that is winning is the
/// subject of the line rather than one of its readers.
/// </para>
/// <para>
/// Two wins is the first streak worth announcing. A single win is what every
/// winner has, so there is nothing to tell the lobby that the result itself did
/// not already tell it.
/// </para>
/// </summary>
/// <param name="teamService">Service that owns the teams, read for the winner's name and streak.</param>
/// <param name="sessionDirectory">Lookup of the sessions this lobby is serving.</param>
/// <param name="flashNewsService">Service that writes the ticker line.</param>
public sealed class SurvivalStreakAnnouncementService(
    EventTeamService teamService,
    EventSessionDirectoryService sessionDirectory,
    FlashNewsService flashNewsService)
{
    /// <summary>Shortest streak that is worth a lobby announcement.</summary>
    private const int FirstAnnouncedStreak = 2;

    /// <summary>
    /// Announces the winner's streak when it is a Survival team on a run.
    /// <para>
    /// It is inert for a win that does not qualify: a Tournament team, a team
    /// that just broke its streak, and a team nobody is connected around all
    /// leave without writing anything.
    /// </para>
    /// </summary>
    /// <param name="winningTeamIdentifier">Team that won the finished match.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task AnnounceAsync(
        int winningTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (winningTeamIdentifier <= 0)
        {
            return;
        }

        var team = await teamService.FindAsync(winningTeamIdentifier, cancellationToken);
        if (team is null
            || team.MatchType != EventConstants.SurvivalSelector
            || team.ConsecutiveWins < FirstAnnouncedStreak)
        {
            return;
        }

        // The winning team is the line's subject, not its reader, so it is left
        // out of the lobby audience; every other player in the lobby is told.
        var recipients = sessionDirectory
            .LobbySessions(team.LobbyIdentifier)
            .Where(session => session.EventTeamIdentifier != winningTeamIdentifier)
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        await flashNewsService.BroadcastToAsync(
            recipients,
            new FlashNewsAnnouncement(
                $"Team {team.Name} has won {team.ConsecutiveWins} matches in a row!"),
            cancellationToken);
    }
}
