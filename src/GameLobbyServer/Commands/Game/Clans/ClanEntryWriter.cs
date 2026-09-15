using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Builds the clan list entries shared by several handlers.</summary>
internal static class ClanEntryWriter
{
    /// <summary>Maximum number of entries per packet, bounded by the payload limit.</summary>
    public const int MaximumPerPacket = 15;

    /// <summary>
    /// Size of one list record. The client's array holds a hundred of them, which
    /// is why the page the list is fetched in is a hundred entries rather than a
    /// packet's worth.
    /// </summary>
    public const int RecordSize = 48;

    /// <summary>Length of a clan or character name field.</summary>
    private const int NameLength = 16;

    /// <summary>Writes one clan list record.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="clan">Clan to write.</param>
    public static void WriteClanListEntry(PacketWriter writer, ClanListEntry clan)
    {
        var start = writer.Size;
        writer.WriteUInt32((uint)clan.ClanIdentifier);
        writer.WriteFixedString(clan.ClanName, NameLength);
        // Member count, not the leader's identifier: this word and the name below
        // are the two facts a list row carries, and a row that names its leader
        // without saying how many members the clan has is the reading that renders
        // nothing useful.
        writer.WriteUInt32((uint)clan.MemberCount);
        writer.WriteFixedString(clan.LeaderCharacterName, NameLength);
        writer.WritePadding(4);
        writer.WriteUInt32((uint)clan.CreationTime);
        writer.WritePadding(RecordSize - (writer.Size - start));
    }
}
