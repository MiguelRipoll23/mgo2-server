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
/// <b>Every pair count now has a shape, and only three of them are
/// captured.</b> A body's length is the pair count's own arithmetic
/// (<see cref="LoadoutFramesUtils.BodyLengthFor"/>): a leading marker, two bytes
/// a pair, the list end, two unnamed bytes and the appearance block. The
/// captures hold the zero-, two- and three-pair shapes, so those three bodies are
/// byte for byte what the game wrote. A one- or four-pair player is the shape the
/// capture never held, and the burst used to fall back on a body carrying a
/// different player's skills — the one length this record cannot absorb, because
/// the peer reads the skills off the length. Those two shapes are built below
/// rather than looked for: a recorded body with the pair bytes spliced in behind
/// the region's leading marker. Every other byte is the recorded body's, and the
/// spliced bytes are always overwritten by the player's own pair, so nothing is
/// invented.
/// </para>
/// <para>
/// The four bodies that are captured are: the fifty-byte and fifty-four-byte ones
/// from the burst at t+6.687 of <c>mgo2-game1.pcapng</c> (the second written
/// twice), and the fifty-six-byte one that capture writes for the single player of
/// its roster carrying three pairs.
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
    /// Body length of the four-pair shape: the rule's own arithmetic on the four
    /// slots a character's skills are saved in.
    /// </summary>
    /// <remarks>
    /// **[U]** — arithmetic, not an observation. Nothing captured or in a replay
    /// file holds a body of this length; see
    /// <see cref="LoadoutFramesUtils.MaximumSkills"/> for why it is built and not
    /// yet written.
    /// </remarks>
    public static int FourthLoadoutPayloadLength =>
        LoadoutFramesUtils.BodyLengthFor(FourthLoadoutSkillCount);

    /// <summary>Skill pairs the one-pair shape carries.</summary>
    public const int SingleLoadoutSkillCount = 1;

    /// <summary>Skill pairs the four-pair shape carries.</summary>
    public const int FourthLoadoutSkillCount = 4;

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

    /// <summary>
    /// The appearance block the recorded host's own loadout body carries.
    /// </summary>
    /// <remarks>
    /// <b>It is the reference face a player with no readable appearance is
    /// announced with.</b> The dedicated host character is created without an
    /// appearance row, so the host's own loadout cannot be built from its rows;
    /// a loadout whose face is the capture's rather than another character's is
    /// still a loadout the client reads, which is what keeps the room's own
    /// players named in the settings burst. The block is the recorded body's
    /// trailing <see cref="LoadoutFramesUtils.AppearanceLength"/> bytes, in wire
    /// order, exactly as that body carries them.
    /// </remarks>
    /// <returns>The 23-byte appearance block.</returns>
    public static byte[] ReferenceAppearanceBlock()
    {
        var recorded = PlayerLoadout();
        return recorded[^LoadoutFramesUtils.AppearanceLength..];
    }

    /// <summary>The recorded body carrying two skill pairs, as the capture writes it twice.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] SecondLoadout() => Convert.FromHexString(SecondLoadoutPayloadHex);

    /// <summary>The recorded body carrying three skill pairs.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] ThirdLoadout() => Convert.FromHexString(ThirdLoadoutPayloadHex);

    /// <summary>
    /// The one-pair shape, built rather than captured: the fifty-byte body with
    /// two bytes spliced in behind its region's leading marker, which grows the
    /// record to the length one pair is.
    /// </summary>
    /// <returns>The body, with the pair slot left zero for the player's own pair.</returns>
    public static byte[] SingleLoadout() =>
        GrownForPairs(PlayerLoadout(), PlayerLoadoutSkillCount);

    /// <summary>
    /// The four-pair shape, built rather than captured, and **[U]**: the length is
    /// the rule's arithmetic and no capture holds a body of it. See
    /// <see cref="LoadoutFramesUtils.MaximumSkills"/>.
    /// </summary>
    /// <returns>The body, with the pair slots left zero for the player's own pairs.</returns>
    public static byte[] FourthLoadout() =>
        GrownForPairs(ThirdLoadout(), ThirdLoadoutSkillCount);

    /// <summary>
    /// Grows a recorded body to the shape one pair more has, by leaving room for
    /// the extra pair inside its skill region behind the leading marker.
    /// </summary>
    /// <remarks>
    /// The room is what the length rule says the region holds, not a value read
    /// anywhere: the header and the leading marker stay put, the pair slots open up
    /// between them and the closing list end, the list end moves to the body's new
    /// end, and the unnamed run behind it and the appearance block move with it
    /// unchanged. The opened slots are the pair bytes themselves, which
    /// <see cref="LoadoutFramesUtils.TryWrite"/> overwrites with the player's own
    /// pairs, so nothing is invented.
    /// </remarks>
    /// <param name="recorded">A recorded body carrying <paramref name="recordedPairs"/> pairs.</param>
    /// <param name="recordedPairs">Pairs that body carries.</param>
    /// <returns>The grown body, one pair longer.</returns>
    private static byte[] GrownForPairs(byte[] recorded, int recordedPairs)
    {
        var pairs = recordedPairs + 1;
        var grown = new byte[LoadoutFramesUtils.BodyLengthFor(pairs)];
        var marker = LoadoutFramesUtils.SkillRegionOffset;

        // The header and the region's leading marker.
        Array.Copy(recorded, 0, grown, 0, marker + 1);

        // The pair slots are the body's own zero bytes; only the list end is set.
        var listEnd = marker + 1 + (LoadoutFramesUtils.BytesPerPair * pairs);
        grown[listEnd] = LoadoutFramesUtils.SkillListEnd;

        // The run behind the recorded list end, and the appearance block with it.
        var source = recorded.Length - LoadoutFramesUtils.ListEndFromBodyEnd + 1;
        Array.Copy(recorded, source, grown, listEnd + 1, recorded.Length - source);

        return grown;
    }

    /// <summary>
    /// The loadout body of a shape, named by the skill pairs it carries.
    /// </summary>
    /// <param name="skillCount">Pairs the player's own skills make.</param>
    /// <returns>
    /// The body, or <c>null</c> when no shape has that many pairs. Every pair count
    /// from none to <see cref="LoadoutFramesUtils.MaximumSkills"/> has one.
    /// </returns>
    public static byte[]? RecordedLoadoutFor(int skillCount) => skillCount switch
    {
        PlayerLoadoutSkillCount => PlayerLoadout(),
        SingleLoadoutSkillCount => SingleLoadout(),
        SecondLoadoutSkillCount => SecondLoadout(),
        ThirdLoadoutSkillCount => ThirdLoadout(),
        FourthLoadoutSkillCount => FourthLoadout(),
        _ => null,
    };
}
