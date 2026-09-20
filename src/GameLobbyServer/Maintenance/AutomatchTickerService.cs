using Mgo2Server.GameLobbyServer.Commands.Game.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Runs the automatch matchmaker and delivers what it produces: the search panel
/// every tick, then a formed group, the room it produced, or the failure that
/// ended it.
/// <para>
/// The delivery is the whole point of the worker. Answering a start with zero is
/// what arms the client's push channel and is also the moment it stops sending
/// anything at all, so a queue that is never ticked is a player watching a
/// stopwatch run out. Each push is per recipient rather than broadcast: the
/// shortfall and the band are that searcher's own, and the encoders are
/// per-connection anyway.
/// </para>
/// </summary>
/// <param name="automatchService">Queue that is drained and released.</param>
/// <param name="activeGameSessions">Sessions of this lobby, which are the push targets.</param>
/// <param name="sessionHelper">Helper used to write the pushes.</param>
/// <param name="options">Operator policy, which sets the tick interval.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class AutomatchTickerService(
    AutomatchService automatchService,
    ActiveGameSessionsService activeGameSessions,
    SessionHelper sessionHelper,
    IOptions<AutomatchOptions> options,
    ILogger<AutomatchTickerService> logger)
    : PeriodicWorker(options.Value.Tick, logger, FailureBackoffCeiling)
{
    /// <summary>Columns of the population histogram, which is the client's own bar count.</summary>
    private const int PanelColumns = 23;

    /// <summary>
    /// Longest a failing matchmaker waits before trying again. Far shorter than the
    /// default, because a searcher is sitting in front of this tick: a database that
    /// comes back has to be picked up within the minute, and the panels are what a
    /// waiting player sees.
    /// </summary>
    private static readonly TimeSpan FailureBackoffCeiling = TimeSpan.FromMinutes(1);

    /// <summary>Detail carried by the failure push when a group's host never produced a room.</summary>
    private const int NoHostDetail = 0;

    private int lobbyIdentifier;
    private int lobbySubtype;

    /// <summary>Starts the matchmaker for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    /// <param name="lobbySubtype">Game type of the lobby, which the formed-match push carries.</param>
    public void StartFor(int lobbyIdentifier, int lobbySubtype)
    {
        this.lobbyIdentifier = lobbyIdentifier;
        this.lobbySubtype = lobbySubtype;
        Start();
    }

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (lobbyIdentifier <= 0)
        {
            return;
        }

        await automatchService.TickAsync(cancellationToken);

        // Formed before released before failed: a group that has just been
        // elected has to be told to create the room, and one whose room exists has
        // to be told where it is. Panels go last because only searchers still
        // waiting are painted, and a group that has just been released is no longer
        // waiting.
        await PushFormedMatchesAsync(cancellationToken);
        await PushReleasedMatchesAsync(cancellationToken);
        await PushFailedMatchesAsync(cancellationToken);
        await PushSearchPanelsAsync(cancellationToken);
    }

    private async Task PushFormedMatchesAsync(CancellationToken cancellationToken)
    {
        var sessions = SessionsByCharacter();

        foreach (var match in automatchService.TakeFormedMatches())
        {
            if (match.Rules.Count == 0)
            {
                continue;
            }

            var settings = AutomatchSettingsBlock.Build(match.Rules, match.Map);
            var payload = AutomatchPushWriter.BuildMatchFound(
                match.HostCharacterIdentifier,
                lobbyIdentifier,
                lobbySubtype,
                match.Rules[0],
                settings);

            logger.LogInformation(
                "Automatch formed: host {HostCharacterIdentifier} of {Members} players, rules {Rules}, map {Map}",
                match.HostCharacterIdentifier,
                match.Members.Count,
                string.Join(',', match.Rules),
                match.Map);

            await SendToMembersAsync(sessions, match, CommandConstants.AutomatchMatchFound, payload, cancellationToken);
        }
    }

    private async Task PushReleasedMatchesAsync(CancellationToken cancellationToken)
    {
        var sessions = SessionsByCharacter();

        foreach (var match in automatchService.TakeReleasedMatches())
        {
            var payload = AutomatchPushWriter.BuildMatchGame(match.GameIdentifier);

            // The host is included, and that is load-bearing: this push is what
            // moves a client off the match-found screen into the room, and the host
            // is parked on that same screen having just created it.
            await SendToMembersAsync(sessions, match, CommandConstants.AutomatchMatchGame, payload, cancellationToken);

            logger.LogInformation(
                "Automatch released {Members} players into room {GameIdentifier}",
                match.Members.Count,
                match.GameIdentifier);
        }
    }

    private async Task PushFailedMatchesAsync(CancellationToken cancellationToken)
    {
        var sessions = SessionsByCharacter();

        foreach (var match in automatchService.TakeFailedMatches())
        {
            var payload = AutomatchPushWriter.BuildMatchFailed(NoHostDetail);

            // Only a client that has not received the release push is still
            // listening, which is exactly this group: it is still on the
            // match-found screen, so this is the last moment it can be told.
            await SendToMembersAsync(sessions, match, CommandConstants.AutomatchMatchFailed, payload, cancellationToken);

            logger.LogWarning(
                "Automatch host {HostCharacterIdentifier} {Reason}; {Members} players told the match failed",
                match.HostCharacterIdentifier,
                match.HostLost ? "disconnected" : "never created a room",
                match.Members.Count);
        }
    }

    private async Task PushSearchPanelsAsync(CancellationToken cancellationToken)
    {
        var panels = automatchService.BuildSearchPanels(PanelColumns);
        if (panels.Count == 0)
        {
            return;
        }

        var sessions = SessionsByCharacter();

        // The second array stays zero. Its meaning is unestablished, and a number
        // nobody can justify in it is worse than an honest zero.
        var inGame = new int[PanelColumns];

        foreach (var panel in panels)
        {
            if (!sessions.TryGetValue(panel.CharacterIdentifier, out var session))
            {
                continue;
            }

            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.AutomatchSearchPanel,
                AutomatchPushWriter.BuildSearchPanel(panel.MatchmakingByLevel, inGame, panel.Band, panel.PlayersNeeded),
                cancellationToken);
        }
    }

    private async Task SendToMembersAsync(
        Dictionary<int, TcpSession> sessions,
        PendingMatch match,
        ushort command,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        foreach (var characterIdentifier in match.Members)
        {
            if (sessions.TryGetValue(characterIdentifier, out var session))
            {
                await sessionHelper.SendPacketAsync(session, command, payload, cancellationToken);
            }
        }
    }

    /// <summary>Maps the characters searching in this lobby to the sessions they are on.</summary>
    private Dictionary<int, TcpSession> SessionsByCharacter() =>
        activeGameSessions.List()
            .Where(session => session.CharacterIdentifier is not null && session.LobbyIdentifier == lobbyIdentifier)
            .GroupBy(session => session.CharacterIdentifier!.Value)
            .ToDictionary(group => group.Key, group => group.First());
}
