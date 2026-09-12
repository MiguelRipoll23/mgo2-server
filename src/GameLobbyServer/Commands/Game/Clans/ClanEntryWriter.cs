using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Builds the clan list entries shared by several handlers.</summary>
internal static class ClanEntryWriter
{
    /// <summary>Maximum number of entries per page.</summary>
    public const int MaximumPerPacket = 15;

    /// <summary>Writes one clan list entry.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="clan">Clan to write.</param>
    public static void WriteClanListEntry(PacketWriter writer, ClanListEntry clan)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        writer.WriteUInt32((uint)clan.ClanIdentifier);
        writer.WriteFixedString(clan.ClanName, 16);
        writer.WriteUInt32((uint)clan.LeaderCharacterIdentifier);
        writer.WriteFixedString(clan.LeaderCharacterName, 16);
        writer.WriteUInt8(0);
        writer.WritePadding(3);
        writer.WriteUInt32((uint)now);
    }
}
