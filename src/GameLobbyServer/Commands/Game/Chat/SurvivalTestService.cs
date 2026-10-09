using Microsoft.Extensions.Logging;
using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// The first half of the scripted Survival self-test behind the <c>/test</c> chat
/// command: it makes a solo player's team look like one of a full field. The team
/// is padded with simulated players and those players are moved to the state a
/// pairing needs, so the only slot still undecided is the leader's own.
/// <para>
/// The run then stops, because what comes next is not the server's to do. A team
/// enters the field when its state changes, and that change is a request the
/// player's own screen sends — the team leaves the joinable list and becomes a
/// member of the field. So the run arms the second half,
/// <see cref="SurvivalTestOpponentService"/>, which forms the simulated opponent
/// the moment the team is recorded as waiting, and hands the finishing move to
/// the player: ready the last slot, and the opponent appears.
/// </para>
/// <para>
/// No room is opened here and none is looked for. A room exists because a client
/// asked for one and the create-room command served that request, and the
/// dedicated room this pairing needs is opened by the player afterwards, from
/// their own client. Until then the pairing waits, which is a state the match
/// screens are built for; the room's own creation is what offers the match to it.
/// </para>
/// <para>
/// The division of authorship is strict. The team's state and the leader's own
/// slot belong to the leader: the test never readies their slot for them and
/// never moves the team on its own initiative — it only adds the in-memory
/// players to the real team and moves <em>those</em> players from NG to Ready,
/// which is the state a pairing needs. Every state change is announced with a
/// chat line the server sends back as if the player had written it, which is the
/// only log a client can show, and the simulated roster changes are pushed as
/// ordinary roster notifications so the player's screens update the way they
/// would with a real teammate.
/// </para>
/// <para>
/// The steps are spaced a second apart so the client's screens settle between
/// them, and each run starts from a clean slate: the in-memory stores and the
/// half-finished runs are reset first, so a second run does not build on the
/// first one's rows.
/// </para>
/// </summary>
/// <param name="teamMemoryService">Store that owns the simulated teams and members.</param>
/// <param name="characterMemoryService">Store that owns the simulated characters.</param>
/// <param name="opponentService">Service that forms the simulated opponent once the team is frozen.</param>
/// <param name="teamService">Service that owns the teams, real and simulated.</param>
/// <param name="memberService">Service that owns the rosters, real and simulated.</param>
/// <param name="matchmakingService">Service that pairs the teams.</param>
/// <param name="assignmentService">Service that releases the room a pairing held.</param>
/// <param name="pushService">Service that pushes roster changes to the client.</param>
/// <param name="lobbyIdentity">Service that knows this lobby's own game type.</param>
/// <param name="sessionHelper">Helper used to write the chat lines.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class SurvivalTestService(
    EventTeamMemoryService teamMemoryService,
    CharacterMemoryService characterMemoryService,
    SurvivalTestOpponentService opponentService,
    EventTeamService teamService,
    EventTeamMemberService memberService,
    EventMatchmakingService matchmakingService,
    EventAssignmentService assignmentService,
    EventTeamPushService pushService,
    LobbyIdentityService lobbyIdentity,
    SessionHelper sessionHelper,
    ILogger<SurvivalTestService> logger)
{
    /// <summary>Text that triggers the test.</summary>
    public const string CommandText = "/test";

    /// <summary>Seconds between one scripted step and the next.</summary>
    private const int StepDelaySeconds = 1;

    /// <summary>Simulated players the real team is padded with.</summary>
    private const int AddedMemberCount = 2;

    /// <summary>Whether a chat line is the test command.</summary>
    /// <param name="text">Chat text as it arrived.</param>
    public static bool IsCommand(string text) =>
        string.Equals(text.Trim(), CommandText, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs the scripted test against the sender's own team.</summary>
    /// <param name="session">Connection that typed the command.</param>
    /// <param name="cancellationToken">Token that cancels the run.</param>
    public async Task RunAsync(TcpSession session, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier
            || session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            return;
        }

        // The mode is read from the process rather than taken from the teams, so a
        // lobby whose own configuration is broken refuses the run instead of
        // pairing teams a room could never accept.
        if (await lobbyIdentity.ResolveModeAsync(cancellationToken) is null)
        {
            return;
        }

        // A run is a fresh simulation: everything a previous one left behind is
        // dropped before the first step writes anything, the opponent a previous
        // run formed as much as the roster it padded.
        teamMemoryService.Reset();
        characterMemoryService.Reset();
        opponentService.Reset();

        var team = await teamService.FindOwnedInLobbyAsync(
            characterIdentifier,
            lobbyIdentifier,
            cancellationToken);
        if (team is null)
        {
            await SayAsync(
                session,
                characterIdentifier,
                "You have no event team in this lobby. Form one, ready your slot and enter the event first.",
                cancellationToken);
            return;
        }

        // A run rebuilds the situation from scratch, so any pairing the real team
        // is in — including one a previous run made — is released first, its room
        // with it: the queue un-pairs the two teams but the room they were leased
        // belongs to the assignment, and a run that left it claimed would find no
        // host free the second time. Releasing it also returns the team to the
        // joinable state, which is the state the player freezes again below.
        var previous = await matchmakingService.CancelAsync(team.Identifier, cancellationToken);
        if (previous.MatchIdentifier > 0)
        {
            await assignmentService.CancelAsync(previous.MatchIdentifier, cancellationToken);
        }

        logger.LogInformation(
            "Survival self-test started for character {CharacterIdentifier} on team {TeamIdentifier}",
            characterIdentifier,
            team.Identifier);

        // 1. Pad the real team with simulated players, pending like any arrival.
        var added = AddSimulatedPlayers(team);
        await PushAddedAsync(team.Identifier, added, cancellationToken);
        await SayAsync(
            session,
            characterIdentifier,
            $"Added {added.Count} in-memory players to \"{team.Name}\" with state NG.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 2. Move the simulated players from NG to Ready, which leaves the
        // leader's own slot as the only one the field is still waiting on.
        await ReadyAsync(team.Identifier, added, cancellationToken);
        await PushDecisionAsync(team.Identifier, added, cancellationToken);
        await SayAsync(
            session,
            characterIdentifier,
            $"In-memory players are now Ready; {added.Count} slots moved from NG.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 3. Arm the second half and hand the player the move that starts it. The
        // opponent is not formed here: the team is still joinable, and it is the
        // player's own state change — readying the last slot, which takes the
        // team out of the joinable list and into the field — that is the moment
        // an opponent exists for.
        opponentService.ExpectOpponent(team.Identifier);
        await SayAsync(
            session,
            characterIdentifier,
            "Ready your own slot now: the team is queued the moment you do, and the in-memory opponent is formed for it then.",
            cancellationToken);
    }

    private List<EventTeamMember> AddSimulatedPlayers(EventTeam team)
    {
        var added = new List<EventTeamMember>();
        var freeSlots = FreeSlots(team);
        for (var index = 0; index < AddedMemberCount && index < freeSlots.Count; index++)
        {
            added.Add(memberService.AddSimulatedMember(
                team.Identifier,
                freeSlots[index],
                characterMemoryService.Create()));
        }

        return added;
    }

    private async Task ReadyAsync(
        int teamIdentifier,
        List<EventTeamMember> members,
        CancellationToken cancellationToken)
    {
        foreach (var member in members)
        {
            await memberService.SetDecisionAsync(
                teamIdentifier,
                member.CharacterIdentifier,
                decision: 1,
                cancellationToken);
        }
    }

    private async Task PushAddedAsync(
        int teamIdentifier,
        List<EventTeamMember> members,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return;
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null)
        {
            return;
        }

        var snapshot = EventTeamService.BuildSnapshot(team);

        // The client validates a roster notification against the team's own
        // state, so a member shown on a queued team has to carry the state that
        // team expects; the pending state the simulated member is stored with is
        // the one the ready step moves away, not the one the push may carry.
        foreach (var member in members)
        {
            if (member.Slot >= 0 && member.Slot < snapshot.Participants.Length)
            {
                snapshot.Participants[member.Slot].State =
                    EventTeamRegistrationUtils.ParticipantStateFor(snapshot.State);
            }

            await pushService.PushParticipantAddedAsync(
                snapshot,
                member.Slot,
                excludedSession: null,
                cancellationToken);
        }
    }

    private async Task PushDecisionAsync(
        int teamIdentifier,
        List<EventTeamMember> members,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return;
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null)
        {
            return;
        }

        var snapshot = EventTeamService.BuildSnapshot(team);
        foreach (var member in members)
        {
            await pushService.PushParticipantDecisionAsync(snapshot, member.Slot, cancellationToken);
        }
    }

    private Task SayAsync(
        TcpSession session,
        int characterIdentifier,
        string text,
        CancellationToken cancellationToken)
    {
        var request = new ChatRequest(ChatPayloadBuilder.PublicChannelDigit, text);
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.SendChatResult,
            ChatPayloadBuilder.BuildServerReply(characterIdentifier, request),
            cancellationToken);
    }

    private static List<int> FreeSlots(EventTeam team) =>
        [.. Enumerable
            .Range(1, EventConstants.TeamRosterSize - 1)
            .Where(candidate => !team.Members.Any(member => member.Slot == candidate))];

    private static Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(StepDelaySeconds), cancellationToken);
}
