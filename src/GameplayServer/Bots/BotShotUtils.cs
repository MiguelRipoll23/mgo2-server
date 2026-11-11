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
    /// How far the point a hit lands on sits from the victim's own position along
    /// <c>x</c> and <c>z</c>, in stage units. Every one of the reference's bot hits lands
    /// a hundred wire tenths short of its victim on both axes — a victim at
    /// <c>(1755, 117, 213)</c> is hit at <c>(1655, 157, 113)</c> — and the shooter's state
    /// record names that same point.
    /// </summary>
    public const float ImpactOffset = 1000f;

    /// <summary>
    /// Height an ordinary hit lands at above the victim's position: forty wire tenths on
    /// most of the reference's hits (157 on a victim at 117, 140 on one at 100).
    /// </summary>
    public const float BodyHeight = 400f;

    /// <summary>Height a headshot lands at above the victim: 200 on a victim at 120.</summary>
    private const float HeadHeight = 800f;

    /// <summary>
    /// Height of a standing shooter's eye above its position, in stage units: the pitch
    /// the reference writes puts it 79 wire tenths up (a pitch of 126 over 1 840 tenths
    /// from a shooter at 100 to an impact at 157).
    /// </summary>
    private const float EyeHeight = 790f;

    /// <summary>Whether the next hit is a headshot.</summary>
    /// <returns><c>true</c> on the share of hits the reference makes headshots.</returns>
    public static bool RollHeadshot() => Random.Shared.NextDouble() < HeadshotChance;

    /// <summary>The health points an ordinary hit takes.</summary>
    /// <returns>One of the capture's two sizes.</returns>
    public static int RollDamage() => HitDamages[Random.Shared.Next(HitDamages.Length)];

    /// <summary>
    /// The hit one bot lands on its enemy, with the three things the reference's record
    /// says about where it came from.
    /// </summary>
    /// <remarks>
    /// <b>The record names its shooter by direction, and this host named nobody.</b> In
    /// <c>working-server-v2</c> the two bytes that close a bot's hit are the shooter's own
    /// heading, the same word its position record carries on that beat (<c>62ab</c> on
    /// both for slot 7's hit at t+42.55), and the two before them its vertical angle from
    /// eye to impact. This host wrote a zero angle, an impact on the victim's own position
    /// and a heading its bots did not hold — a bot backing off faced away from the enemy
    /// it was shooting — so a client could not walk the hit back to a player, and the one
    /// of <c>this-server.pcapng</c> stopped speaking on the beat a bot's hit killed it.
    /// </remarks>
    /// <param name="body">The shooter's body.</param>
    /// <param name="target">The enemy it hit.</param>
    /// <param name="damage">Health points the hit takes.</param>
    /// <param name="weapon">Weapon id the record carries.</param>
    /// <param name="headshot">Whether the hit is a headshot.</param>
    /// <returns>The hit.</returns>
    public static BotShot Build(BotBodyState body, BotActorView target, int damage, byte weapon, bool headshot)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(target);

        var impactX = target.X - ImpactOffset;
        var impactY = target.Y + (headshot ? HeadHeight : BodyHeight);
        var impactZ = target.Z - ImpactOffset;
        var deltaX = impactX - body.X;
        var deltaZ = impactZ - body.Z;
        var distance = MathF.Max(1f, MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ)));
        var drop = body.Y + EyeHeight - impactY;
        var pitch = (short)MathF.Round(MathF.Atan2(drop, distance) / (2f * MathF.PI) * 65536f);

        return new BotShot(
            target.SessionSlot,
            damage,
            weapon,
            impactX,
            impactY,
            impactZ,
            BotEngagementUtils.Bearing(body, target))
        {
            Headshot = headshot,
            Pitch = pitch,
        };
    }
}
