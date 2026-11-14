namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The action an actor's state record carries in the low six bits of its first
/// byte, with <c>0x40</c> above them marking the actor as acting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured over <c>.pcapngs/actions/</c>, one capture per action.</b>
/// <c>shooting-and-reloading</c> writes <c>02 FF 00</c>, then
/// <c>04 FF 00 …</c>, then alternating <c>06</c> and <c>07</c> beats as the
/// magazine empties; <c>hitting-with-weapon-short-and-long-attack</c> closes a
/// melee run on <c>48 FF 00</c>; <c>switching-standing-crouching-prone</c>
/// carries the down form <c>25 FF 00</c>; the CQC captures write
/// <c>57</c>/<c>58</c>/<c>59</c>/<c>5A FF 00</c>; and
/// <c>different-explosives-mines-grenades-on-me</c> writes <c>0A</c> for a placed
/// device and <c>14</c> then <c>16</c> — with <c>0x40</c> and <c>0x80</c> above
/// them — for each thrown grenade.
/// </para>
/// <para>
/// <b>The action is not the stance.</b> A walking actor still writes
/// <see cref="Idle"/>: the stance is the position record's head
/// (<see cref="ActorStance"/>) and this byte is what the upper body is doing.
/// </para>
/// </remarks>
public enum ActorAction : byte
{
    /// <summary>Nothing: the three-byte <c>00 FF 00</c> a standing actor writes.</summary>
    Idle = 0x00,

    /// <summary>Aiming, from <c>02 FF 00</c> to the nine-byte form with its aim point.</summary>
    Aim = 0x02,

    /// <summary>The trigger held: <c>04</c>, which rides every automatic-fire beat.</summary>
    TriggerHeld = 0x04,

    /// <summary>The round that opens a burst: <c>06</c>.</summary>
    Fire = 0x06,

    /// <summary>The rounds behind it: <c>07</c>, alternating with <see cref="Fire"/>.</summary>
    FireBeat = 0x07,

    /// <summary>
    /// The body coming off an attack: a single three-byte <c>08 FF 00</c>
    /// between the last aimed beat and <see cref="Idle"/>.
    /// </summary>
    Recover = 0x08,

    /// <summary>
    /// A placed device — a mine or C4 — being set down: the nine-byte <c>0A</c>.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>different-explosives-mines-grenades-on-me.pcapng</c>.</b>
    /// Its <c>0A</c> run at t+202.12 names one aim point for five beats
    /// (<c>c0 00 88 00 e3 fa</c>), which is a placement, not a throw. The grenades
    /// in the same capture are <see cref="ThrowReady"/> and
    /// <see cref="ThrowRelease"/>.
    /// </remarks>
    PlaceDevice = 0x0a,

    /// <summary>
    /// A reload: the long run of <c>0B</c> beats <c>shooting-and-reloading</c>
    /// carries after a magazine is spent.
    /// </summary>
    Reload = 0x0b,

    /// <summary>
    /// A grenade readied in the hand: <c>14</c>.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>different-explosives-mines-grenades-on-me.pcapng</c>.</b>
    /// Each throw opens on a nine-byte <c>14 FF 00</c> run (t+180.09, 284.19,
    /// 310.22) whose aim point tracks the player, then turns into
    /// <see cref="ThrowRelease"/> about 0.8 s later.
    /// </remarks>
    ThrowReady = 0x14,

    /// <summary>
    /// The grenade leaving the hand: <c>16</c>.
    /// </summary>
    /// <remarks>
    /// It runs for about 1.4 s in the same capture, the last 0.6 s as the
    /// three-byte form, and the blast follows. The first throw writes it with the
    /// <c>0x80</c> bit alone (<c>96</c>); the other two with <c>0x80</c> and the
    /// acting bit (<c>D6</c>).
    /// </remarks>
    ThrowRelease = 0x16,

    /// <summary>A CQC grab that holds an enemy: <c>17</c>, the hold's first action.</summary>
    CqcHold = 0x17,

    /// <summary>A CQC blow landed in a hold: <c>18</c>.</summary>
    CqcStrike = 0x18,

    /// <summary>A CQC finish in a hold: <c>19</c>.</summary>
    CqcFinish = 0x19,

    /// <summary>A CQC throw out of a hold: <c>1A</c>, measured on the knife capture.</summary>
    CqcThrow = 0x1a,

    /// <summary>
    /// The round-opening form: <c>25 FF 00</c>, which the capture writes for a
    /// few beats at the start of every round and again when a body comes back
    /// to its feet.
    /// </summary>
    Spawn = 0x25,
}
