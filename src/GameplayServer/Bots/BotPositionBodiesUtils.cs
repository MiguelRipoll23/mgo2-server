using Mgo2Server.GameplayServer.Gameplay;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The position record an actor carries: the living head with its three
/// coordinates and heading, or the thirty-two bytes of a corpse.
/// </summary>
/// <remarks>
/// Split out of <see cref="BotActorRecordUtils"/> so neither file exceeds the
/// repository's size limit. The bytes are the working capture's own; what the
/// heads mean is that file's subject.
/// </remarks>
public static class BotPositionBodiesUtils
{
    /// <summary>
    /// The head byte a corpse opens with: <c>0x22</c>, the lying frame the
    /// working capture's deaths carry.
    /// </summary>
    private const byte CorpseHead = 0x22;

    /// <summary>
    /// The animation byte behind the corpse's head: <c>0x1A</c>, the stun
    /// animation the down body reuses.
    /// </summary>
    private const byte CorpseAnimation = (byte)ActorAnimation.Stun;

    /// <summary>The frame byte between the animation and the counter at +4.</summary>
    private const byte CorpseFrame = 0x00;

    /// <summary>The fourth header byte, fixed at <c>0x0B</c> across the captures.</summary>
    private const byte CorpseMarker = 0x0b;


    /// <summary>
    /// The position body: the walking or standing head, then <c>x</c>, height and
    /// <c>z</c> at a tenth of a unit, then the heading.
    /// </summary>
    /// <remarks>
    /// The heads are the capture's: <c>21 00 00 00</c> on a bot standing at its
    /// spawn, <c>41 81 00 00</c> on one walking. The three coordinates are the
    /// stage's at a tenth, which is how the recorded host writes a bot from
    /// <c>-23750</c> as <c>-2375</c>.
    /// </remarks>
    /// <param name="body">The bot's body.</param>
    /// <returns>The sixteen-byte body, or a corpse's thirty-two.</returns>
    public static byte[] Build(BotBodyState body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.IsDead)
        {
            return BuildDown(body, DownMarker(body));
        }

        // A sleeping body is not a corpse: the sleep captures write the sixteen-byte
        // lying pose for the whole of the sleep and never the thirty-two-byte body a
        // death carries.
        if (body.Sleeping)
        {
            return BuildLying(body);
        }

        // A CQC or knife knock-out draws the stun animation, not the sleep's lying
        // form and not the ordinary hit reaction.
        if (body.StunAnimation)
        {
            return BuildStunned(body);
        }

