using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Builds and reads the per-player scoreboard record the game publishes on the
/// roster identifier, so a host with a score of its own can write one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The record is an indexed ordinary record on low vid <c>0x001</c>, the
/// stream the roster identifiers share</b>, and its body opens on the settings
/// discriminator <c>0x0b</c> like the loadout and rotation payloads beside it.
/// What tells it apart is its length: the replay parser reads a slot of one or
/// more with a body in 23…45 as a scoreboard delta, a body in 50…58 as a loadout
/// and a slot of zero with 71 bytes or more as the room's rotation
/// (<c>docs/analysis/replay-console/rpdt.js</c>, mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>).
/// </para>
/// <para>
/// The body is <c>[0x0b, lobby slot + 1, 21-byte bit mask (LSB first), one value
/// per set bit]</c>. Each value is a little-endian <c>u16</c> except bit 1 and
/// bits 128…152, which are one byte; that rule holds on 10 135 of the 10 301
/// captured records, and with the one-byte bits counted as one byte it holds on
/// all of them. The recorded host writes a record when one of a player's numbers
/// changes and carries the new value, which is why this builds a <em>delta</em>:
/// the bits the caller says changed, and no others.
/// </para>
/// <para>
/// The named bits are the replay parser's and are measured, not inferred: kills
/// <c>5</c>, deaths <c>6</c>, score <c>8</c> and headshots <c>11</c> have been
/// seen to move with the round, and <c>152</c> and <c>153</c> carry a last-result
/// code and a streak. The rest of the mask is unnamed and this file does not
/// write it (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §4).
/// </para>
/// </remarks>
public static class MatchScoreboardFramesUtils
{
    /// <summary>
    /// Leading byte of a scoreboard body: the replay parser's
    /// <c>TYPE_SETTINGS</c> discriminator, which every settings payload opens
    /// with.
    /// </summary>
    public const byte PayloadType = 0x0b;

    /// <summary>
    /// Length of the bit mask that follows the slot byte. Its 168 bits are the
    /// only ones the parser walks, and a record that sets a bit past them has
    /// none of its values read.
    /// </summary>
    public const int MaskLength = 21;

    /// <summary>Bits the mask holds.</summary>
    public const int BitCount = MaskLength * 8;

    /// <summary>Offset of the mask within the body.</summary>
    public const int MaskOffset = 2;

    /// <summary>Offset of the first value within the body, behind the mask.</summary>
    public const int ValuesOffset = MaskOffset + MaskLength;

    /// <summary>Lengths a scoreboard body is read in, shortest first.</summary>
    public const int MinimumBodyLength = ValuesOffset;

    /// <summary>Longest body the parser reads as a scoreboard delta.</summary>
    public const int MaximumBodyLength = 45;

    /// <summary>Bit carrying a player's kills.</summary>
    public const int KillsBit = 5;

    /// <summary>Bit carrying a player's deaths.</summary>
    public const int DeathsBit = 6;

    /// <summary>Bit carrying a player's score.</summary>
    public const int ScoreBit = 8;

    /// <summary>Bit carrying a player's headshots.</summary>
    public const int HeadshotsBit = 11;

    /// <summary>Bit carrying the player's last-result code, one byte wide.</summary>
    public const int LastResultBit = 152;

    /// <summary>Bit carrying the player's streak.</summary>
    public const int StreakBit = 153;

    /// <summary>
    /// The record type a scoreboard delta travels under: the roster identifier's
    /// low stream <c>0x001</c>, which is this server's player-profile type.
    /// </summary>
    public const ushort RecordType = UdpCommandConstants.PlayerProfile;

    /// <summary>
    /// One value of a delta: the bit it belongs to and the value the record
    /// carries for it.
    /// </summary>
    /// <param name="Bit">Bit index within the mask, from zero.</param>
    /// <param name="Value">Value the record carries for that bit.</param>
    public sealed record ScoreboardValue(int Bit, int Value);

