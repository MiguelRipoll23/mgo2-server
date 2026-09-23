using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Writes the location a roster or search row ends with: which lobby a
/// character is in, which room they are in and what kind of lobby it is.
/// <para>
/// Two shapes live here, because the 1.36 client does not read the same record
/// for every screen. The player search (<c>0x4602</c>) and the clan roster
/// (<c>0x4b54</c>) carry the full five-field block, lobby name included. The
/// friends/blocked roster (<c>0x4582</c>) carries a shorter tail with no lobby
/// name — the client reads its lobby id and resolves the name itself — and its
/// record is 43 bytes rather than 59. Writing the full block there left 16
/// bytes over, which the client's record loop read as a second, all-zero row:
/// the record loop bounds against the receive buffer rather than the payload, so
/// a wrong width shifts rows instead of failing.
/// </para>
/// <para>
/// The split is 1.36's, read from the built client's own parsers: <c>0xf14570</c>
/// reads six fields into the struct slots the disc build fills with seven
/// (<c>0</c>, <c>4</c>, <c>0x16</c>, <c>0x2c</c>, <c>0x30</c>, <c>0x41</c> — the
/// <c>0x18</c> slot the lobby name occupies is left zeroed), while <c>0xf13cd0</c>
/// and <c>0xf28ab4</c> still read the full block for the search and the clan
/// roster. See <c>docs/BUILD_1_36.md</c>.
/// </para>
/// <para>
/// The location is served from the recorded presence rather than from this
/// process's connections, because a roster lists players who are connected to
/// other lobbies as well as this one. A character with no recorded presence
/// gets zeros and empty names, which the client renders as "not connected" — it
/// does not remove the row, so an offline friend still appears on the list.
/// </para>
/// </summary>
public static class CharacterLocationWriter
{
    /// <summary>Length of a lobby or room name field.</summary>
    private const int NameLength = 16;

    /// <summary>Size of one full written block: search and clan roster rows.</summary>
    public const int Size = sizeof(ushort) + NameLength + sizeof(uint) + NameLength + sizeof(byte);

    /// <summary>
    /// Size of the friends/blocked roster tail: the lobby id, the game id, the
    /// game name and the lobby label, without the lobby name.
    /// </summary>
    public const int RosterSize = sizeof(ushort) + sizeof(uint) + NameLength + sizeof(byte);

    /// <summary>
    /// Writes the full location block of the player search and clan roster.
    /// </summary>
    /// <param name="writer">Writer the block is appended to.</param>
    /// <param name="location">Where the character is, or null when nowhere.</param>
    public static void Write(PacketWriter writer, CharacterLocation? location)
    {
        // Written field by field rather than padded to Size, so the two branches
        // cannot drift apart in width: a short block would shift every field of
        // every row that follows it.
        writer.WriteUInt16((ushort)(location?.LobbyIdentifier ?? 0));
        writer.WriteFixedString(location?.LobbyName ?? string.Empty, NameLength);
        writer.WriteUInt32((uint)(location?.GameIdentifier ?? 0));
        writer.WriteFixedString(location?.GameName ?? string.Empty, NameLength);
        writer.WriteUInt8((byte)(location is null ? 0 : CharacterLocationUtils.LobbyLabel(location.LobbySubtype)));
    }

    /// <summary>
    /// Writes the shorter tail the friends/blocked roster row ends with: the lobby
    /// id, the game id, the game name and the lobby label, with no lobby name. The
    /// 1.36 client does not read one from this record, and a name written here is
    /// read as the next row's first field — which is where the blank row came from.
    /// </summary>
    /// <param name="writer">Writer the tail is appended to.</param>
    /// <param name="location">Where the character is, or null when nowhere.</param>
    public static void WriteRoster(PacketWriter writer, CharacterLocation? location)
    {
        writer.WriteUInt16((ushort)(location?.LobbyIdentifier ?? 0));
        writer.WriteUInt32((uint)(location?.GameIdentifier ?? 0));
        writer.WriteFixedString(location?.GameName ?? string.Empty, NameLength);
        writer.WriteUInt8((byte)(location is null ? 0 : CharacterLocationUtils.LobbyLabel(location.LobbySubtype)));
    }
}
