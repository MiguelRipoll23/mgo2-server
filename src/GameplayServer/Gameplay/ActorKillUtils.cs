namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The kill rule shared by a bot's body and a player's slot: zero health is a
/// death, and a death is credited only to a slot above zero.
/// </summary>
public static class ActorKillUtils
{
    /// <summary>Slot a death with no named killer is filed under.</summary>
    public const byte NoKiller = 0;

    /// <summary>Whether a health bar is empty.</summary>
    /// <param name="health">Health to test.</param>
    /// <returns><c>true</c> at zero or below.</returns>
    public static bool IsFatal(short health) => health <= 0;

    /// <summary>
    /// The slot a kill is credited to. Slot zero is this host's own seat, which is
    /// not a player of the round, so it is the same as nobody.
    /// </summary>
    /// <param name="shooterSlot">Slot the record named, or <c>null</c> for nobody.</param>
    /// <returns>A seated slot, or <see cref="NoKiller"/>.</returns>
    public static byte CreditSlot(byte? shooterSlot) =>
        shooterSlot is > 0 ? shooterSlot.Value : NoKiller;
}