    /// <summary>
    /// One player's scoreboard delta: the lobby slot it names and the values
    /// that have changed for it.
    /// </summary>
    /// <param name="Slot">
    /// Key in the room's 24-entry player table. The body carries it plus one, so
    /// the host's own slot <c>0</c> is written as <c>0x01</c>.
    /// </param>
    /// <param name="Values">The changed values, in ascending bit order.</param>
    public sealed record ScoreboardDelta(byte Slot, IReadOnlyList<ScoreboardValue> Values);

    /// <summary>
    /// Whether a value at a bit is one byte wide rather than a little-endian
    /// <c>u16</c>. It is the parser's own rule: bit 1 and bits 128…152.
    /// </summary>
    /// <param name="bit">Bit index within the mask.</param>
    /// <returns><c>true</c> when the value is one byte.</returns>
    public static bool IsByteWide(int bit) => bit == 1 || bit is >= 128 and <= LastResultBit;

    /// <summary>Bytes a value at a bit occupies on the wire.</summary>
    /// <param name="bit">Bit index within the mask.</param>
    /// <returns>One or two.</returns>
    public static int ValueLength(int bit) => IsByteWide(bit) ? 1 : sizeof(ushort);

    /// <summary>
    /// Whether a body is a scoreboard delta: the settings discriminator, a slot
    /// of one or more, and a length in the range the parser reads.
    /// </summary>
    /// <param name="body">Body of an inbound record.</param>
    /// <returns><c>true</c> when the body says a player's score changed.</returns>
    public static bool IsScoreboard(ReadOnlySpan<byte> body) =>
        body.Length is >= MinimumBodyLength and <= MaximumBodyLength
        && body[0] == PayloadType
        && body[1] >= 1;

    /// <summary>
    /// Builds a scoreboard body from a player's changed values.
    /// </summary>
    /// <param name="delta">Slot and values to write.</param>
    /// <returns>The record body, ready to be sent under <see cref="RecordType"/>.</returns>
    public static byte[] BuildBody(ScoreboardDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(delta.Values);

        var values = delta.Values.OrderBy(value => value.Bit).ToList();
        var body = new byte[ValuesOffset + values.Sum(value => ValueLength(value.Bit))];
        body[0] = PayloadType;
        body[1] = (byte)(delta.Slot + 1);

        var offset = ValuesOffset;
        foreach (var value in values)
        {
            if (value.Bit is < 0 or >= BitCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delta),
                    value.Bit,
                    $"A scoreboard bit is a bit of the {BitCount}-bit mask.");
            }

            body[MaskOffset + (value.Bit >> 3)] |= (byte)(1 << (value.Bit & 7));

            if (IsByteWide(value.Bit))
            {
                body[offset++] = (byte)(value.Value & 0xff);
                continue;
            }

            body[offset++] = (byte)(value.Value & 0xff);
            body[offset++] = (byte)((value.Value >> 8) & 0xff);
        }

        return body;
    }

    /// <summary>
    /// Reads a scoreboard body: the slot it names and the values it carries.
    /// </summary>
    /// <remarks>
    /// A body that does not walk to its own end is refused rather than guessed
    /// at, which is the parser's own rule — it skips such a record rather than
    /// reading a length the mask and the values disagree on.
    /// </remarks>
    /// <param name="body">Body of an inbound record.</param>
    /// <param name="delta">The slot and values read, when the body holds them.</param>
    /// <returns><c>true</c> when the body is a scoreboard delta that walks.</returns>
    public static bool TryParse(ReadOnlySpan<byte> body, out ScoreboardDelta delta)
    {
        delta = null!;
        if (!IsScoreboard(body))
        {
            return false;
        }

        var mask = body.Slice(MaskOffset, MaskLength);
        var values = new List<ScoreboardValue>();
        var offset = ValuesOffset;
        for (var bit = 0; bit < BitCount; bit++)
        {
            if ((mask[bit >> 3] & (1 << (bit & 7))) == 0)
            {
                continue;
            }

            var length = ValueLength(bit);
            if (offset + length > body.Length)
            {
                return false;
            }

            var value = length == 1
                ? body[offset]
                : body[offset] | (body[offset + 1] << 8);
            values.Add(new ScoreboardValue(bit, value));
            offset += length;
        }

        if (offset != body.Length)
        {
            return false;
        }

        delta = new ScoreboardDelta((byte)(body[1] - 1), values);
        return true;
    }
}
