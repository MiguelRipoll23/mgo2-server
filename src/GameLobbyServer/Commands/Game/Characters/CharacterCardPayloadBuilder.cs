using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Builds the card a player opens on another character's name, the single reply to
/// the player-details request (<c>0x4220</c>).
/// <para>
/// The layout is the 1.36 client's, read field by field from its own parser
/// (<c>0xf0a2ec</c>): the disc build's 201 bytes, then the word, the byte and the
/// feature byte it reads past them. The offsets below are wire offsets, and the
/// client's own parser destinations are named where they matter.
/// </para>
/// <para>
/// The clan is a <c>{u4 id, 16B name, u1 state}</c> triple at <c>0xa7</c>,
/// <c>0xab</c> and <c>0xbb</c> — the offsets a clan occupies everywhere else in
/// this protocol, and the ones <c>0x4101</c> and <c>0x4103</c> use on their own
/// blocks. Every reader of the triple tests the identifier before the name and
/// treats a zero identifier as "no clan" whatever the name says, so the name
/// alone never renders and an identifier invented to sit beside it is looked up.
/// </para>
/// </summary>
public static class CharacterCardPayloadBuilder
{
    /// <summary>
    /// Exact size of the card payload: the disc build's 201 bytes plus the word, the
    /// byte and the feature byte 1.36 reads after them. A short payload is not a hole;
    /// the client abandons the record at the first read past its end.
    /// </summary>
    public const int Size = 207;

    /// <summary>Length of the character name field.</summary>
    private const int NameLength = 16;

    /// <summary>Length of the comment field.</summary>
    private const int CommentLength = 128;

    /// <summary>Length of the clan name inside the clan triple.</summary>
    private const int ClanNameLength = 16;

    /// <summary>
    /// Third member of the clan triple for a character who belongs to one. Its readers
    /// test <c>state - 1 &lt;= 1</c>, so a member and a leader take the same branch;
    /// this service does not distinguish them, so the member value is the honest one.
    /// </summary>
    private const int MemberState = 1;

    /// <summary>
    /// The clan emblem flag the client tests for: three means the clan has a published
    /// emblem, in which case the card fetches the image. Zero is not "no picture" but
    /// "never ask", which is why it is the value for a clan without one.
    /// </summary>
    private const int EmblemOnDisplay = 3;

    /// <summary>
    /// The refusal shape: the result word alone. A nonzero result makes the client skip
    /// every field below it, so a card that cannot be served is four bytes rather than a
    /// padded record — the same answer, said once.
    /// <para>
    /// The codes are the client's own literals and go out unmasked. Its error table is
    /// keyed on those values: <c>-266</c> is the one that raises a sentence of its own
    /// ("Designated character has been deleted and no longer exists"), and every other
    /// nonzero value collapses into the generic "Unable to acquire character information".
    /// A masked code matches nothing and so is always the vague one.
    /// </para>
    /// </summary>
    /// <param name="resultCode">Result word the reply carries.</param>
    public static byte[] BuildResult(uint resultCode)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(resultCode);
        return writer.Build();
    }

    /// <summary>
    /// Builds the card payload. The character is required rather than nullable: a card for a
    /// character who is not there is not a card, it is the refusal above, and making the two
    /// states the same shape is how a deleted character came to be served a placeholder.
    /// </summary>
    /// <param name="character">Character the card describes.</param>
    /// <param name="targetIdentifier">Identifier the request asked for, echoed back as the card's own.</param>
    /// <param name="clan">Clan of that character, when it belongs to one.</param>
    public static byte[] Build(Character character, int targetIdentifier, CharacterClanInformation? clan)
    {
        var experience = character.Experience;
        var writer = new PacketWriter();

        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)targetIdentifier);
        writer.WriteFixedString(character.Name, NameLength);
        // Wire 0x18 is the experience, and it is the card's whole source for its LEVEL:
        // the figure on the screen is this number walked through the client's own
        // threshold table, so a level sent as its own field would not be read at all.
        writer.WriteUInt32((uint)experience);
        // Wire 0x1c and 0x1d, the privilege nibble and the beginner flag. Both reach the
        // card's own block, whose readers take the local one, so zero here cannot affect
        // this player's own lobby entry and is the checked-correct value.
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        // Wire 0x1e is the client's total_rewards slot, the card's bit-2-gated
        // "TOTAL REWARDS" figure. It is the character's own reward total and not the
        // round score, which is what used to land here.
        writer.WriteUInt32((uint)character.TotalRewards);
        writer.WriteUInt32(0);
        // Wire 0x26 is the worn title: 1-based, zero for none, and the only thing the
        // card draws its title badge from. It is the rank the title service latched, the
        // same figure the personal-stats header and the 0x4122 write-back carry, so the
        // card and the stats screen cannot disagree about which title is worn.
        writer.WriteUInt8((byte)Math.Clamp(character.Rank, 0, byte.MaxValue));
        writer.WriteFixedString(character.Comment, CommentLength);
        writer.WriteUInt32((uint)(clan?.ClanIdentifier ?? 0));
        writer.WriteFixedString(clan?.ClanName ?? string.Empty, ClanNameLength);
        writer.WriteUInt8(clan is not null ? MemberState : 0);
        // Wire 0xbc and 0xc0, the host-rating numerator and denominator. The gauge returns
        // zero when either half is, so an unfilled pair reads as "no bar" rather than as a
        // wrong one.
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt8(clan?.HasEmblem == true ? EmblemOnDisplay : 0);
        // Grade points, which this server defines as the character's accumulated
        // experience, so the two fields carry the same number here as they do on 0x4129.
        writer.WriteUInt32((uint)experience);
        // The six bytes 1.36 reads past the disc build's last field: a word and a byte
        // below grade points, then the feature byte the client splits into its flag set.
        // None of the three is filled — a guess in a field whose consumer is unknown is
        // not neutral.
        writer.WriteUInt32(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);

        if (writer.Size < Size)
        {
            writer.WritePadding(Size - writer.Size);
        }

        return writer.Build();
    }
}
