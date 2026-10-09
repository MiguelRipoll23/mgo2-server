using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// The half of the scripted Survival self-test that runs when the player freezes
/// their team: it forms the simulated opponent, queues it, and lets the pair
/// logic find the two.
/// <para>
/// Nothing here is scheduled. The moment a team is recorded as waiting is the
/// moment the client's own state change is being served, so the simulated
/// opponent is formed from that request rather than from a timer that would
/// have to guess when the player was ready. A team is only served once per run:
/// the expectation is consumed by the formation, so a second change of state
/// cannot invent a second opponent for a team that already has one.
/// </para>
/// <para>
/// The room the match is played in is not opened here. Pairing immediately
/// offers the match to an idle host; if none is available, the pairing waits
/// for a room to become available.
/// </para>
/// </summary>
/// <param name="characterMemoryService">Store that owns the simulated characters.</param>
/// <param name="teamService">Service that owns the teams, real and simulated.</param>
/// <param name="matchmakingService">Service that pairs the teams.</param>
/// <param name="sessionHelper">Helper used to write the chat lines.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class SurvivalTestOpponentService(
    CharacterMemoryService characterMemoryService,
    EventTeamService teamService,
    EventMatchmakingService matchmakingService,
    SessionHelper sessionHelper,
    ILogger<SurvivalTestOpponentService> logger)
{
    /// <summary>Simulated players the opponent team is formed with.</summary>
    private const int OpponentMemberCount = 2;

    private readonly Lock gate = new();
    private readonly HashSet<int> expected = [];

    /// <summary>
    /// Forgets every team that is still waiting for its simulated opponent. A
    /// run begins here, so the team a previous run armed is not served twice.
    /// </summary>
    public void Reset()
    {
        lock (gate)
        {
            expected.Clear();
        }
    }

    /// <summary>
    /// Records that the team's own state change is the moment its simulated
    /// opponent is formed.
    /// </summary>
    /// <param name="teamIdentifier">Team the run is expecting an opponent for.</param>
    public void ExpectOpponent(int teamIdentifier)
    {
        lock (gate)
        {
            expected.Add(teamIdentifier);
        }
    }

    /// <summary>
    /// Takes the expectation for one team, which is what makes a run's second
    /// half single-shot.
    /// </summary>
    /// <param name="teamIdentifier">Team being served.</param>
    /// <returns>Whether the team was expecting an opponent.</returns>
    public bool TakeExpectedOpponent(int teamIdentifier)
    {
        lock (gate)
        {
            return expected.Remove(teamIdentifier);
        }
    }

    /// <summary>
    /// Whether a team has been frozen by the queue, which is the state a team is
    /// in while it waits for an opponent and the state that takes it out of the
    /// joinable list the client offers.
    /// </summary>
    /// <param name="team">Team to test.</param>
    /// <returns>Whether the team is waiting for an opponent.</returns>
    public static bool IsFrozen([NotNullWhen(true)] EventTeam? team) =>
        team is not null && team.State == EventTeamRegistrationUtils.QueuedState;

    /// <summary>
    /// Forms the simulated opponent for a team the run expects one for, once
    /// that team is waiting in the field.
    /// <para>
    /// It is inert for every other team and for every other moment: a team that
    /// is not expecting an opponent, a team that is still joinable, and a team
    /// that has already been served all leave without writing anything, which is
    /// what lets the ordinary entry-decision command call it unconditionally.
    /// </para>
    /// </summary>
    /// <param name="session">Connection whose state change triggered the run.</param>
    /// <param name="teamIdentifier">Team the player has just frozen.</param>
    /// <param name="cancellationToken">Token that cancels the run.</param>
    public async Task RunAsync(
        TcpSession session,
        int teamIdentifier,
        CancellationToken cancellationToken)
    {
        // The run reports itself through chat lines, and a chat line is written
        // as a character's.
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            return;
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (!IsFrozen(team))
        {
            // The team is not waiting, so the player has not frozen it: the
            // expectation stays with it and the next state change is looked at.
            return;
        }

        if (!TakeExpectedOpponent(teamIdentifier))
        {
            return;
        }

        var opponent = teamService.CreateInMemory(
            TestPlayerNameUtils.TeamName,
            team.MatchType,
            team.LobbyIdentifier,
            team.EventIdentifier,
            CreateCharacters(OpponentMemberCount));
        await SayAsync(
            session,
            characterIdentifier,
            $"Formed the in-memory opponent \"{opponent.Name}\" with {opponent.Members.Count} players.",
            cancellationToken);

        // The real team is already waiting, so queueing the opponent is what
        // pairs the two: the pair logic finds both in the field it reads.
        var pairing = await matchmakingService.ReconcileAsync(opponent.Identifier, cancellationToken);
        await SayAsync(
            session,
            characterIdentifier,
            PairingMessage(team, opponent, pairing),
            cancellationToken);

        await SayAsync(
            session,
            characterIdentifier,
            OutcomeMessage(pairing),
            cancellationToken);

        logger.LogInformation(
            "Survival self-test opposed team {TeamIdentifier} with in-memory team {OpponentIdentifier}: {Pairing}",
            teamIdentifier,
            opponent.Identifier,
            pairing.Status);
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
            ChatPayloadBuilder.BuildServerReply(characterIdentifier, request),
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
                $"In-memory team \"{opponent.Name}\" is queued and waiting; \"{team.Name}\" was not in the field to be paired with.",
            _ =>
                $"In-memory team \"{opponent.Name}\" was registered, but \"{team.Name}\" was already paired or gone.",
        };

    private static string OutcomeMessage(MatchmakingResult pairing)
    {
        if (pairing.Status != MatchmakingStatus.Paired)
        {
            return "Nothing was published: both teams have to be paired before a room can be claimed.";
        }

        return $"Match {pairing.MatchIdentifier} is paired; it may still be waiting for a host.";
    }
}
