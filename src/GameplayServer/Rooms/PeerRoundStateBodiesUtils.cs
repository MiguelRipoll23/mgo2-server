namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// A peer's own round state on the <c>0x0b</c> payload family: the slot state
/// class byte, its presence map and the slot's closed-round count.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second byte is the state index, and <c>0x01</c>–<c>0x18</c> are the
/// session's twenty-four slot states.</b> The client's state dispatcher
/// (<c>0x27E8B0</c>, the module object's vtable slot 0) reads one byte, scales it
/// by four and indexes the 26-entry descriptor table at <c>0x0187E990</c>; the
/// twenty-four entries behind <c>0x01</c> share one 164-field table and 1248-byte
/// state blocks, one per session slot, and the class byte is the slot's key plus
/// one (<c>docs/analysis/udp-record-dispatch.md</c> §3.5, §3.6). So a record whose
/// second byte is <c>n</c> writes fields of the slot whose key is <c>n - 1</c>.
/// </para>
/// <para>
/// <b>The bytes between that index and the values are the presence map.</b>
/// <c>ceil(164 / 8) = 21</c> bytes, one bit a field, the field's index being the
/// bit's position; the map is followed by that field's bytes in index order. The
/// recording's map sets three bits — <c>0x18</c> in its third byte (fields
/// <c>19</c> and <c>20</c>) and <c>0x80</c> in its seventh (field <c>55</c>) — and
/// the six bytes behind it are their three values.
/// </para>
/// <para>
/// <b>Three 16-bit fields of the slot state, read out of the image.</b> The shared
/// field table is at <c>0x01229DA6</c>, ten bytes an entry; index <c>3</c>'s
/// descriptor is <c>0x012298FC</c> (164 fields, <c>0x4E0</c> block). The three
/// entries are
/// </para>
/// <list type="bullet">
/// <item><description>field <c>19</c> — state offset <c>0x38</c>, 2 bytes, flags <c>0x0025</c>;</description></item>
/// <item><description>field <c>20</c> — state offset <c>0x3A</c>, 2 bytes, flags <c>0x0025</c>;</description></item>
/// <item><description>field <c>55</c> — state offset <c>0x80</c>, 2 bytes, flags <c>0x0025</c>.</description></item>
/// </list>
/// <para>
/// With no <c>0x100</c> and no <c>0x200</c> bit set, each moves as one element of
/// its own length (<c>0x27EDBC</c>), which is the six bytes the capture carries.
/// </para>
/// <para>
/// <b>The image has no key for them, and no single writer to read one off.</b>
/// Every write of a slot field goes through the module's generic setter
/// <c>write_lobby_operation_state</c> (<c>0x74FFA8</c>), which takes the state block,
/// the offset and the width as arguments and hands them to
/// <c>P2P_WriteOperationStateField</c>; dozens of callers pass their own offset, so
/// no literal <c>0x38</c> names this field anywhere, and the field table carries
/// offsets, lengths, multipliers and flags with no key beside them
/// (<c>docs/analysis/udp-record-dispatch.md</c> §3.6). The game's own name lists
/// agree: <c>.gametools/Haven/dictionary.txt</c> and
/// <c>.gametools/gcl-vscode/data/dictionary.txt</c> hold 43 906 and the larger list
/// of script names, and neither carries a round-named slot or player state field —
/// their 172 "round" entries are <c>GROUND</c> animation names, <c>ROUND</c>,
/// <c>ROUNDS</c> and <c>STRING_ROUNDS</c>. So the three are named by their measured
/// role and their offsets, not by an invented label.
/// </para>
/// <para>
/// <b>The value is the number of rounds the room has closed.</b>
/// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> writes the record
/// forty-eight times on <c>0x1001</c>, every 1.14 s, from its first round's result
/// screens to the end of the capture: <c>01 00</c> in all three fields until
/// t+163.597, <c>02 00</c> in all three from it on, and no other change. That
/// instant is the second round's close, not its open, so the three values carry a
/// count of closed rounds and not a round number.
/// <c>different-explosives-mines-grenades-on-me</c> carries <c>01 00</c> three
/// times in its first round and
/// <c>switching-standing-crouching-prone-while-aiming-and-shooting</c> zeroes
/// before any round has closed — the same twenty-three leading bytes in each.
/// </para>
/// </remarks>
public static class PeerRoundStateBodiesUtils
{
    /// <summary>Body length on the wire, as every captured copy has it: <c>29</c>.</summary>
    public const int PayloadLength = 29;

    /// <summary>Type byte the body opens with, the settings payload type.</summary>
    public const byte PayloadType = PlayerSettingsBurstService.SettingsPayloadType;

    /// <summary>
    /// Body offset of the state index: the slot's key plus one, so the host's own
    /// slot is <c>1</c>.
    /// </summary>
    public const int StateIndexOffset = 1;

    /// <summary>Offset of the presence map, one bit a field of the slot state.</summary>
    public const int MapOffset = 2;

    /// <summary>Length of the map: <c>ceil(164 / 8)</c> bytes.</summary>
    public const int MapLength = 21;

    /// <summary>Offset of the first value, behind the map.</summary>
    public const int ValueOffset = MapOffset + MapLength;

    /// <summary>How many values the map's three set bits carry: three.</summary>
    public const int ValueCount = 3;

    /// <summary>Field index of the round count at slot-state offset <c>0x38</c>.</summary>
    public const int FirstFieldIndex = 19;

    /// <summary>Field index of the round count at slot-state offset <c>0x3A</c>.</summary>
    public const int SecondFieldIndex = 20;

    /// <summary>Field index of the round count at slot-state offset <c>0x80</c>.</summary>
    public const int ThirdFieldIndex = 55;

    /// <summary>The three fields' offsets in the slot's 1248-byte state block.</summary>
    public static readonly IReadOnlyList<int> RoundCountOffsets = [0x38, 0x3A, 0x80];

    /// <summary>
    /// The record body for one slot: its state index, the map naming the three
    /// round-count fields and the room's closed-round count in each of them.
    /// </summary>
    /// <param name="stateIndex">The slot's key plus one; the host's own slot is <c>1</c>.</param>
    /// <param name="roundsClosed">
    /// Rounds the room has closed since it was published — <c>0</c> before its first
    /// close, <c>1</c> behind it.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] Body(byte stateIndex, int roundsClosed)
    {
        var body = new byte[PayloadLength];
        body[0] = PayloadType;
        body[StateIndexOffset] = stateIndex;
        body[MapOffset + (FirstFieldIndex / 8)] = (byte)(1 << (FirstFieldIndex % 8));
        body[MapOffset + (SecondFieldIndex / 8)] = (byte)(1 << (SecondFieldIndex % 8));
        body[MapOffset + (ThirdFieldIndex / 8)] = (byte)(1 << (ThirdFieldIndex % 8));
        for (var value = 0; value < ValueCount; value++)
        {
            var offset = ValueOffset + (value * 2);
            body[offset] = (byte)(roundsClosed & 0xff);
            body[offset + 1] = (byte)((roundsClosed >> 8) & 0xff);
        }

        return body;
    }
}
