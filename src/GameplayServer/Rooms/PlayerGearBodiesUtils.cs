namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The per-player settings body a reference host writes for the other players of
/// the room: the one that carries a player's gear, numbers and look behind that
/// player's key.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a second body per player, not the loadout record's replacement.</b>
/// `pcapng1` writes for each key both a fifty-byte <c>0b SS 02 …</c> body — the
/// shape <see cref="LoadoutFramesUtils"/> fills — and one of these, sixty-one to
/// ninety-one bytes long, opening <c>0b SS 62/42/60</c> with the gear list behind
/// a <c>50 02</c> or <c>53 02</c> marker. Both are keyed by the player and both are
/// written in the join burst (t+3.63), so a room that writes only the first tells
/// each client nothing about the gear of anyone else in it.
/// </para>
/// <para>
/// <b>Four fields are written, and everything else is the recorded template's.</b>
/// The slot (byte <c>0x01</c>), the thirteen bytes of the two stats blocks
/// (<see cref="StatsRegionOffset"/>), and the appearance block (the body's
/// trailing twenty-three bytes). No capture names what the bytes between the gear
/// marker and the stats blocks hold, and the region's length differs between the
/// recorded bodies (<c>ff</c> two bytes before the appearance block, three, and
/// three again), so nothing there is written.
/// </para>
/// <para>
/// <b>The level is a field, and it is the byte twenty-four from the body's end.</b> The client
/// shows twice that byte: the recorded host's own body carries `0x05` there and its briefing
/// showed 10, while this template carries `0x07` and every row of the 2026-11-09 13:25 screen
/// showed 14. <see cref="Build(byte, ReadOnlySpan{byte}, PlayerStats, int?)"/> therefore writes
/// the half of the level its caller states, and keeps the recorded one when the room can state
/// none.
/// </para>
/// <para>
/// <b>The gear list is the recorded template's, and that is a known limit.</b>
/// Nothing captured maps a character's saved gear onto that list, so a player
/// announced here carries the template's three gear slots. The alternative — a
/// list built from a guess — would put another player in gear nothing states.
/// </para>
/// </remarks>
public static class PlayerGearBodiesUtils
{
    /// <summary>Body length of the recorded template, as measured.</summary>
    public const int BodyLength = 70;

    /// <summary>Offset of the player's slot byte, which is the player's key plus one.</summary>
    public const int SlotOffset = 0x01;

    /// <summary>Length of the appearance block this body ends with.</summary>
    public const int AppearanceLength = LoadoutFramesUtils.AppearanceLength;

    /// <summary>
    /// Offset of the body's two stats blocks, behind the gear list's marker and its
    /// count byte.
    /// </summary>
    /// <remarks>
    /// <b>The briefing table's three columns read these values.</b> Each block is
    /// five little-endian <c>u16</c>s, and the recorded template's carry its own
    /// player's figures, `8`, `10`, `4`, `2`, `2`. While they were copied as
    /// recorded, every row of the table showed the same `009 010 0004` — the screen
    /// of 2026-11-09 13:25 — because every player's body was built from that one
    /// template. The last two columns match the template (`10`, `4`) and the first
    /// reads one above it (`9` against `8`), which nothing measured so far explains;
    /// the builder clears the whole region and writes the caller's figures into it,
    /// so neither the template's numbers nor that discrepancy can survive.
    /// </remarks>
    public const int StatsRegionOffset = 24;

    /// <summary>Length of one stats block: five two-byte values.</summary>
    public const int StatsBlockLength = 10;

    /// <summary>Stats blocks the body carries: the round's and the character's.</summary>
    public const int StatsBlockCount = 2;

    /// <summary>Offset of a player's kills within a stats block.</summary>
    public const int KillsOffsetInBlock = 0;

    /// <summary>Offset of a player's deaths within a stats block.</summary>
    public const int DeathsOffsetInBlock = 2;

    /// <summary>Offset of a player's points within a stats block.</summary>
    public const int PointsOffsetInBlock = 4;

