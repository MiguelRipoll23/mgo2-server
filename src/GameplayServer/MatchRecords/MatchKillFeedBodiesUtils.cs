namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The four-byte record the working host writes when a player goes down:
/// <c>02 &lt;killer slot&gt; &lt;victim slot&gt; 02</c>, under the item-box group's
/// identifier with the <c>0x08</c> bit (<c>0x1a58</c>), its fourth byte an ordinal
/// that climbs one per record.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on `.pcapngs/game-with-bots-workingserver.pcapng`.</b> Twenty-nine
/// of them in ten minutes, all host to client, ordinals <c>0</c> to <c>28</c>:
/// <c>02 06 00 02</c>, <c>02 06 0e 02</c>, <c>02 05 0e 02</c>, then
/// <c>02 08 05 02</c>, <c>02 08 03 02</c>, <c>02 08 0c 02</c>, <c>02 08 06 02</c>,
/// <c>02 08 04 02</c> and <c>02 08 00 02</c> four times. The slot that dies is the
/// second: the human holds slot <c>8</c> and appears as the second byte only on
/// itself, while <c>5</c>, <c>6</c>, <c>3</c>, <c>4</c> and <c>0c</c> are the
/// bots' slots, and every one of those deaths is a vitals record at zero a beat
/// later.
/// </para>
/// <para>
/// <b>The two slot bytes' roles are [I].</b> The shape and the ordinal are
/// measured; that the first byte is the killer and the second the victim is the
/// reading the vitals records support, and it is the direction the client's own
/// kill feed draws. The opcode and the trailing byte are the recorded body's and
/// nothing here claims to know them.
/// </para>
/// <para>
/// **This is the identifier this host used to write a player list under.** A
/// two-byte <c>00 &lt;slot&gt;</c> per member in that slot read as a kill feed —
/// slot zero killing each player — and took every player off the client's screen
/// the moment a round opened.
/// </para>
/// </remarks>
public static class MatchKillFeedBodiesUtils
{
    /// <summary>Length of the body the working host writes.</summary>
    public const int BodyLength = 4;

    /// <summary>Leading byte of the recorded body, <c>0x02</c>.</summary>
    public const byte Opcode = 0x02;

    /// <summary>Trailing byte of the recorded body, <c>0x02</c>.</summary>
    public const byte Tail = 0x02;

    /// <summary>
    /// The record one death travels as.
    /// </summary>
    /// <param name="killerSlot">Slot the kill is credited to.</param>
    /// <param name="victimSlot">Slot that went down.</param>
    /// <returns>The four-byte body.</returns>
    public static byte[] Build(byte killerSlot, byte victimSlot) =>
        [Opcode, killerSlot, victimSlot, Tail];
}
