namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The four-byte chat macro body that travels under <c>0x1a58</c>:
/// <c>[kind, slot, macro, 0x02]</c>.
/// </summary>
/// <remarks>
/// <b>Measured on <c>.pcapngs/actions/sending-chat-macros.pcapng</c>.</b> A
/// client sends kind <c>0x01</c> with its slot and the macro index; the host
/// confirms the same slot and macro as kind <c>0x02</c>. Macro <c>0x00</c> is
/// idle/cleared. The third byte is the macro index, not a victim slot: the old
/// kill-feed writer's <c>02 &lt;killer&gt; &lt;victim&gt; 02</c> was read by the
/// client as a chat macro and announced “Roger that” instead of the death.
/// </remarks>
public static class ChatMacroBodiesUtils
{
    /// <summary>Length of the chat macro body.</summary>
    public const int BodyLength = 4;

    /// <summary>Kind a client writes when it sends a macro.</summary>
    public const byte ClientKind = 0x01;

    /// <summary>Kind the host writes when it confirms a macro.</summary>
    public const byte HostKind = 0x02;

    /// <summary>Macro index that means idle or cleared.</summary>
    public const byte IdleMacro = 0x00;

    /// <summary>Fourth byte every measured chat body closes with.</summary>
    public const byte Terminator = 0x02;

    /// <summary>
    /// Whether a body is a chat macro rather than the room count that shares the
    /// identifier.
    /// </summary>
    /// <param name="body">Body to test.</param>
    /// <returns><c>true</c> for the four-byte chat form.</returns>
    public static bool IsChatMacro(ReadOnlySpan<byte> body) =>
        body.Length == BodyLength
        && body[3] == Terminator
        && body[0] is ClientKind or HostKind;

    /// <summary>
    /// Builds a chat macro body.
    /// </summary>
    /// <param name="kind">Client or host kind.</param>
    /// <param name="slot">Slot the macro belongs to.</param>
    /// <param name="macro">Macro index, or <see cref="IdleMacro"/>.</param>
    /// <returns>The four-byte body.</returns>
    public static byte[] Build(byte kind, byte slot, byte macro) =>
        [kind, slot, macro, Terminator];
}
