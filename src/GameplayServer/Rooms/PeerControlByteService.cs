using Mgo2Server.GameplayServer.MatchRecords;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Chooses the control byte a pass's record-level answers carry, which is the phase
/// the room is in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The byte is the acker's own, and it is a ladder.</b> Every <c>0xd001</c>
/// answer to a record carrying <c>0x8000</c> carries one body byte, and no value of
/// it is compared to anything: the client's handler for the record writes it to
/// state <c>0x00</c> byte <c>99</c>, two screens read that field, and each of them
/// returns on <c>0xff</c>, returns at step <c>12</c>, and otherwise moves the screen
/// to step <c>12</c> and raises script event <c>0x16002C</c> with the byte
/// (<see cref="PeerAcknowledgementUtils"/> carries the addresses). So the byte is a
/// one-shot "advance" token and which value a host writes is its own choice. The
/// sixteen captured sessions make that choice three different ways — a phase
/// ladder, a ring of eight indices walked per answer, and a value that changes with
/// every answer of a five-second beat (`docs/analysis/control-byte-findings.md`) —
/// and this host writes the ladder, in one place instead of three constants read at
/// three call sites:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>a briefing that has heard a report</b> answers <c>0x08</c>
/// (<see cref="BriefingByte"/>): `mgo2-game3` held <c>03</c> for its whole
/// briefing, from t+10.967 to t+118.218, and `mgo2-game1` alternates <c>02</c> and
/// <c>03</c> over the same stretch; `briefing-then-solo-game`, whose briefing
/// appears, writes <c>06</c> and then <c>08</c> from t+4.71;
/// </item>
/// <item>
/// <b>a round that is playing</b> answers <c>0x04</c> on its first pass and
/// <c>0x24</c> — the same code with <c>0x20</c> over it —
/// (<see cref="RoundByte"/>, <see cref="OpenedFlag"/>) once it has answered: both
/// hosts step to <c>04</c> where their round opens (`game3` t+118.218, `game1`
/// t+88.035) and `game1` writes <c>0x25</c>–<c>0x28</c> later in the round;
/// </item>
/// <item>
/// <b>a room that has not briefed yet</b> — the joining pass, and the pass a run is
/// written in — answers <c>0x06</c> (<see cref="JoinByte"/>):
/// `briefing-then-solo-game` writes <c>06</c> at t+4.71 and <c>08</c> after it, and
/// both values are this host's now. `game1` writes <c>24</c> at t+6.687 and game3 on
/// both answers of its join pass at t+8.595; that ladder is the one this host used,
/// and the capture whose briefing appears is the one it follows instead.
/// </item>
/// </list>
/// <para>
/// <b>The join byte is the working capture's, and no client compares it.</b> The
/// client's handler writes the byte to state <c>0x00</c> byte <c>99</c> and the two
/// screens that read it only test <c>0xff</c>, so the value is a host's choice and
/// the corpus holds three: <c>0x45</c> from `started-briefing-then-solo`, <c>0x06</c>
/// from `briefing-then-solo-game`, and <c>0x24</c> from `game1` and `game3`. This
/// host writes <c>0x06</c>, the value of the capture whose briefing appears, rather
/// than the <c>0x24</c> it wrote before — the failing capture
/// (`myserver.pcapng`, 2026-11-03) wrote <c>24</c> on the pass whose room never
/// briefed.
/// </para>
/// <para>
/// <b>Only a screen that has not advanced can see the value at all.</b> Both readers
/// return at step <c>12</c> without raising the event, and no host in the corpus ever
/// writes <c>0xff</c>, so a screen is moved by the first answer it receives and every
/// answer after that on the same screen changes nothing. What the ladder is for is
/// the sequence of screens: each opens below step 9 and is advanced once, and which
/// byte advances it is the habit above. That is also why the rungs are the only thing
/// worth getting right, and why the flag is on the round's later answers rather than
/// on all of them: it costs nothing either way, and it is what the two hosts that
/// show a whole session write.
/// </para>
/// <para>
/// <b>What is deliberately not reproduced: the low nibble's climb.</b> `game1`
/// answers <c>0x25</c>, <c>0x26</c>, <c>0x27</c> and <c>0x28</c> through one round,
/// which reads as a counter inside the code rather than four more phases. This host
/// writes one flagged value for every later answer of a round, because the client
/// requires no particular value (<c>0xff</c> is the only one it tests), and a counter
/// would be a value made up here instead of measured.
/// </para>
/// </remarks>
/// <param name="roundStates">The room's round, which says whether one is playing.</param>
/// <param name="matchStart">The room's briefing, which says whether one has a set yet.</param>
public sealed class PeerControlByteService(
    MatchRoundStateService roundStates,
    MatchStartRunService matchStart)
{
    /// <summary>
    /// Control byte a briefing's answers carry: the second value
    /// `briefing-then-solo-game` writes in its briefing phase, <c>0x08</c>.
    /// </summary>
    public const byte BriefingByte = 0x08;

    /// <summary>
    /// Control byte the round's code stands on: <see cref="BriefingByte"/>'s
    /// neighbours in the ladder, and the value a round answers on its first pass.
    /// </summary>
    public const byte RoundByte = 0x04;

    /// <summary>
    /// Flag the corpus puts over the round's code once the round has answered, and
    /// over the joining pass's answer.
    /// </summary>
    public const byte OpenedFlag = 0x20;

    /// <summary>
    /// Control byte a room that has not briefed yet answers: <c>0x06</c>, the value
    /// `briefing-then-solo-game` writes on its join pass and ahead of its settings
    /// burst — the capture whose briefing appears.
    /// </summary>
    public const byte JoinByte = 0x06;

    private readonly object gate = new();
    private int answeredRound = -1;
    private int answeredInRound;

    /// <summary>
    /// The control byte the answers written next carry, advancing the round's count.
    /// </summary>
    /// <remarks>
    /// <b>It advances, so it is asked once per pass.</b> A round's first answer is
    /// its code and every one after it is that code flagged, which needs the count
    /// this keeps; two reads inside one pass would spend two of the round's answers
    /// and flag the second before it is written. The count is per round — a room
    /// that opens its next round answers that one's first pass with the code again.
    /// </remarks>
    /// <returns>The control byte this pass's answers carry.</returns>
    public byte Next()
    {
        lock (gate)
        {
            if (!roundStates.IsOpen)
            {
                return matchStart.BriefingGeneration is null ? JoinByte : BriefingByte;
            }

            var round = roundStates.RoundNumber;
            if (round != answeredRound)
            {
                answeredRound = round;
                answeredInRound = 0;
            }

            var controlByte = answeredInRound == 0 ? RoundByte : (byte)(RoundByte | OpenedFlag);
            answeredInRound++;

            return controlByte;
        }
    }

    /// <summary>
    /// The control byte of the phase the room is in, without advancing the round's count.
    /// </summary>
    /// <remarks>
    /// <b>For the answers that consume no byte.</b> A record without the payload-class
    /// bit is answered with no body at all, so its answer's byte never reaches the
    /// wire; reading it from the ladder keeps the phase in one place, and reading it
    /// through <see cref="Next"/> would spend one of the round's answers — and flag the
    /// round's first body — on a value nothing sees.
    /// </remarks>
    /// <returns>The control byte the room's phase stands for.</returns>
    public byte Current()
    {
        lock (gate)
        {
            return SessionByte();
        }
    }

    /// <summary>
    /// The byte the phase alone decides, with no round counting.
    /// </summary>
    /// <returns>The phase's control byte.</returns>
    private byte SessionByte()
    {
        if (roundStates.IsOpen)
        {
            return RoundByte;
        }

        return matchStart.BriefingGeneration is null ? JoinByte : BriefingByte;
    }
}
