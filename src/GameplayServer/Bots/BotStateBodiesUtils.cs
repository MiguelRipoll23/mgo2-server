using Mgo2Server.GameplayServer.Gameplay;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The state record a bot's actor block carries: the three-byte standing form,
/// or the nine-byte acting form when it aims, fires or is held.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured over <c>.pcapngs/actions/</c>.</b> A body with no target writes
/// <c>00 FF 00</c>. One that aims writes <c>42 FF 00</c> and its nine-byte form
/// carries the point looked at; a held trigger writes <c>44 &lt;target&gt; 01</c>,
/// the round that opens a burst <c>46</c> and the rounds behind it <c>47</c>.
/// The actions are <see cref="ActorAction"/> and the bit above them
/// <see cref="ActorActionUtils.ActingBit"/>.
/// </para>
/// <para>
/// <b>The acting form is written only while an enemy is held.</b> The capture
/// writes the standing <c>00 FF 00</c> on about eight beats in ten, its walking
/// bots included, where this host wrote the acting form whenever a bot moved —
/// a bot aiming down every corridor it patrolled.
/// </para>
/// </remarks>
public static class BotStateBodiesUtils
{
    /// <summary>Length of the standing state body.</summary>
    public const int StandingBodyLength = 3;

    /// <summary>Length of the acting state body.</summary>
    public const int ActingBodyLength = 9;

    /// <summary>
    /// Builds one actor's state body for this beat.
    /// </summary>
    /// <param name="body">The bot's body.</param>
    /// <param name="firing">Whether it holds its trigger this tick.</param>
    /// <param name="fireBeat">Whether this tick is one a shot animation rides on.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The three- or nine-byte body.</returns>
    public static byte[] Build(BotBodyState body, bool firing, bool fireBeat, long now)
    {
        ArgumentNullException.ThrowIfNull(body);

        // A reload is the capture's nine-byte `0x4B ...` run; the beat behind it is
        // the three-byte `0x48 FF 00` recovery.
        if (body.ReloadUntilMilliseconds > now)
        {
            return BuildReloading(body);
        }

        if (body.RecoverUntilMilliseconds > now)
        {
            return
            [
                ActorActionUtils.FirstByteOf(ActorAction.Recover),
                BotBodyState.NoTarget,
                0x00,
            ];
        }

        if (body.TargetSlot == BotBodyState.NoTarget && body.HeldBySlot == BotBodyState.NoTarget)
        {
            return
            [
                ActorActionUtils.FirstByteOf(ActorAction.Idle, acting: false),
                BotBodyState.NoTarget,
                0x00,
            ];
        }

        // A body a client holds writes the aim form with `0xff` for the target and
        // a zero trigger — the capture's `42 ff 00 63 08 8c 00 d8 07` for the
        // whole of its grab, after the one standing `00 ff 00` beat that opens it.
        var held = body.HeldBySlot != BotBodyState.NoTarget;
        var opening = fireBeat && body.BurstSize > 0 && body.BurstRemaining >= body.BurstSize;
        var action = held
            ? ActorAction.Aim
            : opening
                ? ActorAction.Fire
                : fireBeat
                    ? ActorAction.FireBeat
                    : firing
                        ? ActorAction.TriggerHeld
                        : ActorAction.Aim;

        var state = new byte[ActingBodyLength];
        state[0] = ActorActionUtils.FirstByteOf(action, acting: true);
        state[1] = held ? BotBodyState.NoTarget : body.TargetSlot;
        state[2] = (byte)(!held && firing ? 1 : 0);
        WriteAim(state, body);
        return state;
    }

    /// <summary>
    /// The nine-byte reload body: the <c>0x4B</c> action, no target, and the point
    /// the body is looking at while it reloads.
    /// </summary>
    /// <param name="body">The reloading body.</param>
    /// <returns>The nine-byte body.</returns>
    private static byte[] BuildReloading(BotBodyState body)
    {
        var state = new byte[ActingBodyLength];
        state[0] = ActorActionUtils.FirstByteOf(ActorAction.Reload);
        state[1] = BotBodyState.NoTarget;
        state[2] = 0x00;
        WriteAim(state, body);
        return state;
    }

    /// <summary>Writes the aim point an acting state body closes with.</summary>
    /// <param name="state">Body to write into.</param>
    /// <param name="body">The body it belongs to.</param>
    private static void WriteAim(byte[] state, BotBodyState body)
    {
        BotActorRecordUtils.WriteInt16(state, 3, BotActorRecordUtils.ToTenths(body.AimX));
        BotActorRecordUtils.WriteInt16(state, 5, BotActorRecordUtils.ToTenths(body.AimY));
        BotActorRecordUtils.WriteInt16(state, 7, BotActorRecordUtils.ToTenths(body.AimZ));
    }
}
