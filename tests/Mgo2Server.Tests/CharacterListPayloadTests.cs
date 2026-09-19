using System.Buffers.Binary;
using Mgo2Server.AccountLobbyServer.Commands;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the 0x3049 grid. Every size here comes from the client's own parser, so
/// a change silently moves the entitlement trailer out of the thirty-two bytes the
/// client copies to its session, which is what leaves an account without
/// expansion packs.
/// </summary>
public sealed class CharacterListPayloadTests
{
    /// <summary>Offset of the expansion bitmask inside the trailer.</summary>
    private const int ExpansionIndex = 1;

    /// <summary>Offset of the codec pack unlock inside the trailer.</summary>
    private const int CodecIndex = 3;

    /// <summary>Expansion bitmask: GENE (0x01), MEME (0x02) and SCENE (0x04).</summary>
    private const byte AllExpansions = 0x07;

    /// <summary>Codec voice pack unlock.</summary>
    private const byte CodecPacks = 0x03;

    /// <summary>Moment every countdown in these cases is measured against.</summary>
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>Offset of the trailing word inside one entry.</summary>
    private const int EntryCountdownOffset = 0x17 + 48;

    [Fact]
    public void Build_writes_the_grid_the_client_parses()
    {
        var payload = CharacterListPayloadBuilder.Build(characterSlots: 3, entries: [], Now);

        // Eight entries of fifty-two bytes plus the header and the trailer. The
        // client's loop compares its destination offset against 0x1a4 before it
        // advances by 0x3c, so it fills the eighth record as well.
        Assert.Equal(0x1d7, payload.Length);
        Assert.Equal(0x1b7, CharacterListPayloadBuilder.TrailerOffset);
        // A non-zero result word makes the client skip the list entirely.
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(payload));
        Assert.Equal(3, payload[4]);
        Assert.Equal(0, payload[5]);
        Assert.Equal(
            CharacterListPayloadBuilder.TrailerOffset + CharacterListPayloadBuilder.TrailerSize,
            payload.Length);
    }

    [Fact]
    public void Build_places_the_entitlements_where_the_client_reads_them()
    {
        var payload = CharacterListPayloadBuilder.Build(characterSlots: 8, entries: [], Now);
        var trailer = payload.AsSpan(CharacterListPayloadBuilder.TrailerOffset);

        Assert.Equal(CharacterListPayloadBuilder.TrailerSize, trailer.Length);
        Assert.Equal(AllExpansions, trailer[ExpansionIndex]);
        Assert.Equal(CodecPacks, trailer[CodecIndex]);
        // The remaining bytes are reserved: the client keeps them in its session
        // but no reader in the image consumes them.
        Assert.Equal(0, trailer[0]);
        Assert.Equal(0, trailer[2]);
        Assert.All(trailer[4..].ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void Build_serves_eight_entries_and_drops_the_rest()
    {
        var entries = Enumerable.Range(1, 10)
            .Select(identifier => new CharacterListPayloadBuilder.Entry(
                new Character { Identifier = identifier, Name = $"CHAR{identifier}" },
                Appearance: null,
                IsMain: identifier == 1))
            .ToList();

        var payload = CharacterListPayloadBuilder.Build(characterSlots: 10, entries, Now);

        Assert.Equal(0x1d7, payload.Length);
        // Slot byte, then the identifier, then the sixteenth byte name field.
        Assert.Equal(8, payload[5]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(0x17 + 1)));
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(0x17 + (7 * 52) + 1)));
        Assert.Equal(0, payload[0x17]);
        Assert.Equal(7, payload[0x17 + (7 * 52)]);
        Assert.Equal(8, CharacterListPayloadBuilder.SlotCount);
        // Everything past the eighth entry is padding up to the trailer.
        Assert.Equal(0, payload[0x17 + (8 * 52)]);
        // The main character is starred, as the client's main slot search expects.
        Assert.Equal((byte)'*', payload[0x17 + 5]);
    }

    [Fact]
    public void Build_star_keeps_the_main_name_inside_its_field()
    {
        var entry = new CharacterListPayloadBuilder.Entry(
            new Character { Identifier = 1, Name = "SIXTEEN_CHAR_NAME" },
            Appearance: null,
            IsMain: true);

        var payload = CharacterListPayloadBuilder.Build(characterSlots: 1, [entry], Now);

        var name = System.Text.Encoding.Latin1.GetString(payload.AsSpan(0x17 + 5, 16));

        Assert.Equal("*SIXTEEN_CHAR_NA", name);
    }

    /// <summary>
    /// The trailing word is the seconds left before the character may be deleted, and
    /// the client draws its wait screen from it. Serving zero here would promise a
    /// deletion the delete command then refuses, with no countdown to explain why.
    /// </summary>
    [Fact]
    public void Build_carries_the_seconds_left_before_a_character_may_be_deleted()
    {
        var entry = new CharacterListPayloadBuilder.Entry(
            new Character
            {
                Identifier = 1,
                Name = "YOUNG",
                CreatedAt = Now - TimeSpan.FromDays(2),
            },
            Appearance: null,
            IsMain: false);

        var payload = CharacterListPayloadBuilder.Build(characterSlots: 1, [entry], Now);

        // Five days of the seven-day cooldown are left, in whole seconds.
        Assert.Equal(
            (uint)TimeSpan.FromDays(5).TotalSeconds,
            BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(EntryCountdownOffset)));
    }

    /// <summary>A character past the cooldown carries no countdown, which reads as deletable.</summary>
    [Fact]
    public void Build_serves_no_countdown_once_a_character_is_old_enough()
    {
        var entry = new CharacterListPayloadBuilder.Entry(
            new Character
            {
                Identifier = 1,
                Name = "SETTLED",
                CreatedAt = Now - TimeSpan.FromDays(30),
            },
            Appearance: null,
            IsMain: false);

        var payload = CharacterListPayloadBuilder.Build(characterSlots: 1, [entry], Now);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(EntryCountdownOffset)));
    }
}
