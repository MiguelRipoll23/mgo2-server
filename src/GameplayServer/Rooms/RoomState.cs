namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The state a host states about its room on the gameplay channel: state
/// <c>0x00</c>'s field 0, the byte at body offset <c>0x09</c> of the 161-byte
/// room body and the one-byte value of the ten-byte state transfer.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a state and not a round.</b> The field used to be written from the
/// room's rotation index, so a client was told a round number where the reference
/// states a phase: the value repeats while a phase lasts, and a room of one plays
/// a whole round on one value.
/// </para>
/// <para>
/// <b>The values are the ones <c>.pcapngs/</c> carries.</b> Decoding every
/// gameplay datagram of those captures with <c>tools/udp_frame.py</c> and reading
/// state <c>0x00</c>'s field 0 yields exactly <c>0</c> to <c>5</c>; the three with
/// a measured meaning are <see cref="Briefing"/> (<c>2</c>, the room body of every
/// briefing), <see cref="RoundStartSolo"/> (<c>3</c>, the whole round of
/// <c>briefing-then-solo-game</c>, whose room holds one player and no bots) and
/// <see cref="RoundStart"/> (<c>4</c>, the round of every room with somebody else
/// in it). The rest keep their wire value behind an <c>Unknown</c> name, because
/// no capture says what they state.
/// </para>
/// <para>
/// <b>The round itself is not stated here.</b> The reference writes it as a
/// <c>0x1001</c> record whose body is <c>81 &lt;n&gt;</c>, one byte counting the rounds
/// played since the process started, a second or two before every round's opening
/// run: <c>game-with-bots-workingserver</c> writes <c>81 00</c>, <c>81 01</c> and
/// <c>81 02</c> for its briefing, round 1, round 2, briefing, round 1 flow — it does
/// not reset at a briefing, which is what a rotation index would do.
/// </para>
/// <para>
/// <b>The flow those captures show.</b> A room is in <see cref="Briefing"/> while
/// its briefing runs. As the briefing ends the host writes <see cref="NotReady"/>
/// and then <see cref="Transition"/>; the round opens as <see cref="RoundStart"/>,
/// or <see cref="RoundStartSolo"/> in a room of one player and no bots, and that
/// value is re-written through the round. The round's own end writes
/// <see cref="Transition"/> again in the captures that write it, in front of the
/// next round's value; the dedicated ticket-limit capture writes no field 0 at
/// its boundary at all, and holds the screens on the stopped clock instead. The
/// client's placement machine treats <c>0</c> and <c>7</c> as "wait and ask
/// again", so a room that has moved past its briefing needs a value other than
/// zero to release a joining client.
/// </para>
/// </remarks>
public enum RoomState : byte
{
    /// <summary>
    /// Body value <c>0</c>: written in front of <see cref="Transition"/> as a
    /// briefing ends, and one of the two values the client's placement machine
    /// waits on (<c>0x288160</c>: <c>if (field0 == 0 || field0 == 7) retry</c>).
    /// It states that the room is not ready for the peer to place itself, and
    /// what ends it is the next value written behind it.
    /// </summary>
    NotReady = 0,

    /// <summary>
    /// Body value <c>1</c>: the boundary both phases pass through — written as a
    /// briefing ends and as a round closes, each time in front of the next phase's
    /// value. It is a transition and not a phase: no capture holds a room on it
    /// for any stretch, and the screens that follow a round are held on the
    /// stopped clock rather than on this value
    /// (<c>rounded-ended-by-reaching-tickets-limit.pcapng</c>).
    /// </summary>
    Transition = 1,

    /// <summary>Body value <c>2</c>: the room is on its briefing screen.</summary>
    Briefing = 2,

    /// <summary>
    /// Body value <c>3</c>: a round started in a room that holds one player and no
    /// other players or bots. The round's clock is held and does not decrement in
    /// this state.
    /// </summary>
    RoundStartSolo = 3,

    /// <summary>
    /// Body value <c>4</c>: a round started with more than that one player in the
    /// room.
    /// </summary>
    RoundStart = 4,

    /// <summary>
    /// Body value <c>5</c>: not a round-end marker, and not written once per round.
    /// <c>game-with-bots-workingserver</c> writes it eleven times in its one
    /// gameplay flow — once inside the first round, then a run of ten through the
    /// second — where the round boundary itself is <see cref="Transition"/>. What it
    /// states is not established, and no capture in this repository holds one of
    /// those writes, so it keeps its wire value as its name.
    /// </summary>
    Unknown5 = 5,
}
