using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's round, as the two writers of the gameplay channel both need it:
/// the round opener writes it and the per-peer beat waits on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the two have to share one object.</b> Every record the beat writes is
/// measured as an offset from the round's opening run — the item-box group 2.36 s
/// after it, the vitals 27.0 s after it, the two roster records riding that
/// onset. A beat that starts when the sparse phase closes rather than when the
/// round opens spends its opening item-box groups before the client has reported
/// itself ready, so the client is told about boxes it is not yet in a round for
/// and never hears about them again. The wait belongs here because the beat runs
/// per peer and the round is the room's, and the latch is what makes the round
/// open once for a room whose peers report at different times.
/// </para>
/// <para>
/// <b>It also carries the identifier set.</b> A round plays under one generation
/// and the next round under the same set a <c>0x800</c> bit along
/// (<see cref="MatchRecordGenerationUtils"/>), so a beat that held the first set
/// for the life of the session would stream its second round under identifiers
/// its client is not on. The generation is read per frame rather than captured
/// once, so the beat moves to the next round's set on its next frame.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed partial class MatchRoundStateService(ILogger<MatchRoundStateService> logger)
{
    /// <summary>
    /// How long a peer's beat waits for the round to open before it starts
    /// without one.
    /// </summary>
    /// <remarks>
    /// The wait is bounded because the channel must not go silent: every
    /// measurement has the room reporting within a few seconds of the briefing,
    /// and a client that never reports cannot be allowed to leave a beat that
    /// writes nothing. The beat starts either way; it is the round's own records
    /// that a late report adds.
    /// </remarks>
    public const int OpenWaitMilliseconds = 30_000;

    /// <summary>How often the wait looks for the round to have opened.</summary>
    private const int PollMilliseconds = 250;

    /// <summary>
    /// How long a peer's beat waits for the round's opening run before it starts
    /// anyway.
    /// </summary>
    /// <remarks>
    /// Everything the beat writes is measured as an offset from the run, so the
    /// run has to be on the wire first: the beat's frame indices are counted
    /// from it and the item-box group rides in one frame in eight. The wait is
    /// bounded all the same — a run that is never written must not leave the
    /// channel silent — and the beat goes on to write its own frames, which are
    /// the records that keep the channel moving.
    /// </remarks>
    public const int RunWaitMilliseconds = 5_000;

    /// <summary>
    /// A wait that lasts while the peer is there, for the callers whose writes
    /// must not happen at all until the round does.
    /// </summary>
    /// <remarks>
    /// It is a large positive number and not
    /// <see cref="Timeout.Infinite"/>, which is <c>-1</c>: a countdown written
    /// against that sentinel decides it has already waited longer than it is
    /// allowed to and returns on the first pass, and a caller that then loops on
    /// the round's state spins — which is how a round's opening run came to be
    /// written two hundred thousand times in a probe.
    /// </remarks>
    public const int WaitWhilePeerIsThere = int.MaxValue;

    private readonly Lock gate = new();
    private bool isOpen;
    private bool vitalsArrived;
    private int roundNumber;
    private MatchRecordGenerationUtils generation = MatchRecordGenerationUtils.First;
    private MatchRotationEntry? roundSettings;
    private byte? lastRoundResult;
    private long? openedAt;
    private readonly HashSet<PeerSession> writtenRuns = [];

    /// <summary>Whether a round is playing, and so whether the beat's records are a round's.</summary>
    public bool IsOpen
    {
        get
        {
            lock (gate)
            {
                return isOpen;
            }
        }
    }

    /// <summary>Rounds this room has opened, counting the one that may be playing.</summary>
    public int RoundNumber
    {
        get
        {
            lock (gate)
            {
                return roundNumber;
            }
        }
    }

    /// <summary>
    /// The identifier set the round now playing is written under, or the first
    /// set while no round is open.
    /// </summary>
    public MatchRecordGenerationUtils Generation
    {
        get
        {
            lock (gate)
            {
                return generation;
            }
        }
    }

    /// <summary>
    /// When the round now playing opened, or <c>null</c> while none is open. It
    /// is what the round clock is measured from.
    /// </summary>
    public DateTimeOffset? OpenedAt
    {
        get
        {
            lock (gate)
            {
                return openedAt is { } milliseconds
                    ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                    : null;
            }
        }
    }

    /// <summary>
    /// Whether a player has reported vitals since the round opened: the record
    /// the beat's vitals onset is gated on.
    /// </summary>
    /// <remarks>
    /// <b>The onset is a record and not a frame count.</b> The capture places the
    /// beat's vitals record 818 frames — 27.0 s — after the round's opening run,
    /// because that is when its host had vitals to write; a host that counted the
    /// frames instead writes the record on a clock, whether or not it has
    /// anything in it. Measured, the onset is where the round's first player
    /// health arrives (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §5), and this is
    /// the record of it.
    /// </remarks>
    public bool VitalsArrived
    {
        get
        {
            lock (gate)
            {
                return vitalsArrived;
            }
        }
    }

    /// <summary>
    /// The round now playing and the clock it opened on, or <c>null</c> while
    /// none is open.
    /// </summary>
    public MatchRotationEntry? RoundSettings
    {
        get
        {
            lock (gate)
            {
                return roundSettings;
            }
        }
    }

    /// <summary>
    /// Units of the round clock still to run, in the stream's own 125 ms ticks,
    /// counted from the round's own rule time and the moment it opened.
    /// </summary>
    /// <remarks>
    /// <b>The clock is the round's, and it counts.</b> A recorded host writes the
    /// round's remaining time under the operations clock opcode and moves it by a
    /// tick every 125 ms; the two captured rooms that play a round through both do
    /// it, from 3 360 and from 2 400, and a host that wrote the opening value for
    /// the life of the round would show its clients a timer that never moves — the
    /// shape of a replayed byte rather than a running round.
    /// <para>
    /// It is zero while no round plays, because there is no clock to read rather
    /// than because the round has run out.
    /// </para>
    /// </remarks>
    public int RemainingUnits
    {
        get
        {
            lock (gate)
            {
                return roundSettings is { } settings && openedAt is { } opened
                    ? RoundClockUtils.RemainingUnits(
                        settings,
                        opened,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    : 0;
            }
        }
    }

    /// <summary>
    /// Opens a round for the room, under the identifier set the report that
    /// asked for it arrived on and on the round the room's own rotation names.
    /// </summary>
    /// <remarks>
    /// Opening a round that is already open is not an error and does nothing to
    /// the one playing: a client re-reports until it is answered, so the second
    /// report of a round must not open a second round. That is why the round
    /// number only moves here, out of the closed state.
    /// <para>
    /// The round's rule, map and clock are carried here rather than read by each
    /// writer, because the two writers of the channel need the same ones: the
    /// opener writes the clock into the round's opening run and the beat counts it
    /// down, and a room whose rule was read twice is a room two records disagree
    /// about.
    /// </para>
    /// </remarks>
    /// <param name="requested">The set the reporting client is on.</param>
    /// <param name="round">The round the room's rotation names.</param>
    /// <returns>
    /// The round that is now playing, or <c>null</c> when one already was and
    /// this call changed nothing.
    /// </returns>
    public int? Open(MatchRecordGenerationUtils requested, MatchRotationEntry round)
    {
        ArgumentNullException.ThrowIfNull(round);

        lock (gate)
        {
            if (isOpen)
            {
                logger.LogDebug(
                    "Round {RoundNumber} is already open; the report does not open another",
                    roundNumber);
                return null;
            }

            generation = requested;
            roundSettings = round;
            roundNumber++;
            isOpen = true;
            vitalsArrived = false;
            openedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            writtenRuns.Clear();
            logger.LogInformation(
                "Round {RoundNumber} opened under identifier set 0x{Identifier:x4} "
                + "(operations 0x{Operations:x4}, payload 0x{Payload:x4}, item boxes 0x{ItemBox:x4}) "
                + "on rule {Rule}, map {Map}, {TimeLimitMinutes} minutes ({TimeLimitUnits} clock units)",
                roundNumber,
                requested.MatchStartPayload,
                requested.Operations,
                requested.MatchStartPayload,
                requested.ItemBox,
                round.Rule,
                round.Map,
                round.TimeLimitMinutes,
                round.TimeLimitUnits);
            return roundNumber;
        }
    }

    /// <summary>
    /// Ends the round now playing and keeps the result it ended on. Nothing is
    /// written here: the opener that owns the transition does that, and the
    /// writers that publish the result read it from here.
    /// </summary>
    /// <param name="roundResult">
    /// The result code the round ended on, in the operations state block's own
    /// coding (<see cref="MatchRecordBodiesUtils.StateResultTeamZero"/>), or
    /// <c>null</c> when this host has no result to write for it.
    /// </param>
    /// <returns>The round that ended, or <c>null</c> when none was playing.</returns>
    public int? Close(byte? roundResult)
    {
        lock (gate)
        {
            if (!isOpen)
            {
                return null;
            }

            isOpen = false;
            lastRoundResult = roundResult;
            roundSettings = null;
            openedAt = null;
            logger.LogInformation(
                "Round {RoundNumber} ended{Result}",
                roundNumber,
                roundResult is { } result ? $" with result code {result}" : " with no result to write");
            return roundNumber;
        }
    }

    /// <summary>
    /// Notes that a player has reported vitals in the round now playing, which
    /// reaches the beat's vitals onset.
    /// </summary>
    /// <remarks>
    /// A round is the unit: the onset is the round's own record, so it is cleared
    /// when the next round opens rather than when a peer joins or leaves.
    /// </remarks>
    public void NoteVitalsArrived()
    {
        lock (gate)
        {
            if (vitalsArrived)
            {
                return;
            }

            vitalsArrived = true;
            logger.LogInformation(
                "A player in round {RoundNumber} has reported vitals; the beat's vitals record rides from "
                + "this frame on",
                roundNumber);
        }
    }

    /// <summary>
    /// Notes that the round's opening run has been written to a peer.
    /// </summary>
    /// <param name="session">Session the run was written to.</param>
    public void NoteRunWritten(PeerSession session)
    {
        lock (gate)
        {
            writtenRuns.Add(session);
        }
    }
}
