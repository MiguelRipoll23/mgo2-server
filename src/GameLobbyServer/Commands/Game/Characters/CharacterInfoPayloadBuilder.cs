using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Builds the character header the connect burst starts with (0x4101). The client's
/// parser reads it as one flat record: the identifier, the name, the four dead
/// 16-bit constants, the experience, the two login stamps, the two identifier grids
/// and the tail of availability flags.
/// </summary>
public static class CharacterInfoPayloadBuilder
{
    /// <summary>
    /// Offset of the sixteen-byte map and rule availability mask, one past the
    /// tail byte that follows the friend and blocked grids.
    /// </summary>
    private const int ContentMaskOffset = 0x22a;

    /// <summary>Offset of the trailing feature byte, one past the fixed grid.</summary>
    private const int FeatureByteOffset = 0x242;

    /// <summary>
    /// Number of identifiers in each friend and blocked array. The client reads
    /// 64 (its loops compare against 0x40), so each array is 256 bytes: a short
    /// payload shifts the feature byte into the friend grid.
    /// </summary>
    private const int MaximumListIdentifiers = 64;

    /// <summary>The four dead 16-bit constants that follow the name.</summary>
    private static readonly byte[] FixedBytes =
    [
        0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
    ];

    /// <summary>
    /// Builds the payload. The experience is the character's own total, which is what
    /// the client's parser stores at its record offset <c>0x120</c> and derives the
    /// displayed level from — the wire has always carried it per character.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="characterName">Name of the character.</param>
    /// <param name="experience">Experience of the character.</param>
    /// <param name="previousLoginTime">The login this one replaced.</param>
    /// <param name="lastLoginTime">The login being reported.</param>
    /// <param name="friends">Identifiers of the character's friends.</param>
    /// <param name="blocked">Identifiers of the characters it blocked.</param>
    public static byte[] Build(
        int characterIdentifier,
        string characterName,
        int experience,
        int previousLoginTime,
        int lastLoginTime,
        IReadOnlyList<int> friends,
        IReadOnlyList<int> blocked)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)characterIdentifier);
        writer.WriteFixedString(characterName, 16);
        writer.WriteBytes(FixedBytes);
        writer.WriteUInt32((uint)experience);
        // The client shows the previous login alongside the current one. A character who
        // has never logged in has neither; zero is what the client's own zero renders as.
        writer.WriteUInt32((uint)previousLoginTime);
        writer.WriteUInt32((uint)lastLoginTime);
        writer.WriteUInt8(0);

        for (var index = 0; index < MaximumListIdentifiers; index++)
        {
            writer.WriteUInt32((uint)(index < friends.Count ? friends[index] : 0));
        }

        for (var index = 0; index < MaximumListIdentifiers; index++)
        {
            writer.WriteUInt32((uint)(index < blocked.Count ? blocked[index] : 0));
        }

        // Tail the client reads past the two grids: a u8, the map and rule
        // availability mask, two reserved u32s and the feature byte.
        writer.WritePadding(ContentMaskOffset - writer.Size);
        writer.WriteBytes(FeatureFlags.ContentMask);
        writer.WritePadding(FeatureByteOffset - writer.Size);
        // The parser reads this byte's four low bits as separate feature flags
        // and greys out the expansion maps and modes when they are clear.
        writer.WriteUInt8(FeatureFlags.MainMenuFlags);
        return writer.Build();
    }
}

/// <summary>Feature bits the client reads one byte past the character grid.</summary>
public static class FeatureFlags
{
    /// <summary>
    /// Lets the client offer expansion content such as Team Sneaking without
    /// triggering the post-login tip modals. The low nibble splits into four
    /// flags (bit 0 <c>0x4184</c>, bit 1 <c>0x4185</c>, bit 2 <c>0x4187</c>,
    /// bit 3 <c>0x4186</c> in the splitter at <c>0xf06450</c>); bits 2 and 3
    /// each gate a one-time "welcome" help document that the main-menu state
    /// machine opens as soon as the character info is parsed: bit 2 opens help
    /// document 13 (<c>2_13.txt</c>, gate <c>0x98e208</c>) and bit 3 opens
    /// document 6 (<c>2_6.txt</c>, gate <c>0x98e2b0</c>).
    /// </summary>
    public const int MainMenuFlags = 0x00;

    /// <summary>
    /// Map, rule and expansion availability mask. The client reads it as a bit
    /// field in which bit 0 through bit 55 each stand for one selectable map or
    /// rule, and it offers the real row for a set bit and a greyed row whose
    /// name is the shipped <c>????</c> translation for a clear one. Every bit is
    /// set so the whole catalogue is offered; the trailing nine bytes are past
    /// the highest bit the client ever tests.
    /// </summary>
    public static readonly byte[] ContentMask =
    [
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];
}