    /// <summary>
    /// Offset of the level byte, counted back from the body's end: twenty-four.
    /// </summary>
    /// <remarks>
    /// <b>The client shows twice this byte, and that is what two screens measure.</b>
    /// The recorded host's own fifty-byte body carries <c>0x05</c> at the same
    /// distance from its end and its briefing showed **10**; this template carries
    /// `0x07` and the screen of 2026-11-09 13:25 shows **14** on every row, because
    /// every body was built from it. So the figure is twice the byte, and an odd
    /// level is not a value this byte can state exactly — it is written rounded
    /// down (`docs/protocol/UDP_COMMANDS.md`, “Where LEVEL and GRADE are not”).
    /// </remarks>
    public const int LevelByteFromEnd = 24;

    /// <summary>
    /// A player's figures in the body's two stats blocks.
    /// </summary>
    /// <param name="Kills">Kills the player has been credited with.</param>
    /// <param name="Deaths">Times the player has been recorded dead.</param>
    /// <param name="Points">Points the round has paid the player.</param>
    public sealed record PlayerStats(int Kills, int Deaths, int Points)
    {
        /// <summary>The figures every player starts a round with.</summary>
        public static PlayerStats None { get; } = new(0, 0, 0);
    }

    /// <summary>
    /// The seventy-byte body `pcapng1` writes for the player in key <c>0x0e</c> at
    /// t+3.63, which is the shape its three three-slot bodies carry.
    /// </summary>
    private const string TemplateHex =
        "0b0e6205000000800000b00200000040000000000050020108000a00040002000200"
        + "08000a00040002000200fff7071c44a0563f666f000000001b0101111600141514130001";

    /// <summary>
    /// One player's settings body, with the player starting the round at no kills,
    /// no deaths and no points.
    /// </summary>
    /// <param name="slot">The player's key plus one, as the loadout record carries it.</param>
    /// <param name="appearance">
    /// The player's twenty-three-byte appearance block, or a block of another
    /// length, which leaves the recorded one in place.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] Build(byte slot, ReadOnlySpan<byte> appearance) =>
        Build(slot, appearance, PlayerStats.None);

    /// <summary>
    /// One player's settings body: the recorded template with the player's own slot,
    /// figures and appearance written into it.
    /// </summary>
    /// <param name="slot">The player's key plus one, as the loadout record carries it.</param>
    /// <param name="appearance">
    /// The player's twenty-three-byte appearance block, or a block of another
    /// length, which leaves the recorded one in place.
    /// </param>
    /// <param name="stats">The player's kills, deaths and points.</param>
    /// <param name="level">
    /// The player's character level, or <c>null</c> to keep the recorded one. It is
    /// written as its half, which is the unit the client shows
    /// (<see cref="LevelByteFromEnd"/>).
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] Build(
        byte slot,
        ReadOnlySpan<byte> appearance,
        PlayerStats stats,
        int? level = null)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var body = Convert.FromHexString(TemplateHex);
        body[SlotOffset] = slot;

        // Cleared first, so the two values per block nothing names start at zero as
        // well, and then the three named fields are written over both blocks.
        body.AsSpan(StatsRegionOffset, StatsBlockLength * StatsBlockCount).Clear();
        for (var block = 0; block < StatsBlockCount; block++)
        {
            var offset = StatsRegionOffset + (block * StatsBlockLength);
            WriteValue(body, offset + KillsOffsetInBlock, stats.Kills);
            WriteValue(body, offset + DeathsOffsetInBlock, stats.Deaths);
            WriteValue(body, offset + PointsOffsetInBlock, stats.Points);
        }

        if (level is { } stated)
        {
            body[body.Length - LevelByteFromEnd] =
                (byte)Math.Clamp(stated / 2, 0, byte.MaxValue);
        }

        if (appearance.Length == AppearanceLength)
        {
            appearance.CopyTo(body.AsSpan(body.Length - AppearanceLength));
        }

        return body;
    }

    /// <summary>Writes one unsigned two-byte value little-endian, saturated to its width.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the value's low byte.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteValue(byte[] body, int offset, int value)
    {
        var clamped = Math.Clamp(value, 0, ushort.MaxValue);
        body[offset] = (byte)(clamped & 0xff);
        body[offset + 1] = (byte)((clamped >> 8) & 0xff);
    }
}
