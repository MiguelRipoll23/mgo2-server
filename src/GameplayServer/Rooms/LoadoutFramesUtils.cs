using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Writes a player's own fields into a loadout record: the slot byte, the side,
/// the trailing appearance block and the skill pairs, at the offsets the replay
/// console's decoder reads them from (<c>rpdt.js</c>'s <c>appearanceRecord</c>
/// and <c>skillRecord</c>, mapped in <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the fields the decoder names are written; the rest of a body is a
/// recorded template's.</b> A loadout carries three regions nothing names: the
/// nineteen bytes at <c>0x03</c>, the four bytes at <c>0x12</c>, and the two
/// bytes between the skill list's end and the appearance block. Measured over the
/// twenty bodies the captures hold, the last two belong to the player and not to
/// the appearance — game1's slot 20 and game2's slot 1 carry the same appearance,
/// the same skills and the same four leading bytes and differ in that tail alone
/// (<c>05 78</c> against <c>05 7b</c>) — so they are per-player values this host
/// has no source for. They therefore stay as the recorded body has them, and the
/// claim this file makes is about the fields it writes rather than about a body
/// built from nothing. The template passed in must have the shape
/// (<see cref="SkillCountOf"/>) the player's own skills do.
/// </para>
/// <para>
/// <b>The slot byte is the room's own table key plus one.</b> The captured bodies
/// name the host's entry at <c>0x01</c> — the dedicated host holds key zero in its
/// own player table — and the joiners at <c>0x05</c>, <c>0x07</c>, <c>0x0c</c>,
/// <c>0x10</c> and <c>0x15</c>, which are exactly the keys §8.4 of
/// <c>UDP_GAME_CAPTURE.md</c> reads out of the same capture's roster records. So
/// the byte is the session slot and not a roster position, and a host that
/// numbered the players itself would name slots they were not given.
/// </para>
/// <para>
/// <b>The side is the one field here whose coding is measured across files.</b>
/// rpdt.js aligns its lobby-array team groups against this byte over 30 recordings
/// and reports group 0 as value <c>0</c> and group 1 as value <c>2</c> for 323 of
/// 323 players that carry both, which is why the byte is written as twice the side
/// the room's rows state and not as the side itself. The same decoder warns that a
/// body whose byte is the same on every slot is the unassigned default rather than
/// a real assignment — every captured loadout of game1 carries <c>02</c> — so a
/// player the room's rows put on no side keeps that recorded default.
/// </para>
/// </remarks>
public static class LoadoutFramesUtils
{
    /// <summary>Offset of the record's slot byte, which is the player's key plus one.</summary>
    public const int SlotByteOffset = 0x01;

    /// <summary>Offset of the record's side byte.</summary>
    public const int SideByteOffset = 0x02;

    /// <summary>Offset of the byte that sizes the run of bytes before the appearance block.</summary>
    public const int SkillCountOffset = 0x16;

    /// <summary>Offset of the skill region's first byte, which a skills-bearing body opens with <c>0xff</c>.</summary>
    public const int SkillRegionOffset = 0x17;

    /// <summary>Byte the skill region ends on: the decoder's list end.</summary>
    public const byte SkillListEnd = 0xff;

    /// <summary>
    /// Byte a region may end on before the list end, which is not part of a pair
    /// and is dropped by the decoder when it is present.
    /// </summary>
    public const byte SkillRegionSuffix = 0x10;

    /// <summary>Length of the appearance block: the body's trailing 23 bytes.</summary>
    public const int AppearanceLength = 23;

    /// <summary>
    /// Skill pairs a loadout record can carry: three. The game's own bodies carry
    /// none, two or three, and the body's length follows from the count, so a
    /// player with four equipped skills has no shape to be written in and is
    /// announced with the recorded body instead.
    /// </summary>
    public const int MaximumSkills = 3;

    /// <summary>Highest skill identifier the replay parser's list names.</summary>
    public const int MaximumSkillIdentifier = 25;

    /// <summary>Highest level a skill pair may carry.</summary>
    public const int MaximumSkillLevel = 3;

    /// <summary>
    /// The side byte a body carries when the room's rows state no side for the
    /// player: the value every loadout of the dedicated host's own capture
    /// carries, which the replay decoder reads as the unassigned default rather
    /// than as a team.
    /// </summary>
    public const byte UnassignedSideByte = 0x02;

    /// <summary>
    /// The number of skill pairs a template carries, read the way the decoder
    /// reads it: the byte at <see cref="SkillCountOffset"/> sizes the run before
    /// the appearance block, the list ends on <see cref="SkillListEnd"/>, an
    /// optional suffix is dropped, and the pairs fill what is left.
    /// </summary>
    /// <param name="template">A recorded loadout body.</param>
    /// <returns>The pair count, or <c>-1</c> when the body is not that shape.</returns>
    public static int SkillCountOf(ReadOnlySpan<byte> template)
    {
        if (template.Length <= SkillCountOffset + 1
            || template.Length <= AppearanceLength + 1
            || template[0] != PlayerSettingsBurstService.SettingsPayloadType)
        {
            return -1;
        }

        var count = template[SkillCountOffset];
        var end = template.Length - AppearanceLength - count - 1;
        if (end < SkillRegionOffset || end >= template.Length || template[end] != SkillListEnd)
        {
            return -1;
        }

        var region = end - SkillRegionOffset;
        if (region > 0 && template[SkillRegionOffset + region - 1] == SkillRegionSuffix)
        {
            region--;
        }

        return region < 2 ? 0 : region / 2;
    }

