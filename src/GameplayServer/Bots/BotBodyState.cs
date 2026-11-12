namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One bot's body in the round: where it stands, where it looks, what it is
/// doing about whom, and how much health it has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>The coordinates are the stage's own units, not the wire's tenths.</b> A bot
/// is placed from <see cref="MapSpawnUtils"/> and walked in the same units the
/// stage's placements are stated in; the record builders divide by ten on the way
/// out, which is the scale the recorded position and aim records carry.
/// </para>
/// <para>
/// <b>Every field here is one the working-server capture shows the host writing
/// for a bot.</b> Health falls in the vitals record, the heading and the three
/// coordinates are the position record, the aim point is the aim record, the
/// target slot and the action are the state record, and a hit is the damage
/// record. Nothing is kept that no record carries.
/// </para>
/// </remarks>
public sealed class BotBodyState
{
    /// <summary>Slot the bot holds in the room's player table.</summary>
    public required byte SessionSlot { get; init; }

    /// <summary>Stage <c>x</c>, in stage units.</summary>
    public float X { get; set; }

    /// <summary>Stage height, in stage units — the stand height of the floor it is on.</summary>
    public float Y { get; set; }

    /// <summary>Stage <c>z</c>, in stage units.</summary>
    public float Z { get; set; }

    /// <summary>Facing, as the recorded heading word.</summary>
    public ushort Yaw { get; set; }

    /// <summary>Health the vitals record reports; zero is a corpse.</summary>
    public short Health { get; set; } = BotBodyState.MaximumVitals;

    /// <summary>
    /// Health the vitals record last reported. The working host writes a bot's
    /// vitals about once a second, and on a change, not on every beat: slot 1 of
    /// the capture writes 719 vitals records over 610 s against 15 900 position
    /// records.
    /// </summary>
    public short LastPublishedHealth { get; set; } = -1;

    /// <summary>When the vitals record may be written again, in monotonic milliseconds.</summary>
    public long NextVitalsMilliseconds { get; set; }

    /// <summary>Stamina the vitals record reports.</summary>
    public short Stamina { get; set; } = BotBodyState.MaximumVitals;

    /// <summary>Where the bot is looking, in stage units.</summary>
    public float AimX { get; set; }

    /// <summary>Height it is looking at, in stage units.</summary>
    public float AimY { get; set; }

    /// <summary>Where it is looking, in stage units.</summary>
    public float AimZ { get; set; }

    /// <summary>Slot it is engaging, or <see cref="NoTarget"/>.</summary>
    public byte TargetSlot { get; set; } = BotBodyState.NoTarget;

    /// <summary>Slot no player holds, which the state record writes beside no target.</summary>
    public const byte NoTarget = 0xff;

    /// <summary>
    /// The vitals both bytes open at, measured over the working-server capture:
    /// <c>fa fa</c> on every living actor.
    /// </summary>
    public const short MaximumVitals = 250;

    /// <summary>Whether the body is walking this tick, which the position head shows.</summary>
    public bool Moving { get; set; }

    /// <summary>
    /// Whether this beat is one a shot leaves the weapon on, which the state
    /// record writes as action <c>0x47</c> rather than the held trigger's
    /// <c>0x44</c>.
    /// </summary>
    public bool ShotThisBeat { get; set; }

    /// <summary>
    /// Whether the body is in the hit reaction the capture's <c>c1 81</c> position
    /// head marks, which lasts <see cref="BotLocomotionService.HitReactionMilliseconds"/>.
    /// </summary>
    public bool HitReacting { get; set; }

    /// <summary>When the hit reaction ends, in monotonic milliseconds.</summary>
    public long HitReactUntilMilliseconds { get; set; }

    /// <summary>
    /// How many patrol points the bot has taken so far, which is the phase of its
    /// tour of the stage.
    /// </summary>
    public int PatrolIndex { get; set; }

    /// <summary>Route it is following, if any.</summary>
    public IReadOnlyList<BotPosition> Route { get; set; } = [];

    /// <summary>Waypoint on that route it is walking towards.</summary>
    public int RouteIndex { get; set; }

    /// <summary>Destination the route was planned for, so a new one replaces it.</summary>
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>When the route may be planned again, in monotonic milliseconds.</summary>
    public long NextRouteMilliseconds { get; set; }

    /// <summary>When it may fire again, in monotonic milliseconds.</summary>
    public long NextShotMilliseconds { get; set; }

    /// <summary>
    /// Rounds left in the burst the trigger is in the middle of, which is what
    /// keeps a bot firing three rounds rather than one (<see cref="BotBurstUtils"/>).
    /// </summary>
    public int BurstRemaining { get; set; }

    /// <summary>Rounds the burst in hand was started with, which sets the pause behind it.</summary>
    public int BurstSize { get; set; }

    /// <summary>
    /// When the next <c>0x47</c> fire beat may ride, in monotonic milliseconds. The
    /// capture writes one about 1.3 times a second while engaged — slot 1 has 799
    /// in 610 s — and a hit far less often than that.
    /// </summary>
    public long NextFireBeatMilliseconds { get; set; }

    /// <summary>When it stands up again after a death, in monotonic milliseconds.</summary>
    public long RespawnMilliseconds { get; set; }

    /// <summary>
    /// When a stun wears off, in monotonic milliseconds; zero when it is not
    /// stunned. A stunned bot keeps its health — the record that knocks it out
    /// spends the stun bar and not a point of health — and holds no target and no
    /// trigger until this has passed.
    /// </summary>
    public long StunnedUntilMilliseconds { get; set; }

    /// <summary>Last time the body was advanced, in monotonic milliseconds.</summary>
    public long LastAdvanceMilliseconds { get; set; }

    /// <summary>
    /// Deaths this slot has taken in the round, which the corpse body's header
    /// counts: the capture's word at <c>+4</c> is odd and rises by two a death.
    /// </summary>
    public int Deaths { get; set; }

    /// <summary>
    /// Slot of the body that took this one's last health, or
    /// <see cref="NoTarget"/>.
    /// </summary>
    /// <remarks>
    /// <b>The victim names its killer on the beat it dies.</b> The working host
    /// keeps the victim's state record on its killer right up to the blow — slot 5
    /// writes <c>44 06 01 …</c> for six beats and stands with <c>00 ff 00</c> only
    /// after the fatal hit — so a body that dropped its target the instant it died
    /// would be one the client never saw turn on the actor that shot it.
    /// </remarks>
    public byte KillerSlot { get; set; } = BotBodyState.NoTarget;

    /// <summary>Whether the beat naming <see cref="KillerSlot"/> is still to be written.</summary>
    public bool DiedThisBeat { get; set; }

    /// <summary>Whether it is dead, which is health at zero.</summary>
    public bool IsDead => Health <= 0;
}
