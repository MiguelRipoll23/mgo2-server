namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The five-byte weapon pair an actor carries: <c>[flag, weapon, 0, 0, ammo]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured over <c>.pcapngs/actions/</c>.</b> A normal pair is
/// <c>00 18 00 00 0F</c> — M4, fifteen rounds; the last byte falls as the
/// trigger spends the magazine and refills on the reload's last beat
/// (<c>shooting-and-reloading.pcapng</c>).
/// </para>
/// <para>
/// <b>The first byte is not always zero.</b> <c>hiding-and-moving-wearing-box</c>
/// carries <c>09 14 00 00 00</c> while the box is worn and <c>00 14 00 00 0F</c>
/// with the P90 after it is put away. The box therefore needs no pose of its own:
/// the pose is the ordinary armed crawl <c>A1 A8</c>, and the carried item is the
/// flag. The box's item id is <c>176</c> (<c>C BOX A</c>).
/// </para>
/// </remarks>
public static class WeaponPairUtils
{
    /// <summary>Length of the pair on the wire.</summary>
    public const int BodyLength = 5;

    /// <summary>Index of the carried-item flag.</summary>
    public const int FlagIndex = 0;

    /// <summary>Index of the weapon's item id.</summary>
    public const int WeaponIndex = 1;

    /// <summary>Index of the magazine's rounds.</summary>
    public const int AmmoIndex = 4;

    /// <summary>Flag of a pair carrying no extra item.</summary>
    public const byte NoFlag = 0x00;

    /// <summary>Flag of a pair carrying the cardboard box.</summary>
    public const byte CardboardBoxFlag = 0x09;

    /// <summary>
    /// Whether a weapon pair says its actor is wearing the cardboard box.
    /// </summary>
    /// <param name="pair">The five-byte pair.</param>
    /// <returns><c>true</c> when the carried-item flag is the box's.</returns>
    public static bool CarriesCardboardBox(ReadOnlySpan<byte> pair) =>
        pair.Length >= BodyLength && pair[FlagIndex] == CardboardBoxFlag;
}
