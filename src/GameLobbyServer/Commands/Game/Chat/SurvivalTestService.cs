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
/// The scripted Survival self-test behind the <c>/test</c> chat command. It makes
/// a solo player's team look like one of a full field: the real team is padded
/// with simulated players, a simulated opponent is formed and queued, and the
/// dedicated host room a pairing needs is opened for it as a real room — so the
/// whole matchmaking path runs without a second person.
/// <para>
/// The pairing is not the end of it. Two paired teams only hear anything once a
/// room has been claimed for them, and a room is only claimed when one is named
/// for the role, says it is dedicated and sits idle with its host present. Such
/// a room is not made here, and could not be: a room exists because a client
/// asked for one and the create-room command served that request. The run
/// therefore looks for the room it needs and says which room to open when there
/// is none, so the host a match is published with is always a room a client
/// really opened. That room need not be in this lobby: the role a host is named
/// for sets its mode when it is created.
/// </para>
/// <para>
/// The division of authorship is strict. The team's state and the leader's own
/// slot belong to the leader: the test never readies their slot for them and
/// never moves the team on its own initiative — it only adds the in-memory
/// players to the real team and moves <em>those</em> players from NG to Ready,
/// which is the state a pairing needs. Every state change is announced
/// with a chat line the server sends back as if the player had written it, which
/// is the only log a client can show, and the simulated roster changes are pushed
/// as ordinary roster notifications so the player's screens update the way they
/// would with a real teammate.
/// </para>
/// <para>
/// The steps are spaced a second apart so the client's screens settle between
/// them, and each run starts from a clean slate: the three in-memory stores are
/// reset first, so a second run does not build on the first one's rows.
/// </para>
/// </summary>
/// <param name="teamMemoryService">Store that owns the simulated teams and members.</param>
/// <param name="characterMemoryService">Store that owns the simulated characters.</param>
/// <param name="roomPool">Service that owns the rooms a match may be hosted in.</param>
/// <param name="teamService">Service that owns the teams, real and simulated.</param>
/// <param name="memberService">Service that owns the rosters, real and simulated.</param>
/// <param name="matchmakingService">Service that pairs the teams.</param>
/// <param name="assignmentService">Service that claims a room and publishes the match.</param>
/// <param name="pushService">Service that pushes roster changes to the client.</param>
/// <param name="lobbyIdentity">Service that knows this lobby's own game type.</param>
/// <param name="sessionHelper">Helper used to write the chat lines.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class SurvivalTestService(
    EventTeamMemoryService teamMemoryService,
    CharacterMemoryService characterMemoryService,
    EventHostRoomPoolService roomPool,
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

    /// <summary>Simulated players the opponent team is formed with.</summary>
    private const int OpponentMemberCount = 2;

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
        // dropped before the first step writes anything.
        teamMemoryService.Reset();
        characterMemoryService.Reset();

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
        // host free the second time. The player re-enters through the same script.
        var previous = await matchmakingService.CancelAsync(team.Identifier, cancellationToken);
        if (previous.MatchIdentifier > 0)
        {
            await assignmentService.CancelAsync(previous.MatchIdentifier, cancellationToken);
        }

        logger.LogInformation(
            "Survival self-test started for character {CharacterIdentifier} on team {TeamIdentifier}",
            characterIdentifier,
            team.Identifier);

        // 1. The room the match is played in has to exist before anything is, and
        // it is not opened here: a room is only ever a client's own request served
        // by the create-room command. The run looks for the one it needs and names
        // the room to open when there is none, so the host a match is published
        // with is a room somebody really opened. It is not looked for in this
        // lobby alone: a room named for a host role runs the role's mode wherever
        // it was opened.
        var hostRoom = roomPool.FindHost(
            await roomPool.ListAsync(cancellationToken),
            team.MatchType,
            EventTeamService.BuildSnapshot(team).OccupiedParticipantCount() + OpponentMemberCount);
        if (hostRoom is null)
        {
            await SayAsync(
                session,
                characterIdentifier,
                $"No room can host the match. Open a dedicated room named \"{EventHostEligibilityUtils.SurvivalHostName}\" from your client, then run /test again.",
                cancellationToken);
            return;
        }

        await SayAsync(
            session,
            characterIdentifier,
            $"Room {hostRoom.Identifier} (\"{hostRoom.Name}\") is free to host the match.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 2. Pad the real team with simulated players, pending like any arrival.
        var added = AddSimulatedPlayers(team);
        await PushAddedAsync(team.Identifier, added, cancellationToken);
        await SayAsync(
            session,
            characterIdentifier,
            $"Added {added.Count} in-memory players to \"{team.Name}\" with state NG.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 3. Move the simulated players from NG to Ready.
        await ReadyAsync(team.Identifier, added, cancellationToken);
        await PushDecisionAsync(team.Identifier, added, cancellationToken);
        await SayAsync(
            session,
            characterIdentifier,
            $"In-memory players are now Ready; {added.Count} slots moved from NG.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 4. Form the simulated opponent, whose own roster starts ready because it
        // has no player behind it to make a decision.
        var opponent = teamService.CreateInMemory(
            TestPlayerNameUtils.TeamName,
            team.MatchType,
            lobbyIdentifier,
            team.EventIdentifier,
            CreateCharacters(OpponentMemberCount));
        await SayAsync(
            session,
            characterIdentifier,
            $"Created in-memory team \"{opponent.Name}\" with {opponent.Members.Count} players.",
            cancellationToken);
        await DelayAsync(cancellationToken);

        // 5. The real team is queued as it stands — the player's own slot is
        // theirs to have readied — and the opponent is registered, which is the
        // step that pairs the two.
        await matchmakingService.ReconcileAsync(team.Identifier, cancellationToken);
        var pairing = await matchmakingService.ReconcileAsync(opponent.Identifier, cancellationToken);
        await SayAsync(session, characterIdentifier, PairingMessage(team, opponent, pairing), cancellationToken);
        await DelayAsync(cancellationToken);

        // 6. Claim the room for the pairing and publish it, which is what puts the
        // match-found packets on both teams' screens.
        var assignments = pairing.Status == MatchmakingStatus.Paired
            ? await assignmentService.TryAssignWaitingAsync(lobbyIdentifier, cancellationToken)
            : [];
        await SayAsync(session, characterIdentifier, OutcomeMessage(pairing, assignments), cancellationToken);

        logger.LogInformation(
            "Survival self-test finished for character {CharacterIdentifier} on team {TeamIdentifier}: {Pairing}, {AssignmentCount} assignment(s)",
            characterIdentifier,
            team.Identifier,
            pairing.Status,
            assignments.Count);
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

    private List<Character> CreateCharacters(int count)
    {
        var characters = new List<Character>();
        for (var index = 0; index < count; index++)
        {
            characters.Add(characterMemoryService.Create());
        }

        return characters;
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
        // the one step two moves away, not the one the push may carry.
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
        var request = new ChatRequest(ChatPayloadBuilder.PublicChannelDigit, $"[test] {text}");
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.SendChatResult,
            ChatPayloadBuilder.BuildReply(characterIdentifier, request),
            cancellationToken);
    }

    private static string PairingMessage(
        EventTeam team,
        EventTeam opponent,
        MatchmakingResult pairing) =>
        pairing.Status switch
        {
            MatchmakingStatus.Paired =>
                $"In-memory team \"{opponent.Name}\" paired with \"{team.Name}\" as match {pairing.MatchIdentifier}.",
            MatchmakingStatus.Waiting =>
                $"In-memory team \"{opponent.Name}\" is queued and waiting. \"{team.Name}\" is not queued: ready your own slot and enter the event, then run /test again.",
            _ =>
                $"In-memory team \"{opponent.Name}\" was registered, but \"{team.Name}\" was already paired or gone.",
        };

    private static string OutcomeMessage(
        MatchmakingResult pairing,
        List<EventAssignment> assignments)
    {
        if (pairing.Status != MatchmakingStatus.Paired)
        {
            return "Nothing was published: both teams have to be paired before a room can be claimed.";
        }

        if (assignments.Count == 0)
        {
            return $"Match {pairing.MatchIdentifier} is paired and still waiting; no room in this lobby qualified as a host for it.";
        }

        var assignment = assignments[0];
        return $"Match {assignment.MatchIdentifier} leased room {assignment.GameIdentifier}; the match-found packets went out to both teams.";
    }

    private static List<int> FreeSlots(EventTeam team) =>
        [.. Enumerable
            .Range(1, EventConstants.TeamRosterSize - 1)
            .Where(candidate => !team.Members.Any(member => member.Slot == candidate))];

    private static Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(StepDelaySeconds), cancellationToken);
}