        // The head is the stance the capture writes for the same body: 0x21
        // standing, 0x41 walking, 0xC1 the hit reaction — and the second byte's
        // 0x81 says the actor is armed and acting rather than standing at ease.
        // An actor holding an enemy writes the walking form whether or not it moves
        // that beat (the working host's engaged bots are 0x41 0x81 on 60% to 75% of
        // their beats).
        var stance = StanceOf(body);
        var acting = stance != ActorStance.Standing;
        var position = new byte[BotActorRecordUtils.PositionBodyLength];
        position[0] = ActorPoseUtils.HeadOf(stance);
        position[1] = ActorPoseUtils.FlagsOf(armed: acting, acting: acting);
        WriteCoordinates(position, body);
        return position;
    }

    /// <summary>
    /// The stance a bot's body holds: the knocked-down form while it reacts to a
    /// blow, lying while it sleeps, the walking form while it moves or holds an
    /// enemy, and standing otherwise.
    /// </summary>
    /// <param name="body">The body to read.</param>
    /// <returns>The stance its position record writes.</returns>
    public static ActorStance StanceOf(BotBodyState body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.HitReacting)
        {
            return ActorStance.KnockedDown;
        }

        if (body.Sleeping)
        {
            return ActorStance.Lying;
        }

        return body.Moving || body.TargetSlot != BotBodyState.NoTarget
            ? ActorStance.Walking
            : ActorStance.Standing;
    }

    /// <summary>
    /// The sixteen-byte stunned body a CQC or knife knock-out writes.
    /// </summary>
    /// <remarks>
    /// <b>Measured over the stun captures.</b> The knocked-down head carries the
    /// stun animation <c>0x1A</c>, the client's stars, without the armed or acting
    /// bits: <c>C1 1A</c>. It is not the sleep's <c>01 1C</c> and not the ordinary
    /// hit reaction's <c>C1 81</c>.
    /// </remarks>
    /// <param name="body">The stunned body.</param>
    /// <returns>The sixteen-byte body.</returns>
    private static byte[] BuildStunned(BotBodyState body)
    {
        var stunned = new byte[BotActorRecordUtils.PositionBodyLength];
        stunned[0] = ActorPoseUtils.HeadOf(ActorStance.KnockedDown);
        stunned[1] = ActorPoseUtils.FlagsOf(
            armed: false,
            acting: false,
            animation: ActorAnimation.Stun);
        WriteCoordinates(stunned, body);
        return stunned;
    }

    /// <summary>
    /// The sixteen-byte lying body a sleeping bot writes.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>.pcapngs/actions/sleep-by-sgmines.pcapng</c>.</b> A body
    /// the <c>0x40</c> non-lethal blast puts to sleep writes <c>01 1C</c> and its
    /// position for the whole of the sleep, with health still at 250; the
    /// thirty-two-byte <c>22 1A</c> body the same capture writes on the corpse of a
    /// slot that was shot is a death and not a sleep.
    /// </remarks>
    /// <param name="body">The sleeping body.</param>
    /// <returns>The sixteen-byte body.</returns>
    private static byte[] BuildLying(BotBodyState body)
    {
        var lying = new byte[BotActorRecordUtils.PositionBodyLength];
        lying[0] = ActorPoseUtils.HeadOf(ActorStance.Lying);
        lying[1] = ActorPoseUtils.FlagsOf(
            armed: false,
            acting: false,
            animation: ActorAnimation.Sleep);
        WriteCoordinates(lying, body);
        return lying;
    }

    /// <summary>Writes a living body's three coordinates and heading.</summary>
    /// <param name="position">Body to write into.</param>
    /// <param name="body">The body they belong to.</param>
    private static void WriteCoordinates(byte[] position, BotBodyState body)
    {
        BotActorRecordUtils.WriteInt16(position, 6, BotActorRecordUtils.ToTenths(body.X));
        BotActorRecordUtils.WriteInt16(position, 8, BotActorRecordUtils.ToTenths(body.Y));
        BotActorRecordUtils.WriteInt16(position, 10, BotActorRecordUtils.ToTenths(body.Z));
        BotActorRecordUtils.WriteInt16(position, 12, (short)body.Yaw);
    }

    /// <summary>
    /// The thirty-two-byte body a corpse writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The floats are <c>z</c>, height and <c>x</c> — not <c>x</c>, <c>y</c>,
    /// <c>z</c>.</b> The capture's death of slot 7 writes <c>22 1a 00 0b 01 00 00 00</c>,
    /// then <c>22161</c>, <c>3265</c>, <c>-8946</c> for a body at <c>x -8950, y 3260,
    /// z 22160</c>, the heading, and the living body's four words again.
    /// </para>
    /// <para>
    /// <b>The first two bytes repeat the head-and-pose pair.</b>
    /// <c>working-server-log.pcapng</c> writes <c>22 1a</c> and <c>21 1a</c> on
    /// bodies whose vitals read zero, and <c>A1 18</c> on bodies whose health is
    /// still 81 to 196 — the living knock-out, not a death. This writer emits the
    /// <c>22 1a 00 0b</c> form, one of the measured death headers.
    /// </para>
    /// </remarks>
    /// <param name="body">The lying body.</param>
    /// <param name="marker">The word at <c>+4</c>.</param>
    /// <returns>The thirty-two-byte body.</returns>
    private static byte[] BuildDown(BotBodyState body, ushort marker)
    {
        var corpse = new byte[BotActorRecordUtils.CorpsePositionBodyLength];
        corpse[0] = CorpseHead;
        corpse[1] = CorpseAnimation;
        corpse[2] = CorpseFrame;
        corpse[3] = CorpseMarker;
        BotActorRecordUtils.WriteUInt16(corpse, 4, marker);
        BotActorRecordUtils.WriteFloat(corpse, 8, body.Z);
        BotActorRecordUtils.WriteFloat(corpse, 12, body.Y);
        BotActorRecordUtils.WriteFloat(corpse, 16, body.X);
        BotActorRecordUtils.WriteInt16(corpse, 20, (short)body.Yaw);
        BotActorRecordUtils.WriteInt16(corpse, 22, BotActorRecordUtils.ToTenths(body.X));
        BotActorRecordUtils.WriteInt16(corpse, 24, BotActorRecordUtils.ToTenths(body.Y));
        BotActorRecordUtils.WriteInt16(corpse, 26, BotActorRecordUtils.ToTenths(body.Z));
        BotActorRecordUtils.WriteInt16(corpse, 28, (short)body.Yaw);
        return corpse;
    }

    /// <summary>
    /// The counter at <c>+4</c> a lying body carries: <c>1</c> the first time a slot
    /// goes down, then <c>3, 5 …</c> — two a down, a death and a knock-out alike.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>.pcapngs/actions/actions.pcapng</c>.</b> Slot 16's corpse at
    /// t+43.29 is <c>1</c>; its two sleeps at t+511.95 and t+547.92 are <c>9</c> and
    /// <c>11</c>, health 250 on both. Counted from deaths alone, each sleep repeated
    /// the word its last death left.
    /// </remarks>
    /// <param name="body">The body that is down.</param>
    /// <returns>The marker word.</returns>
    private static ushort DownMarker(BotBodyState body) =>
        (ushort)Math.Max(1, (body.Downs * 2) - 1);
}
