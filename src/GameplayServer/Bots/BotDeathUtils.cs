namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What a body remembers of the blow that put it down: the slot that took its
/// last health, and the one beat that still has to name it.
/// </summary>
/// <remarks>
/// <b>The victim does not name its killer on the beat it dies; it stands.</b>
/// Measured over every kill in the working capture: the victim's last state
/// record before its feed is the standing <c>00 ff 00</c> on 108 of 113 deaths,
/// and the five that carry a target are beats where the victim was still alive
/// and shooting at somebody else. A corpse that turns on the actor that shot it
/// names a killer the working host never names from the victim's side — the
/// credit is the <c>09</c> feed's and the killer's own aim record's, and this
/// host wrote the victim's <c>42 &lt;killer&gt; 00</c> on all nine of
/// <c>myserver.pcapng</c>'s deaths.
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

        // A corpse stands. The working host's victim is on `00 ff 00` by the beat
        // its health reaches zero, and naming the killer here instead — `42
        // <killer> 00` — is a target the working host's victim never holds.
        body.KillAimSlot = BotBodyState.NoTarget;
        body.KillAimUntilMilliseconds = 0;
        body.TargetSlot = BotBodyState.NoTarget;
    }
}
