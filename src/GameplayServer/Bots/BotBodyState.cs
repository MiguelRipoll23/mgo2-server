using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.Shared.Types;

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

    /// <summary>
    /// Weapon id it carries, chosen once from its role's set and written to the
    /// weapon pair and every hit (<see cref="BotWeaponLoadoutUtils"/>). It is a
    /// <see cref="WeaponIdentifier"/> because that is the table the client loads a
    /// carried weapon's model from; the item table is not interchangeable. Damage
    /// records cross to <see cref="ItemIdentifier"/> through
    /// <see cref="BotDamageWeaponUtils"/>.
    /// </summary>
    public WeaponIdentifier Weapon { get; set; } = WeaponIdentifier.M4;

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

    /// <summary>
    /// Whether the body is asleep on the floor: a non-lethal device or a
    /// tranquilliser emptied its stamina bar and it has not got up yet. A sleeping body keeps its health,
    /// its position and the capture's lying pose; it is not a corpse and carries
    /// no corpse body.
    /// </summary>
    public bool Sleeping { get; set; }

    /// <summary>When a sleeping body stands up, in monotonic milliseconds.</summary>
    public long SleepUntilMilliseconds { get; set; }

    /// <summary>Last time stamina recovery was ticked, in monotonic milliseconds.</summary>
    public long LastStaminaRecoveryMilliseconds { get; set; }

    /// <summary>Fractional stamina recovery carried between ticks, in point-milliseconds.</summary>
    public long StaminaRecoveryCredit { get; set; }

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
    public const short MaximumVitals = ActorHealthUtils.MaximumHealth;

    /// <summary>Whether the body is walking this tick, which the position head shows.</summary>
    public bool Moving { get; set; }

    /// <summary>
    /// Whether this beat is one a shot leaves the weapon on, which the state
    /// record writes as action <c>0x47</c> rather than the held trigger's
    /// <c>0x44</c>.
    /// </summary>
    public bool ShotThisBeat { get; set; }

    /// <summary>
    /// Whether the bot held its trigger on the previous tick. The beat it stops
    /// firing is the one the capture closes on <c>48 FF 00</c>, which
    /// <see cref="RecoverUntilMilliseconds"/> then holds.
    /// </summary>
    public bool WasFiring { get; set; }

    /// <summary>
    /// Whether the body is in the hit reaction the capture's <c>c1 81</c> position
    /// head marks, which lasts <see cref="BotLocomotionService.HitReactionMilliseconds"/>.
    /// </summary>
    public bool HitReacting { get; set; }

    /// <summary>When the hit reaction ends, in monotonic milliseconds.</summary>
    public long HitReactUntilMilliseconds { get; set; }

    /// <summary>
    /// Whether the knock-out draws the stun animation <c>C1 1A</c> rather than
    /// the ordinary hit reaction <c>C1 81</c> or the sleep pose <c>01 1C</c>.
    /// </summary>
    public bool StunAnimation { get; set; }

    /// <summary>
    /// When the standing state record may be written again, in monotonic
    /// milliseconds.
    /// </summary>
    /// <remarks>
    /// The reference writes an idle actor's three-byte <c>00 ff 00</c> about once a
    /// second and its acting form on every beat: 7,974 standing records against
    /// 39,693 acting ones across 592 s. This host wrote the standing form on every
    /// beat instead — 16,770 against 11,947 — so the client saw an idle bot at six
    /// times the rate the reference shows and an acting one at a third of it.
    /// </remarks>
    public long NextStateMilliseconds { get; set; }

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
    /// Rounds left in the magazine, which the weapon pair's last byte reports
    /// and which starts a reload at zero (<see cref="BotBurstUtils.MagazineRounds"/>).
    /// </summary>
    public int MagazineRounds { get; set; } = BotBurstUtils.MagazineRounds;

    /// <summary>
    /// When a reload ends, in monotonic milliseconds; zero when the body is not
    /// reloading. The state record writes the nine-byte <c>0x0B</c> form for the
    /// whole of it (<see cref="BotBurstUtils.ReloadMilliseconds"/>).
    /// </summary>
    public long ReloadUntilMilliseconds { get; set; }

    /// <summary>
    /// When the <c>0x48</c> recovery beat ends, in monotonic milliseconds; zero
    /// when no recovery is due. It follows a reload and the firing run that just
    /// stopped, which are the capture's two places for it.
    /// </summary>
    public long RecoverUntilMilliseconds { get; set; }

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

    /// <summary>
    /// When a grab on this body ends, in monotonic milliseconds; zero when no
    /// client is holding it. A held bot stands, holds no target and pulls no
    /// trigger, which is the host's whole part in a grab its client owns
    /// (<see cref="CqcHoldUtils"/>). It keeps its feet and its health: the grab
    /// knocks nobody out.
    /// </summary>
    public long HeldUntilMilliseconds { get; set; }

    /// <summary>
    /// Slot of the client that holds this body, or <see cref="NoTarget"/>.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>.pcapngs/actions/actions.pcapng</c>.</b> The client writes
    /// its hold on slot 15 from t+392.19, and the holding host turns the held body
    /// towards it: the heading climbs <c>0x200c</c>, <c>0x2894</c>, <c>0x311c</c>,
    /// <c>0x3da4</c> — about <c>+0x888</c> a beat, which is the 8 bits of a stage
    /// heading. A body that kept facing where it was aiming is one the client's grab
    /// never settled on.
    /// </remarks>
    public byte HeldBySlot { get; set; } = BotBodyState.NoTarget;

    /// <summary>Last time the body was advanced, in monotonic milliseconds.</summary>
    public long LastAdvanceMilliseconds { get; set; }

    /// <summary>
    /// Times this slot has gone down in the round — a death or a knock-out — which is
    /// what the lying body's word at <c>+4</c> counts.
    /// </summary>
    /// <remarks>
    /// <b>Every down counts, not every death.</b> `actions.pcapng` slot 16 lies dead at
    /// t+43.29 with <c>1</c>, then sleeps twice at t+511.95 and t+547.92 with <c>9</c>
    /// and <c>11</c> while its health stays 250: the word climbs by two on a knock-out
    /// exactly as on a death. Counted from deaths alone, a sleeper repeated the marker
    /// its last death wrote, and a client that keys the fall on the new word never
    /// played it.
    /// </remarks>
    public int Downs { get; set; }

    /// <summary>
    /// Slot this bot has just put down and keeps naming until its feed is written,
    /// or <see cref="NoTarget"/>.
    /// </summary>
    /// <remarks>
    /// <b>Every kill feed in the working capture lands while the killer's state
    /// record still names the victim:</b> <c>09 01 0c</c> arrives with slot 1
    /// writing <c>46 0c 01 …</c>, <c>09 08 07</c> with <c>47 07 …</c>, <c>09 0c 08</c>
    /// with <c>46 08 …</c>. In <c>myserver.pcapng</c> the killer re-targeted within
    /// the feed's 220 ms and named a third bot (<c>44 0e</c> against a feed naming
    /// slot 10), so the one record pair the client can file the attacker from named
    /// no such pair.
    /// </remarks>
    public byte KillAimSlot { get; set; } = BotBodyState.NoTarget;

    /// <summary>
    /// When the bot stops naming <see cref="KillAimSlot"/>, in monotonic
    /// milliseconds.
    /// </summary>
    public long KillAimUntilMilliseconds { get; set; }

    /// <summary>Whether it is dead, which is health at zero.</summary>
    public bool IsDead => Health <= 0;
}
