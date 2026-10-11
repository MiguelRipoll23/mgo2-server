using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Owns the room's rounds: which round is being played, the identifier set it
/// plays under, and when the next one opens. One peer no longer opens a round on
/// its own — the room opens it, once every peer in it has reported itself ready,
/// and every peer's writer then writes that round's run. This is what makes a
/// solo round and a joined round the same round rather than two streams.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room opens the round, not a clock and not one peer.</b> The capture is
/// the shape: game1's first round opens at t+46.4 s on the solo player's report
/// and its second at t+194.0 s — the measured 147.6 s boundary — once the player
/// who joined at t+154.3 s has also reported. A round that opens on a timer
/// writes a run the client never asked for; a round that opens per peer gives
/// two players two different rounds and two different identifier sets.
/// </para>
/// <para>
/// <b>A round is due, then it waits for readiness.</b> The first round is due as
/// soon as the room holds a peer; each later round is due a
/// <see cref="MatchRecordFramesUtils.RoundLengthMilliseconds"/> after the last
/// one opened. A due round opens only when every peer in the room has a pending
/// readiness report, which is what keeps a match-start run off a client still on
/// its loading screen. The reports are taken when the round opens, so a peer
/// that reports again while the round is going out queues exactly one report for
/// the round after it, the way the capture's client reports once per round.
/// </para>
/// <para>
/// <b>A peer that joins mid-round joins the round.</b> The round it entered on
/// is remembered, so its writer beats the round in progress and writes the
/// match-start run only for the next one — which is what the capture shows when
/// the second player's records keep the first generation until t+194 s.
/// </para>
/// </remarks>
/// <param name="ready">Pending readiness reports of the room's peers.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRoundService(
    PlayerReadyService ready,
    ILogger<MatchRoundService> logger)
    : PeriodicWorker(
        TimeSpan.FromMilliseconds(RoundTickMilliseconds),
        logger,
        PeriodicWorker.NoBackoff)
{
    /// <summary>
    /// How often the room checks whether its next round is due. It is a
    /// readiness poll rather than a schedule: the round's own length is measured
    /// in <see cref="MatchRecordFramesUtils.RoundLengthMilliseconds"/> and this
    /// only bounds how late the check can be by 50 ms.
    /// </summary>
    private const int RoundTickMilliseconds = 50;

    private readonly Lock gate = new();
    private readonly HashSet<PeerSession> peers = [];
    private readonly Dictionary<PeerSession, int> joinRounds = [];
    private int maximumRounds;
    private int currentRound;
    private MatchRecordGenerationUtils generation = MatchRecordGenerationUtils.First;
    private long roundOpenedAtMilliseconds;
    private long version;
    private bool matchFinished;

    /// <summary>
    /// Sets the room's round limit, which comes from the rules the room
    /// advertises. Zero or less means the room plays on, which is the shape every
    /// capture has: its rounds end on a goal or a boundary and nothing stops it.
    /// </summary>
    /// <param name="rounds">Rounds the room plays, or zero for no limit.</param>
    public void Configure(int rounds)
    {
        lock (gate)
        {
            maximumRounds = rounds;
        }
    }

    /// <summary>
    /// Adds a peer to the room and remembers the round it entered on, so a peer
    /// that joins mid-round is not handed the run for the round in progress.
    /// </summary>
    /// <param name="session">Peer whose records are starting.</param>
    public void Enter(PeerSession session)
    {
        lock (gate)
        {
            peers.Add(session);
            joinRounds[session] = currentRound;
        }
    }

    /// <summary>
    /// Removes a peer from the room, so it stops holding a round open. When the
    /// last peer leaves, the match it was in is over: the next peer to join
    /// starts a fresh round one rather than waiting out a boundary it never saw.
    /// </summary>
    /// <param name="session">Peer whose records have stopped.</param>
    public void Leave(PeerSession session)
    {
        lock (gate)
        {
            peers.Remove(session);
            joinRounds.Remove(session);

            if (peers.Count == 0)
            {
                currentRound = 0;
                generation = MatchRecordGenerationUtils.First;
                roundOpenedAtMilliseconds = 0;
                version++;
                matchFinished = false;
            }
        }
    }

    /// <summary>
    /// The round a peer entered on: zero before the first round opens, and the
    /// round in progress for a peer that joined while one was being played.
    /// </summary>
    /// <param name="session">Peer to ask about.</param>
    /// <returns>The round number the peer entered on.</returns>
    public int EnteredOnRound(PeerSession session)
    {
        lock (gate)
        {
            return joinRounds.GetValueOrDefault(session);
        }
    }

    /// <summary>The round the room is playing, read by every peer's writer.</summary>
    public MatchRoundSnapshot Current
    {
        get
        {
            lock (gate)
            {
                return Snapshot();
            }
        }
    }

    /// <inheritdoc />
    protected override Task RunOnceAsync(CancellationToken cancellationToken)
    {
        MatchRoundSnapshot? opened = null;
        var peersAtOpen = 0;
        var finishedNow = false;

        lock (gate)
        {
            if (peers.Count == 0)
            {
                return Task.CompletedTask;
            }

            var now = Environment.TickCount64;

            if (maximumRounds > 0 && currentRound >= maximumRounds)
            {
                // The room has played its last round. The round keeps playing
                // out for whoever is in it, and once its own boundary has elapsed
                // nothing new opens; the end is reported once rather than on
                // every tick.
                if (matchFinished
                    || now - roundOpenedAtMilliseconds < MatchRecordFramesUtils.RoundLengthMilliseconds)
                {
                    return Task.CompletedTask;
                }

                matchFinished = true;
                finishedNow = true;
            }
            else
            {
                var due = currentRound == 0
                    || now - roundOpenedAtMilliseconds >= MatchRecordFramesUtils.RoundLengthMilliseconds;
                if (!due)
                {
                    return Task.CompletedTask;
                }

                // The barrier: a due round waits for every peer in the room. A
                // peer on its loading screen has not reported, and the round it
                // would be dropped into is not opened on it.
                foreach (var peer in peers)
                {
                    if (!ready.HasReport(peer))
                    {
                        return Task.CompletedTask;
                    }
                }

                currentRound++;
                generation = currentRound == 1
                    ? MatchRecordGenerationUtils.First
                    : generation.Sibling;
                roundOpenedAtMilliseconds = now;
                version++;

                // Taken, not just read: a peer that reports again while this
                // round goes out queues one report for the round after it. The
                // byte each report carried stays behind for the run to echo.
                foreach (var peer in peers)
                {
                    ready.TryTake(peer, out _);
                }

                opened = Snapshot();
                peersAtOpen = peers.Count;
            }
        }

        if (finishedNow)
        {
            logger.LogInformation(
                "The room played all {MaximumRounds} round(s) the room's rules allow; no further round opens",
                maximumRounds);
            return Task.CompletedTask;
        }

        if (opened is { } open)
        {
            logger.LogInformation(
                "Opening round {RoundNumber} of {MaximumRounds} for {PeerCount} peer(s) under "
                + "(operations 0x{Operations:x4}, rules 0x{Rules:x4}, vitals 0x{Vitals:x4}, marker 0x{Marker:x4}) "
                + "because every peer has reported itself ready",
                open.Number,
                maximumRounds > 0 ? maximumRounds : 0,
                peersAtOpen,
                open.Generation.RoundClock,
                open.Generation.RulesRoster,
                open.Generation.Vitals,
                open.Generation.MatchStartMarker);
        }

        return Task.CompletedTask;
    }

    /// <summary>Reads the room's state. The caller holds <see cref="gate"/>.</summary>
    private MatchRoundSnapshot Snapshot() =>
        new(version, currentRound, generation, maximumRounds);
}
