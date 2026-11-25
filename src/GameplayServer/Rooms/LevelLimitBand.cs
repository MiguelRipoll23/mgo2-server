namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's level restriction: the band a client tests a joining player's level
/// against, and whether that test is in force.
/// </summary>
/// <remarks>
/// <para>
/// It is two bytes of the room-state body and one bit of the switch word beside them:
/// the half-width at <c>0x63</c>, the centre at <c>0x64</c>, and bit <c>0x400000</c> of
/// the word at <c>0x4f</c>–<c>0x52</c>. The capture that switches the limit on and fills
/// the Create Game box with level 8 ± 2 carries <c>2</c> and then <c>8</c>, so the first
/// byte is the width and the second the centre.
/// </para>
/// <para>
/// <b>The band is written whether or not it is in force.</b> A room that leaves the
/// option off still carries its chosen band with the enable bit clear — which is the
/// state every captured room without the limit is in — so switching the limit off does
/// not discard the numbers the host picked.
/// </para>
/// <para>
/// The two bytes are the client's own: its room-state builder fills them from the
/// game-details record's <c>+0x34f</c> and <c>+0x350</c>, which are the <c>0x4313</c>
/// block's <c>level_limit_tolerance</c> and <c>level_limit_base</c>
/// (<c>docs/analysis/rotation-payload-corpus.md</c> §5a).
/// </para>
/// </remarks>
/// <param name="Tolerance">The band's half-width, in levels.</param>
/// <param name="Base">The band's centre, in levels.</param>
/// <param name="Enabled">Whether a client should apply the band.</param>
public readonly record struct LevelLimitBand(byte Tolerance, byte Base, bool Enabled)
{
    /// <summary>
    /// The highest level a player reaches, which is the ceiling a band's centre can
    /// name: the client's own experience table renders its top figure as level
    /// <c>22</c> (<c>0x16</c>), and no captured room states a centre above it.
    /// </summary>
    public const byte HighestPlayerLevel = 22;

    /// <summary>
    /// The band a body carries when the caller has none: the working capture's, with the
    /// limit switched off.
    /// </summary>
    /// <remarks>
    /// It is the reference body's band, so a caller that does not state a restriction
    /// writes the bytes the capture carries rather than a zero-width band that would
    /// read as "level 0 only" if a client ever applied it.
    /// </remarks>
    public static LevelLimitBand Disabled => new(
        (byte)RoomStateBodyConstants.LevelLimitTolerance,
        (byte)RoomStateBodyConstants.LevelLimitBase,
        Enabled: false);
}
