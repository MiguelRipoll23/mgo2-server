namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// Where a character is in the world: three signed coordinates in the units the
/// gameplay channel counts them in.
/// </summary>
/// <remarks>
/// <para>
/// The wire carries each coordinate as a signed 16-bit number at a tenth of
/// this unit — a capture shows a player at <c>-5000, 650, -37500</c> here and
/// <c>-500, 65, -3750</c> on the wire — so <see cref="CoordinateScale"/> is the
/// factor between the two and nothing in this record pretends otherwise.
/// </para>
/// <para>
/// Which axis is which is read off the capture rather than assumed: the
/// compressed <c>x</c> and <c>z</c> agree with the single-precision copy the
/// longer forms carry to within ten units, which is the quantisation of a
/// ten-unit step. The middle axis does <b>not</b> agree with that record's own
/// middle field — it reads 3 800 where the other reads 3 973.7, and it holds
/// still while the other moves — so which of the two is the height is
/// <b>[U]</b>. The coordinate itself is carried either way, because it moves
/// with the character and both values sit in the range a height would.
/// </para>
/// <para>
/// A spawn splits the roster into two groups about 63 000 units apart on this
/// axis, which is the two teams starting on opposite sides; that is **[V]** as a
/// measurement and **[I]** as a reading, since nothing else in the capture names
/// the two groups.
/// </para>
/// </remarks>
/// <param name="X">Position along the axis the spawns separate on.</param>
/// <param name="Y">The middle axis, which is the height on the balance of the evidence.</param>
/// <param name="Z">The remaining axis.</param>
public sealed record PlayerPosition(int X, int Y, int Z)
{
    /// <summary>
    /// Units one step of the wire's signed 16-bit coordinate is worth.
    /// </summary>
    public const int CoordinateScale = 10;

    /// <summary>
    /// Builds a position from the three signed values the wire carries.
    /// </summary>
    /// <param name="x">Coordinate along the first axis, in wire units.</param>
    /// <param name="y">Coordinate along the middle axis, in wire units.</param>
    /// <param name="z">Coordinate along the remaining axis, in wire units.</param>
    /// <returns>The position in world units.</returns>
    public static PlayerPosition FromWire(short x, short y, short z) =>
        new(x * CoordinateScale, y * CoordinateScale, z * CoordinateScale);

    /// <summary>
    /// Distance from this position to another on the axis the spawns separate
    /// on, in world units.
    /// </summary>
    /// <param name="other">Position to measure to.</param>
    /// <returns>The separation.</returns>
    public int SeparationOnX(PlayerPosition other) => Math.Abs(X - other.X);
}