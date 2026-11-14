namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// Reads and writes the action an actor state record's first byte carries.
/// </summary>
/// <remarks>
/// The acting bit is <c>0x40</c>: the capture writes <c>42 FF 00</c> for an
/// aimed body and <c>02 FF 00</c> for one that has only started to aim, so the
/// bit is part of the action's shape and not a flag to mask away and forget.
/// The throw family also sets <c>0x80</c> — <c>94</c>, <c>96</c>, <c>D4</c>,
/// <c>D6</c> — which is above the action's six bits and not acting: <c>Of(0x94)</c>
/// is the same <see cref="ActorAction.ThrowReady"/> as <c>Of(0x14)</c>.
/// </remarks>
public static class ActorActionUtils
{
    /// <summary>Bit that says the actor is acting rather than standing at ease.</summary>
    public const byte ActingBit = 0x40;

    /// <summary>Mask of the six bits the action itself occupies.</summary>
    public const byte ActionMask = 0x3f;

    /// <summary>
    /// Bit above the acting bit that the throw family sets: measured on
    /// <c>different-explosives-mines-grenades-on-me.pcapng</c> only.
    /// </summary>
    public const byte ThrowBit = 0x80;

    /// <summary>
    /// Reads the action out of a state record's first byte.
    /// </summary>
    /// <param name="first">The record's first byte.</param>
    /// <returns>The action, or <c>null</c> for a value the captures do not name.</returns>
    public static ActorAction? Of(byte first)
    {
        var action = (ActorAction)(first & ActionMask);
        return Enum.IsDefined(action) ? action : null;
    }

    /// <summary>Whether a state record's first byte marks the actor as acting.</summary>
    /// <param name="first">The record's first byte.</param>
    /// <returns><c>true</c> when <c>0x40</c> is set.</returns>
    public static bool IsActing(byte first) => (first & ActingBit) != 0;

    /// <summary>Writes a state record's first byte.</summary>
    /// <param name="action">The action.</param>
    /// <param name="acting">Whether the actor is acting.</param>
    /// <returns>The first byte.</returns>
    public static byte FirstByteOf(ActorAction action, bool acting = true) =>
        (byte)((byte)action | (acting ? ActingBit : 0));
}
