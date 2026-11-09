using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Keeps the room's bots: how many there are, which sides they hold and what they
/// are doing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It reconciles rather than reacts.</b> Every pass it reads the room — the
/// humans on the roster, the sides their own rows hold, the round the room is on —
/// and brings the bots in line with the settings. A reconcile is what makes
/// <c>FILL</c> work without a hook in the join path: a human who takes a slot simply
/// moves the room's human count, and the next pass gives the bot's slot up. It also
/// means a bot that was removed by hand, or a room that emptied and filled again, is
/// corrected the same way.
/// </para>
/// <para>
/// <b>A bot is a roster member and a ready state, and nothing else on the wire.</b>
/// The recorded dedicated hosts add their bots the same way: the entry appears in
/// the roster run and is ready in the record that adds it, and never sends a report
/// (<c>docs/GAMEPLAY_SERVER.md</c>, the ready-set reading). This host writes the
/// same two things and no third: a bot is not sent state records of its own, because
/// no peer answers at its endpoint, and its slot is what the lobby-slot arrays name.
/// </para>
/// <para>
/// The bots' decisions are taken at the end of the pass. They are recorded and
/// logged and are not sent; the combat and navigation systems state, with reasons,
/// what they cannot yet do.
/// </para>
/// </remarks>
public sealed partial class BotPopulationService : PeriodicWorker
{
    private readonly RoomRosterService roster;
    private readonly BotIdentityService identities;
    private readonly BotCharacterService characters;
    private readonly BotTeamAssignmentService teamAssignment;
    private readonly BotRoleAssignmentService roleAssignment;
    private readonly BriefingStateService briefings;
    private readonly MatchRoundStateService roundState;
    private readonly MatchService match;
    private readonly GameService gameService;
    private readonly BotPlayService play;
    private readonly BotSideService sides;
    private readonly BotOptions botOptions;
    private readonly ServerOptions serverOptions;
    private readonly ILogger<BotPopulationService> logger;
    private readonly Dictionary<string, BotParticipant> bots = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Serializes the passes: the worker's own and the one a join report runs, so
    /// two passes cannot seat a bot each.
    /// </summary>
    private readonly SemaphoreSlim reconcileGate = new(1, 1);

    /// <summary>
    /// Whether a real player has reached the room's briefing screen since the room
    /// last emptied, which is what a fill waits for before it seats its bots.
    /// </summary>
    /// <remarks>
    /// <b>It is a flag of the room and not a count of the current briefing.</b> The
    /// reports it is set from are cleared at every game boundary
    /// (<see cref="BriefingStateService.BeginNextBriefing"/>), and a fill gated on
    /// that count emptied the room of its bots the moment a game ended — the first
    /// pass after the clear saw no report and gave every bot's seat away while the
    /// players were still in the room. It is cleared only when the room holds no
    /// real player, so what it answers is "a player is here", which is the question
    /// a fill asks.
    /// </remarks>
    private bool playerInBriefing;

    /// <summary>Creates the population service of one gameplay host.</summary>
    /// <param name="roster">The room's roster, which owns its members.</param>
    /// <param name="identities">The one place bot names are made.</param>
    /// <param name="characters">The character rows bots play as, created when absent.</param>
    /// <param name="teamAssignment">Resolves which side a bot holds.</param>
    /// <param name="roleAssignment">Distributes the archetypes.</param>
    /// <param name="briefings">The briefing state a bot is readied in.</param>
    /// <param name="roundState">The round the room is on.</param>
    /// <param name="match">This host's own room.</param>
    /// <param name="gameService">The games domain's rows, which hold the sides.</param>
    /// <param name="play">Takes the bots' ticks.</param>
    /// <param name="sides">The slots and sides the lobby-slot arrays read.</param>
    /// <param name="botOptions">The deployment's bot settings.</param>
    /// <param name="serverOptions">This host's room settings.</param>
    /// <param name="logger">Logger of this service.</param>
    public BotPopulationService(
        RoomRosterService roster,
        BotIdentityService identities,
        BotCharacterService characters,
        BotTeamAssignmentService teamAssignment,
        BotRoleAssignmentService roleAssignment,
        BriefingStateService briefings,
        MatchRoundStateService roundState,
        MatchService match,
        GameService gameService,
        BotPlayService play,
        BotSideService sides,
        IOptions<BotOptions> botOptions,
        IOptions<ServerOptions> serverOptions,
        ILogger<BotPopulationService> logger)
        : base(
            TimeSpan.FromSeconds(Math.Max(1, botOptions.Value.PopulationIntervalSeconds)),
            logger,
            PeriodicWorker.NoBackoff)
    {
        this.roster = roster;
        this.identities = identities;
        this.characters = characters;
        this.teamAssignment = teamAssignment;
        this.roleAssignment = roleAssignment;
        this.briefings = briefings;
        this.roundState = roundState;
        this.match = match;
        this.gameService = gameService;
        this.play = play;
        this.sides = sides;
        this.botOptions = botOptions.Value;
        this.serverOptions = serverOptions.Value;
        this.logger = logger;
    }

