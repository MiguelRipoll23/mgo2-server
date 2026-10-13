using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The loadout bodies the recorded host sent, one per shape the captures hold,
/// with the slot and length each shape was measured at.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are shapes rather than payloads: the room's own fields are written
/// into them.</b> A loadout record's slot byte, side, appearance block and skill
/// pairs are rows this server holds per player, so the burst writes those and
/// takes from these bodies only what nothing names — the four bytes at
/// <c>0x12</c>, the two before the appearance block, and the byte a no-pair
/// region opens with (<see cref="LoadoutFramesUtils.TryWrite"/>). A body is
/// therefore chosen by the number of skill pairs the player has, because its
/// length follows from that count.
/// </para>
/// <para>
/// <b>Three shapes, and the game's own bodies have no fourth.</b> A body is 49
/// bytes plus its skill region of <c>0xff</c> and one pair per two bytes, and the
/// lengths the replay decoder accepts contain no 52 — the length a single pair
/// would need — so a player with one or four equipped skills has no recorded body
/// of that shape to be written in, and the burst falls back to
/// <see cref="PlayerLoadout"/> behind the player's own slot byte.
/// </para>
/// <para>
/// The four bodies below are byte for byte what the captures wrote: the fifty-byte
/// and fifty-four-byte ones from the burst at t+6.687 of <c>mgo2-game1.pcapng</c>
/// (the second written twice), and the fifty-six-byte one that capture writes for
/// the single player of its roster carrying three pairs.
/// </para>
/// </remarks>
public static class LoadoutBodiesUtils
{
    /// <summary>Message type a loadout body travels under.</summary>
    /// <remarks>
    /// The roster type, because the payload family is polymorphic: a <c>0x1001</c>
    /// record carries a <c>07 48</c> roster entry on one datagram and a <c>0b</c>
    /// payload on the next.
    /// </remarks>
    public const ushort LoadoutRecordType = UdpCommandConstants.PlayerProfile;

    /// <summary>Slot byte on the host's own loadout record.</summary>
    public const byte PlayerLoadoutSlot = 0x01;

    /// <summary>Body length of that record, as measured.</summary>
    public const int PlayerLoadoutPayloadLength = 50;

    /// <summary>Skill pairs that record carries: none.</summary>
    public const int PlayerLoadoutSkillCount = 0;

    /// <summary>Slot byte on the fifty-four-byte record, which the capture writes twice.</summary>
    public const byte SecondLoadoutSlot = 0x15;

    /// <summary>Body length of that record, as measured.</summary>
    public const int SecondLoadoutPayloadLength = 54;

    /// <summary>Skill pairs that record carries: two.</summary>
    public const int SecondLoadoutSkillCount = 2;

    /// <summary>Slot byte on the fifty-six-byte record.</summary>
    public const byte ThirdLoadoutSlot = 0x0c;

    /// <summary>Body length of that record, as measured.</summary>
    public const int ThirdLoadoutPayloadLength = 56;

    /// <summary>Skill pairs that record carries: three.</summary>
    public const int ThirdLoadoutSkillCount = 3;

    /// <summary>
    /// The fifty-byte loadout record of game1's burst, in slot <c>0x01</c>: the
    /// host's own entry, with no skill pairs.
    /// </summary>
    private const string PlayerLoadoutPayloadHex =
        "0b01020000000000000000000000000000000000005002feffc2051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// The fifty-four-byte loadout record written twice, in slot <c>0x15</c>, with
    /// two skill pairs. The parser reads the same appearance out of it as out of
    /// the fifty-byte one.
    /// </summary>
    private const string SecondLoadoutPayloadHex =
        "0b15020000000000000000000000000000007800005002ff01030201ff78051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// The fifty-six-byte loadout record game1 writes for the one player of its
    /// roster carrying three skill pairs, in slot <c>0x0c</c>.
    /// </summary>
    private const string ThirdLoadoutPayloadHex =
        "0b0c02000000000000000000000000000000f801005002ff05020d030f01ff77061f442e643e6b66130000011300001216001b1309000000";

    /// <summary>The recorded host's own loadout body, with no skill pairs.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] PlayerLoadout() => Convert.FromHexString(PlayerLoadoutPayloadHex);

    /// <summary>The recorded body carrying two skill pairs, as the capture writes it twice.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] SecondLoadout() => Convert.FromHexString(SecondLoadoutPayloadHex);

    /// <summary>The recorded body carrying three skill pairs.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] ThirdLoadout() => Convert.FromHexString(ThirdLoadoutPayloadHex);

    /// <summary>
    /// The recorded loadout body of a shape, named by the skill pairs it carries.
    /// </summary>
    /// <param name="skillCount">Pairs the player's own skills make.</param>
    /// <returns>The body, or <c>null</c> when no recorded body has that shape.</returns>
    public static byte[]? RecordedLoadoutFor(int skillCount) => skillCount switch
    {
        PlayerLoadoutSkillCount => PlayerLoadout(),
        SecondLoadoutSkillCount => SecondLoadout(),
        ThirdLoadoutSkillCount => ThirdLoadout(),
        _ => null,
    };
}
