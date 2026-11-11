namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What a body remembers of the blow that put it down: the slot that took its
/// last health, and the one beat that still has to name it.
/// </summary>
/// <remarks>
/// <b>The victim names its killer on the beat it dies.</b> The working host keeps
/// the victim's state record on its killer right up to the blow — slot 5 writes
/// <c>44 06 01 …</c> for six beats, and stands with <c>00 ff 00</c> only after the
/// fatal hit — so a body that dropped its target the instant it died would be one
/// the client never saw turn on the actor that shot it.
/// </remarks>
public static class BotDeathUtils
{
    /// <summary>
    /// Stamps a body with the slot that killed it, and turns the body on that slot
    /// so the record it writes next still names shooter and victim of each other.
    /// </summary>
    /// <param name="body">The body that has just lost its last health.</param>
    /// <param name="killerSlot">Slot the death is credited to.</param>
    /// <param name="bodies">Every body the simulation holds, to read the killer's position from.</param>
    public static void NoteDeath(BotBodyState body, byte killerSlot, IReadOnlyList<BotBodyState> bodies)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(bodies);

        body.KillerSlot = killerSlot;
        body.DiedThisBeat = true;

        if (bodies.FirstOrDefault(other => other.SessionSlot == killerSlot) is not { } killer)
        {
            return;
        }

        body.TargetSlot = killerSlot;
        body.AimX = killer.X;
        body.AimY = killer.Y + BotLocomotionService.AimHeight;
        body.AimZ = killer.Z;
        body.Yaw = BotLocomotionService.YawTowards(killer.X - body.X, killer.Z - body.Z);
    }
}