    /// <summary>The room's bots, in no order.</summary>
    public IReadOnlyList<BotParticipant> Bots
    {
        get
        {
            lock (gate)
            {
                return [.. bots.Values];
            }
        }
    }

    /// <summary>
    /// The slots the bots hold, as the lobby-slot arrays name them.
    /// </summary>
    public IReadOnlyList<LobbyTeamSlot> TeamSlots
    {
        get
        {
            lock (gate)
            {
                return
                [
                    .. bots.Values.Select(bot => new LobbyTeamSlot(bot.SessionSlot, bot.Team)),
                ];
            }
        }
    }

    /// <summary>One pass over the room, as the worker runs it.</summary>
    /// <param name="cancellationToken">Token that cancels the pass.</param>
    protected override async Task RunOnceAsync(CancellationToken cancellationToken) =>
        await ReconcileAsync(cancellationToken);

    /// <summary>
    /// Runs a pass now and answers whether it changed the room's bots.
    /// </summary>
    /// <remarks>
    /// <b>A fill is seated on the join report, so the roster the client was answered
    /// is the one without them.</b> The report's own pass calls this and writes the
    /// room's roster again when it answered true, which is what puts the bots in the
    /// briefing the client is reading (<see cref="Commands.Peer.Room.JoinReportHandler"/>).
    /// Seating them at the profile instead was tried and taken back out: the room
    /// looked full before the client's join was heard and the game refused it.
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    /// <returns><c>true</c> when the pass seated or removed a bot.</returns>
    public async Task<bool> ReconcileNowAsync(CancellationToken cancellationToken)
    {
        await reconcileGate.WaitAsync(cancellationToken);
        try
        {
            var before = Bots.Count;
            await ReconcileCoreAsync(cancellationToken);
            return Bots.Count != before;
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    /// <summary>
    /// Brings the bots in line with the settings and the room.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    private async Task ReconcileAsync(CancellationToken cancellationToken) =>
        await ReconcileNowAsync(cancellationToken);

    /// <summary>
    /// The seats the room advertises, which is what a population is sized from.
    /// </summary>
    /// <remarks>
    /// The seats are the room's, not the channel's: a Free Battle room the client
    /// browses as eight holds eight, and a population sized from the roster's
    /// twenty-three filled seats the briefing's own list treats as unplaced. The
    /// protocol's own limit is the fallback for the window before this instance has
    /// published a room.
    /// </remarks>
    private int Capacity => match.RoomCapacity > 0
        ? match.RoomCapacity
        : Math.Min(RoomRosterService.SessionSlotCount - 1, serverOptions.GameplayServerMaxPlayers);

    /// <summary>
    /// The bots the room may hold right now: the mode's count, less a seat for every
    /// player that is still on its way in.
    /// </summary>
    /// <remarks>
    /// <b>A seat a player is about to take is not a bot's to hold.</b> A client that
    /// has sent its profile has not reached its briefing yet — it is loading the room
    /// and the map — and its report is what says it is in. Until that report the
    /// room must keep the seat free, or the fill's next pass hands it back to a bot
    /// and the client reads the room as full and refuses the join it was already
    /// answered. The subtraction is what keeps that window closed, and it ends by
    /// itself: the report takes the player out of the count.
    /// </remarks>
    /// <param name="mode">How a bot holds its slot.</param>
    /// <param name="members">The room's members.</param>
    /// <returns>The bots the room may hold.</returns>
    private int TargetFor(BotPopulationMode mode, IReadOnlyList<RosterMember> members)
    {
        var humans = members.Count(member => !member.IsBot);
        var joining = members.Count(
            member => !member.IsBot && !briefings.HasJoined(member.RemoteAddress));
        return Math.Max(0, BotPopulationUtils.TargetCount(mode, botOptions.Count, humans, Capacity) - joining);
    }

    /// <summary>Whether a real player has reached the room's briefing screen.</summary>
    private bool PlayerInBriefing
    {
        get
        {
            lock (gate)
            {
                return playerInBriefing;
            }
        }
    }

    /// <summary>
    /// Notes that a real player has reached the room's briefing screen, which is the
    /// report a client sends once its room and map have loaded.
    /// </summary>
    public void NotePlayerInBriefing()
    {
        lock (gate)
        {
            playerInBriefing = true;
        }
    }

    private async Task ReconcileCoreAsync(CancellationToken cancellationToken)
    {
        var policy = BotOptionsUtils.Team(botOptions);
        if (policy == BotTeamPolicy.None)
        {
            RemoveAll("BOT_TEAM is NONE");
            return;
        }

        var matchIdentifier = match.MatchIdentifier;
        if (matchIdentifier == 0)
        {
            return;
        }

        var members = roster.Snapshot();
        var humans = members.Where(member => !member.IsBot).ToList();
        var teams = await gameService.GetPlayerTeamsAsync(matchIdentifier, cancellationToken);
        var humanSides = humans
            .Select(human => teams.TryGetValue(human.CharacterId, out var side) ? side : (short?)null)
            .Where(RoomSideUtils.IsSide)
            .Select(side => side!.Value)
            .ToList();

        var mode = BotOptionsUtils.PopulationMode(botOptions);

        // A room that holds no real player is not a room anybody is filling, so the
        // flag the fill waits on is cleared with the last player's departure: the
        // next player to arrive walks into a room of no bots.
        if (humans.Count == 0)
        {
            lock (gate)
            {
                playerInBriefing = false;
            }
        }

        // A fill waits for the player it is filling for, on the report a client sends
        // once it has loaded the room's map — the briefing screen. The seats stay free
        // until then, so the room that client walks into is a room it can enter: a
        // population added at its first profile made the room look full before the
        // join was heard, and the game would not let the player in. It is the room's
        // own flag rather than the briefing's report count, because those reports are
        // cleared at every game boundary while the players stay.
        var waitingForPlayer = mode == BotPopulationMode.Fill && !PlayerInBriefing;

        var target = waitingForPlayer ? 0 : TargetFor(mode, members);

        var rotation = MatchSettingsUtils.Rotation(serverOptions);
        var roundNumber = roundState.RoundNumber;
        var entry = rotation[Math.Max(0, roundNumber) % rotation.Count];

        Trim(target, mode, humans.Count, waitingForPlayer);
        ResolveSides(policy, humanSides);
        await SpawnAsync(target, policy, humanSides, entry.Rule, cancellationToken);

        // A briefing begins with nobody ready, and a bot never sends the report that
        // would ready it, so its readiness is restated on every pass: the ready set
        // is what the round opens on, and a bot that were left out of it would hold
        // every round shut.
        RestateReady();

        // A briefing is not a round, and a bot does nothing in one but be ready.
        // The recorded hosts' bots are a roster entry and a ready state while the
        // room waits; a bot that chose an objective, picked a target and routed a
        // path through the briefing was writing a round's decisions into a room
        // that had not opened, which is what the local run's log shows for the
        // whole of its briefing.
        if (!roundState.IsOpen)
        {
            return;
        }

        await play.PlayAsync(
            Bots,
            matchIdentifier,
            roundNumber,
            entry.Rule,
            entry.Map,
            cancellationToken);
    }

    /// <summary>Restates every bot's readiness for the briefing that is open.</summary>
    /// <remarks>
    /// <b>A bot is a ready state and not a report.</b> The recorded hosts' bots are
    /// ready in the record that adds them and never send the map-loaded report a
    /// client sends, which is what this writes. The report was tried here once and
    /// taken back out: with the bots in the room's publication the client stopped
    /// readying at all in the run of 03:17, where at 03:06 — bots a ready state
    /// only — it readied twenty-two seconds after joining.
    /// </remarks>
    private void RestateReady()
    {
        foreach (var bot in Bots)
        {
            briefings.NoteBriefingReady(
                bot.RosterKey,
                bot.SessionSlot,
                BriefingReadyBodiesUtils.ReadyState);
        }
    }
}
