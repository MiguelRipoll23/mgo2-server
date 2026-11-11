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
            return BuildCorpse(body);
        }

        // The head is the pose, and the capture's are four: 0x21 standing, 0x41
        // walking, 0xC1 the hit reaction, and 0x22 the corpse. An actor holding an
        // enemy writes the walking head whether or not it moves that beat — the
        // working host's engaged bots are 0x41 0x81 on 60% to 75% of their beats —
        // and the 0x81 second byte is what tells the client the actor is armed and
        // acting rather than standing at ease.
        var acting = body.Moving || body.TargetSlot != BotBodyState.NoTarget;
        var position = new byte[BotActorRecordUtils.PositionBodyLength];
        position[0] = (byte)(body.HitReacting ? 0xc1 : acting ? 0x41 : 0x21);
        position[1] = (byte)(body.HitReacting || acting ? 0x81 : 0x00);
        BotActorRecordUtils.WriteInt16(position, 6, BotActorRecordUtils.ToTenths(body.X));
        BotActorRecordUtils.WriteInt16(position, 8, BotActorRecordUtils.ToTenths(body.Y));
        BotActorRecordUtils.WriteInt16(position, 10, BotActorRecordUtils.ToTenths(body.Z));
        BotActorRecordUtils.WriteInt16(position, 12, (short)body.Yaw);
        return position;
    }

    /// <summary>
    /// The corpse's position body, byte for byte as the capture writes it.
    /// </summary>
    /// <remarks>
    /// <b>The floats are <c>z</c>, height and <c>x</c> — not <c>x</c>, <c>y</c>,
    /// <c>z</c>.</b> The capture's death of slot 7 writes <c>22 1a 00 0b 01 00 00 00</c>,
    /// then <c>22161</c>, <c>3265</c>, <c>-8946</c> for a body at <c>x -8950, y 3260,
    /// z 22160</c>, the heading, and the living body's four words again.
    /// </remarks>
    /// <param name="body">The dead body.</param>
    /// <returns>The thirty-two-byte body.</returns>
    private static byte[] BuildCorpse(BotBodyState body)
    {
        var corpse = new byte[BotActorRecordUtils.CorpsePositionBodyLength];
        corpse[0] = 0x22;
        corpse[1] = 0x1a;
        corpse[2] = 0x00;
        corpse[3] = 0x0b;
        BotActorRecordUtils.WriteUInt16(corpse, 4, (ushort)((body.Deaths * 2) - 1));
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
}
