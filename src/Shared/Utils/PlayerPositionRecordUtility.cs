using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Reads the gameplay channel's position record: where a character is, and
/// whether it is dead.
/// </summary>
/// <remarks>
/// <para>
/// The record is a <b>tick record</b> under attribute class
/// <see cref="AttributeClass"/>, and it comes in several lengths that place the
/// same five trailing words at different offsets. All of them were placed from
/// the capture rather than carried over from a replay: for every length but 44,
/// the coordinates were confirmed by landing on the same trajectory as the
/// 16-byte form, or by agreeing with the single-precision copy the 32-byte form
/// carries alongside. The 44-byte form is <b>not</b> decoded — no offset in it
/// lands on any player's path — and <see cref="Parse"/> returns <c>null</c> for
/// it rather than a position read from the wrong bytes.
/// </para>
/// <para>
/// <b>Bit 1 of the first byte is death.</b> Fifteen thousand records carry it
/// clear and 390 carry it set, and every one of the 390 belongs to a player
/// whose health record was reading zero at that moment; no alive record sets
/// it. Those 390 are also the only records longer than 16 bytes that are not a
/// 38- or 44-byte form: a dead character is reported in the 32-byte shape, alive
/// ones in the 16-, 38- and 44-byte ones. So both the flag and the shape change
/// when a character dies.
/// </para>
/// <para>
/// Each character's position arrives under the identifier one above the one
/// carrying its health — <c>0x0080</c> health, <c>0x0081</c> position — and the
/// <c>0x800</c> bit names a second character rather than a second copy: the two
/// share no position at all.
/// </para>
/// </remarks>
public static class PlayerPositionRecordUtility
{
    /// <summary>Attribute class the position records travel under.</summary>
    public const byte AttributeClass = 0x02;

    /// <summary>Bit of the record's first byte that is set while the character is dead.</summary>
    public const byte DeadBit = 0x02;

    /// <summary>
    /// Body length of the form a character is reported in while it is alive and
    /// walking.
    /// </summary>
    public const int WalkingBodyLength = 16;

    /// <summary>Body length of the form a character is reported in while it is dead.</summary>
    public const int DeadBodyLength = 32;

    /// <summary>
    /// Offsets of the trailing five words — facing, the three coordinates in the
    /// order <c>z, y, x</c>, and the second facing — for each length the capture
    /// places. The 44-byte form is absent because nothing in it decoded.
    /// </summary>
    private static readonly Dictionary<int, (short Facing, short Z, short Y, short X, short SecondFacing)> Layouts =
        new()
        {
            [16] = (4, 6, 8, 10, 12),
            [17] = (4, 6, 8, 10, 12),
            [18] = (4, 6, 8, 10, 12),
            [32] = (20, 22, 24, 26, 28),
            [38] = (26, 28, 30, 32, 34),
        };

    /// <summary>Whether a record's attribute class marks it as a position record.</summary>
    /// <param name="attributeClass">The record's fourth header byte.</param>
    /// <returns>True for a position record.</returns>
    public static bool IsPositionRecord(byte attributeClass) => attributeClass == AttributeClass;

    /// <summary>Whether a body reports a dead character.</summary>
    /// <param name="body">Record body.</param>
    /// <returns>True when the death bit is set.</returns>
    public static bool IsDead(ReadOnlySpan<byte> body) => !body.IsEmpty && (body[0] & DeadBit) != 0;

    /// <summary>
    /// Reads where a character is.
    /// </summary>
    /// <param name="body">Record body.</param>
    /// <returns>
    /// The position, or <c>null</c> when the body is one of the forms this
    /// utility has not placed — the 44-byte form today.
    /// </returns>
    public static PlayerPosition? Parse(ReadOnlySpan<byte> body)
    {
        if (!Layouts.TryGetValue(body.Length, out var layout) || body.Length < layout.SecondFacing + 2)
        {
            return null;
        }

        return PlayerPosition.FromWire(
            ReadInt16(body, layout.X),
            ReadInt16(body, layout.Y),
            ReadInt16(body, layout.Z));
    }

    /// <summary>
    /// Reads a position and the death flag together, which is the whole of what
    /// one of these records says.
    /// </summary>
    /// <param name="body">Record body.</param>
    /// <returns>
    /// The sample, or <c>null</c> when the body's form has not been placed.
    /// </returns>
    public static PlayerPositionSample? ParseSample(ReadOnlySpan<byte> body) =>
        Parse(body) is { } position ? new PlayerPositionSample(position, IsDead(body)) : null;

    private static short ReadInt16(ReadOnlySpan<byte> body, int offset) =>
        BinaryUtility.ReadUInt16LittleEndian(body, offset) is var value && value <= short.MaxValue
            ? (short)value
            : (short)(value - 0x10000);
}

/// <summary>
/// One position update as it arrived: where the character is, and whether it
/// was dead when it said so.
/// </summary>
/// <param name="Position">Where the character is.</param>
/// <param name="IsDead">Whether the record reported the character as dead.</param>
public sealed record PlayerPositionSample(PlayerPosition Position, bool IsDead);