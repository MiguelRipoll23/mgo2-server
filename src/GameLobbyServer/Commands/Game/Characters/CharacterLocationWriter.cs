using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Writes the location block three packets carry: the friends and blocked
/// roster, the player search and the clan roster all end a row with which lobby
/// a character is in, which room they are in and what kind of lobby it is.
/// <para>
/// The block is served from the recorded presence rather than from this
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

    /// <summary>Size of one written block.</summary>
    public const int Size = sizeof(ushort) + NameLength + sizeof(uint) + NameLength + sizeof(byte);

    /// <summary>
    /// Writes the location of a character, or the empty block when they are not
    /// connected anywhere.
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
}
