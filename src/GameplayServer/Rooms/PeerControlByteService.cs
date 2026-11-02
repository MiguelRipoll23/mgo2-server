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
/// one-shot "advance" token and which value a host writes is its own choice — but
/// the reference hosts do not choose freely, they choose by phase, and this is that
/// choice in one place instead of three constants read at three call sites:
/// </para>
/// <list type="bullet">
/// <item><b>a briefing that has heard a report</b> answers <c>0x03</c>
/// (<see cref="BriefingByte"/>): `mgo2-game3` holds <c>03</c> for its whole
/// briefing, from t+10.967 to t+118.218, and `mgo2-game1` alternates <c>02</c> and
/// <c>03</c> over the same stretch;</item>
/// <item><b>a round that is playing</b> answers <c>0x04</c> on its first pass and
/// <c>0x24</c> — the same code with <c>0x20</c> over it —
/// (<see cref="RoundByte"/>, <see cref="OpenedFlag"/>) once it has answered: both
/// hosts step to <c>04</c> where their round opens (`game3` t+118.218, `game1`
/// t+88.035) and `game1` writes <c>0x25</c>–<c>0x28</c> later in the round;</item>
/// <item><b>a room that has not briefed yet</b> — the joining pass, and the pass a
/// run is written in — answers <c>0x24</c> (<see cref="JoinByte"/>), which is the
/// round's code with the same flag: `game1` writes <c>24</c> at t+6.687 and game3 on
/// both answers of its join pass at t+8.595.</item>
/// </list>
/// <para>
/// <b>The join byte is the round's code flagged, and that is the reading, not a
/// coincidence.</b> <c>0x24</c> is <c>0x04 | 0x20</c>, and the bit the corpus puts
/// over a round's later answers is the bit its join answers carry. One host writes
/// something else there — `started-briefing-then-solo` writes <c>0x45</c> for its
/// join and `briefing-then-solo-game` writes <c>0x06</c> — so the value is a host's
/// choice and this host makes the one the two captures that show a whole session
/// make, rather than inventing a third.
/// </para>
/// <para>
/// <b>What is deliberately not reproduced: the low nibble's climb.</b> `game1`
/// answers <c>0x25</c>, <c>0x26</c>, <c>0x27</c> and <c>0x28</c> through one round,
/// which reads as a counter inside the code rather than four more phases. This host
/// writes one flagged value for every later answer of a round, because a client
/// nothing in the image requires a particular value from (<c>0xff</c> is the only
/// value it tests), and a counter would be a value made up here instead of measured.
/// </para>
/// </remarks>
/// <param name="roundStates">The room's round, which says whether one is playing.</param>
/// <param name="matchStart">The room's briefing, which says whether one has a set yet.</param>
public sealed class PeerControlByteService(
    MatchRoundStateService roundStates,
    MatchStartRunService matchStart)
{
    /// <summary>
    /// Control byte a briefing's answers carry.
    /// </summary>
    public const byte BriefingByte = 0x03;

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
    /// Control byte a room that has not briefed yet answers: the round's code, flagged.
    /// </summary>
    public const byte JoinByte = RoundByte | OpenedFlag;

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