    /// <summary>
    /// Writes a player's own fields into a copy of a recorded body of the same
    /// shape.
    /// </summary>
    /// <param name="loadout">The player's slot, side, appearance and skills.</param>
    /// <param name="template">
    /// A recorded body carrying the same number of skill pairs as the player has.
    /// It supplies everything the decoder does not name.
    /// </param>
    /// <param name="body">The body to send, when the shape fits.</param>
    /// <returns>
    /// <c>true</c> when the body was built; <c>false</c> when the player's skills
    /// are a shape no captured body has, which is what the caller falls back on.
    /// </returns>
    public static bool TryWrite(LobbyLoadout loadout, ReadOnlySpan<byte> template, out byte[] body)
    {
        ArgumentNullException.ThrowIfNull(loadout);

        body = [];
        if (loadout.Appearance.Length != AppearanceLength
            || loadout.Skills.Count != SkillCountOf(template)
            || loadout.Skills.Count > MaximumSkills)
        {
            return false;
        }

        var built = template.ToArray();
        built[SlotByteOffset] = (byte)(loadout.Slot + 1);
        built[SideByteOffset] = SideByteOf(loadout.Side);

        // The pairs sit directly behind the region's leading byte, which is what
        // the recorded bodies hold: a region of five bytes is `ff` and two pairs,
        // and the decoder reads them from the region's end back to the front.
        for (var index = 0; index < loadout.Skills.Count; index++)
        {
            built[SkillRegionOffset + 1 + (index * 2)] = loadout.Skills[index].Identifier;
            built[SkillRegionOffset + 1 + (index * 2) + 1] = loadout.Skills[index].Level;
        }

        loadout.Appearance.CopyTo(built, built.Length - AppearanceLength);
        body = built;
        return true;
    }

    /// <summary>
    /// The side byte a body carries for a side the room's rows state, or the
    /// recorded default when they state none.
    /// </summary>
    /// <param name="side">Team slot the room holds, or <c>null</c>.</param>
    /// <returns>The byte to write.</returns>
    public static byte SideByteOf(short? side) =>
        side is 0 or 1 ? (byte)(side.Value * 2) : UnassignedSideByte;

    /// <summary>
    /// A character's saved appearance as the record's trailing block: the 23
    /// fields <c>rpdt.js</c> names, in its order.
    /// </summary>
    /// <remarks>
    /// <b>The order is the network block's and not this repository's.</b>
    /// <c>PROTOCOL.md</c>'s appearance struct runs <c>gender, face, upper, …</c>;
    /// this block runs <c>head, chest, hand, waist, foot, accessories, colours,
    /// gender, face, upper, lower, face paint, voice, pitch</c> and has no four
    /// zero bytes — byte 0 is the head here. <c>UDP_SETTINGS_PAYLOAD.md</c> carries
    /// the field table; the two orders are not interchangeable.
    /// </remarks>
    /// <param name="appearance">The character's saved appearance.</param>
    /// <param name="block">The 23-byte block, when every field fits a byte.</param>
    /// <returns>
    /// <c>true</c> when the block was built. A row outside a byte's range is a row
    /// this record cannot express, and the caller falls back rather than truncating
    /// it onto the wrong face.
    /// </returns>
    public static bool TryAppearanceBlock(CharacterAppearance appearance, out byte[] block)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        int[] values =
        [
            appearance.Head,
            appearance.Chest,
            appearance.Hands,
            appearance.Waist,
            appearance.Feet,
            appearance.Accessory1,
            appearance.Accessory2,
            appearance.HeadColor,
            appearance.ChestColor,
            appearance.HandsColor,
            appearance.WaistColor,
            appearance.FeetColor,
            appearance.Gender,
            appearance.Face,
            appearance.Upper,
            appearance.Lower,
            appearance.FacePaint,
            appearance.UpperColor,
            appearance.LowerColor,
            appearance.Voice,
            appearance.Pitch,
            appearance.Accessory1Color,
            appearance.Accessory2Color,
        ];

        block = new byte[AppearanceLength];
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] is < byte.MinValue or > byte.MaxValue)
            {
                block = [];
                return false;
            }

            block[index] = (byte)values[index];
        }

        return true;
    }

    /// <summary>
    /// A character's equipped skills as the record's pairs, in slot order.
    /// </summary>
    /// <remarks>
    /// An empty slot is a zero identifier and is skipped; a slot that names a skill
    /// outside the parser's list, or a level outside 1 to 3, makes the whole record
    /// unreadable there, so it is reported as such rather than written. A character
    /// with no row saved has no skills, which is a loadout the game's own bodies
    /// carry and not a failure.
    /// </remarks>
    /// <param name="equipped">The character's equipped skills, or <c>null</c>.</param>
    /// <param name="skills">The pairs, when they fit the record.</param>
    /// <returns><c>true</c> when the pairs can be written.</returns>
    public static bool TrySkillsOf(CharacterEquippedSkill? equipped, out List<LobbySkill> skills)
    {
        skills = [];

        if (equipped is null)
        {
            return true;
        }

        (int Identifier, int Level)[] slots =
        [
            (equipped.Skill1, equipped.Level1),
            (equipped.Skill2, equipped.Level2),
            (equipped.Skill3, equipped.Level3),
            (equipped.Skill4, equipped.Level4),
        ];

        foreach (var (identifier, level) in slots)
        {
            if (identifier == 0)
            {
                continue;
            }

            if (identifier is < 1 or > MaximumSkillIdentifier || level is < 1 or > MaximumSkillLevel)
            {
                skills = [];
                return false;
            }

            skills.Add(new LobbySkill((byte)identifier, (byte)level));
        }

        return true;
    }
}
