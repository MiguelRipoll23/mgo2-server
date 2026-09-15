using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// Serialises a board window into the binary reply the Rankings screens parse.
/// <para>
/// The reply is little-endian, which is the opposite order from every lobby
/// packet: a twelve-byte header (record count, board total, and a word the parser
/// reads and discards), then one twenty-eight-byte record per row (rank, subject
/// identifier, a sixteen-byte name and the value). The whole body is then
/// scrambled, because a cleartext reply is not rejected by the client — it is
/// silently misparsed.
/// </para>
/// <para>
/// The record count is taken from the rows actually serialised, never from what
/// was requested, because the client drops the whole reply when the count exceeds
/// what it asked for.
/// </para>
/// </summary>
public static class RankingBodyUtils
{
    /// <summary>Record count, board total, and the word the parser reads and throws away.</summary>
    public const int HeaderSize = 12;

    /// <summary>Rank, identifier, sixteen-byte name and value.</summary>
    public const int RecordSize = 28;

    /// <summary>Width of the name field, which the client copies into a buffer it never clears.</summary>
    public const int NameSize = 16;

    /// <summary>
    /// The client runs <c>strlen</c> over the name field, so a name that fills all
    /// sixteen bytes has no terminator and the length runs into whatever the stack
    /// frame held. Emitting at most fifteen characters guarantees a NUL.
    /// </summary>
    private const int MaximumNameLength = NameSize - 1;

    /// <summary>Serialises a board window and scrambles it.</summary>
    /// <param name="page">Window to serialise.</param>
    public static byte[] Encode(RankingPage page)
    {
        var body = new byte[HeaderSize + (RecordSize * page.Entries.Count)];

        BinaryUtility.WriteUInt32LittleEndian(body, 0, (uint)page.Entries.Count);
        BinaryUtility.WriteUInt32LittleEndian(body, 4, (uint)page.Total);
        BinaryUtility.WriteUInt32LittleEndian(body, 8, 0);

        var offset = HeaderSize;
        foreach (var entry in page.Entries)
        {
            BinaryUtility.WriteUInt32LittleEndian(body, offset, unchecked((uint)entry.Rank));
            BinaryUtility.WriteUInt32LittleEndian(body, offset + 4, unchecked((uint)entry.Identifier));
            WriteName(body, offset + 8, entry.Name);
            BinaryUtility.WriteUInt32LittleEndian(body, offset + 24, unchecked((uint)entry.Value));
            offset += RecordSize;
        }

        RankingScrambleUtils.Apply(body);
        return body;
    }

    /// <summary>Writes a name as sixteen bytes, NUL-padded and always terminated.</summary>
    /// <param name="body">Body being built.</param>
    /// <param name="offset">Offset of the name field.</param>
    /// <param name="name">Name to write.</param>
    private static void WriteName(Span<byte> body, int offset, string name)
    {
        var text = name.Length > MaximumNameLength ? name[..MaximumNameLength] : name;
        StringUtility.WriteFixedStringInto(body, offset, text, NameSize);
    }
}
