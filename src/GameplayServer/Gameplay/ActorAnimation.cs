namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The upper-body animation an actor's pose byte carries in its bits
/// <c>0x02</c> to <c>0x40</c>, beside <see cref="ActorPoseUtils.ArmedBit"/> and
/// <see cref="ActorPoseUtils.ActingBit"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured over <c>.pcapngs/actions/</c>.</b> The value is the animation and
/// not the stance: the same id plays from a standing, crawling or lying head, so
/// the record keeps it apart from <see cref="ActorStance"/>. <c>shooting-and-reloading</c>
/// writes <c>81 02</c> for an aim, <c>switching-between-weapons</c> writes
/// <c>21 12</c> as the weapon changes, <c>magazine-to-teammate</c> writes
/// <c>41 30</c>, and <c>stun-by-throwing-to-ground</c> writes <c>01 1B</c> for the
/// body it puts on the floor.
/// </para>
/// <para>
/// <b>The armed bit is not part of the animation.</b> The box capture's
/// <c>A1 A8</c> is the crawl animation <c>0x28</c> with <c>0x80</c> set, not a
/// second box id, which is why the box needs no member here.
/// </para>
/// <para>
/// Values the captures write but no one action owns — <c>0x06</c>, <c>0x15</c>,
/// <c>0x16</c>, <c>0x22</c>, <c>0x2B</c> — are left out rather than guessed at;
/// decoding keeps their byte, and <see cref="ActorPose"/> states an unnamed
/// animation as its raw value.
/// </para>
/// </remarks>
public enum ActorAnimation : byte
{
    /// <summary>No animation: the pose byte <c>00</c>, <c>01</c> or <c>81</c>.</summary>
    None = 0x00,

    /// <summary>Aiming: <c>41 02</c> walking, <c>81 02</c> prone, <c>A1 02</c> crawling.</summary>
    Aim = 0x02,

    /// <summary>The first beat of a ground throw's landing, on a prone body: <c>81 03</c>.</summary>
    SlamProne = 0x03,

    /// <summary>The landing behind it, on a lying body: <c>01 04</c>.</summary>
    SlamLying = 0x04,

    /// <summary>
    /// Changing weapon: <c>21 12</c> standing, <c>01 12</c> on the floor. Written by
    /// <c>switching-between-weapons.pcapng</c> as the pair's weapon byte changes.
    /// </summary>
    WeaponSwitch = 0x12,

    /// <summary>A melee blow with a weapon: <c>41 18</c> in the hitting capture.</summary>
    MeleeBlow = 0x18,

    /// <summary>The follow-through behind it: <c>41 19</c>.</summary>
    MeleeFollowThrough = 0x19,

    /// <summary>
    /// The stun family's animation: <c>C1 1A</c> knocked down, <c>E1 1A</c>
    /// recovering, <c>81 1A</c> prone. The knife and CQC stun captures write it.
    /// </summary>
    Stun = 0x1a,

    /// <summary>
    /// A CQC throw to the ground: <c>01 1B</c> lying for the whole knock-out —
    /// 13.8 s in <c>stun-by-throwing-to-ground</c>, t+60.07 to 73.92 — then
    /// <c>61 1B</c> crouching and <c>41 1B</c> walking out of it.
    /// </summary>
    GroundThrow = 0x1b,

    /// <summary>
    /// The sleep device's lying form: <c>01 1C</c>. It is the one animation a
    /// non-lethal blast leaves, which is why a sleep is not a corpse.
    /// </summary>
    Sleep = 0x1c,

    /// <summary>A knife blow's stun: <c>01 1E</c>.</summary>
    KnifeStun = 0x1e,

    /// <summary>A punch or kick: <c>21 1F</c> standing, <c>41 1F</c> walking.</summary>
    Strike = 0x1f,

    /// <summary>
    /// Dragging itself along the floor, or the box carried at a crawl:
    /// <c>A1 28</c> and the armed <c>A1 A8</c>.
    /// </summary>
    Crawl = 0x28,

    /// <summary>
    /// Handing a magazine over: <c>41 30</c> walking, <c>A1 30</c> crawling,
    /// <c>E1 30</c> recovering.
    /// </summary>
    MagazineHandover = 0x30,

    /// <summary>The CQC throw-to-ground wind-up on the thrower: <c>21 33</c>.</summary>
    ThrowWindup = 0x33,

    /// <summary>The choke the strangling capture writes: <c>01 35</c>, <c>61 35</c>, <c>E1 35</c>.</summary>
    Choke = 0x35,

    /// <summary>The hold behind the choke: <c>01 36</c>, <c>21 36</c>, <c>61 36</c>.</summary>
    ChokeHold = 0x36,

    /// <summary>The grab that opens a CQC hold: <c>41 37</c>.</summary>
    CqcHold = 0x37,

    /// <summary>The throw out of a CQC hold: <c>41 3A</c>, <c>01 3A</c>, <c>61 3A</c>.</summary>
    CqcThrow = 0x3a,
}
