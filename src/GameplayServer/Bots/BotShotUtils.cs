namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What one bot hit is made of: how much health it takes, whether it is a headshot,
/// and the height it lands at.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on <c>working-server-v2.pcapng</c>.</b> Its host writes 221 hits for
/// bots, and the damage word of an ordinary one is one of two sizes on the weapon
/// the capture's bots carry (<c>0x18</c>): <c>56.25</c> points on 78 of them and
/// <c>33.75</c>, six tenths of it, on 35, the rest spread just under the first by
/// range. They land at 140 tenths of a unit most often, then 100 and 120.
/// </para>
/// <para>
/// <b>A headshot is a hit of its own.</b> Twelve of those 221 hits are subtype
/// <c>0x12</c>, land at 140 to 200 and take the victim's whole bar, and the feed
/// credits the killer a headshot on exactly those kills and no other: twelve of the
/// capture's forty-four bot kills. Every other kill ends on an ordinary <c>0x11</c>
/// hit of 56 points that ran the last of the health out, which this host used to
/// write as <c>0x12</c> too.
/// </para>
/// </remarks>
public static class BotShotUtils
{
    /// <summary>
    /// Chance a hit is a headshot: twelve of the reference's 221 bot hits.
    /// </summary>
    public const double HeadshotChance = 0.055;

    /// <summary>
    /// Damage of the two hit sizes, in health points, in the shares the capture
    /// writes them: seven in ten on the torso, three in ten at six tenths of it.
    /// </summary>
    private static readonly int[] HitDamages = [56, 56, 56, 56, 56, 56, 56, 34, 34, 34];

    /// <summary>
    /// Heights an ordinary hit lands at above the victim's floor, in stage units:
    /// the capture's 140 on nearly half of its hits, then 100 and 120.
    /// </summary>
    private static readonly float[] BodyHeights = [600f, 600f, 600f, 600f, 600f, 200f, 200f, 200f, 400f, 400f];

    /// <summary>Height a headshot lands at above the floor: the capture's 180 tenths.</summary>
    private const float HeadHeight = 1000f;

    /// <summary>Whether the next hit is a headshot.</summary>
    /// <returns><c>true</c> on the share of hits the reference makes headshots.</returns>
    public static bool RollHeadshot() => Random.Shared.NextDouble() < HeadshotChance;

    /// <summary>The health points an ordinary hit takes.</summary>
    /// <returns>One of the capture's two sizes.</returns>
    public static int RollDamage() => HitDamages[Random.Shared.Next(HitDamages.Length)];

    /// <summary>The height a hit lands at above the victim's floor.</summary>
    /// <param name="headshot">Whether the hit is a headshot.</param>
    /// <returns>Stage units above the floor.</returns>
    public static float ImpactHeight(bool headshot) =>
        headshot ? HeadHeight : BodyHeights[Random.Shared.Next(BodyHeights.Length)];
}
