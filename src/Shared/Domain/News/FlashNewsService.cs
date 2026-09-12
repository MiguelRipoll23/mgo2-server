using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.News;

/// <summary>Subcommand values carried in bytes three and four of the ticker payload.</summary>
public static class FlashNewsSubcommand
{
    /// <summary>Server ticker message; the client displays the message text.</summary>
    public const ushort ServerMessage = 0x4a68;

    /// <summary>Emergency maintenance notice; the client ignores the message text.</summary>
    public const ushort EmergencyMaintenance = 0x4a71;
}

/// <summary>One ticker announcement.</summary>
/// <param name="Message">Text shown in the client's ticker.</param>
/// <param name="Unknown1">First byte of the leading field, whose purpose is unknown.</param>
/// <param name="Unknown2">Second byte of the leading field, whose purpose is unknown.</param>
/// <param name="Subcommand">Subcommand that selects the client behaviour.</param>
/// <param name="Unknown5">Fifth byte of the payload, whose purpose is unknown.</param>
/// <param name="MaintenanceTime">Maintenance start time, used by the emergency subcommand.</param>
public sealed record FlashNewsAnnouncement(
    string Message = "",
    byte Unknown1 = 0x00,
    byte Unknown2 = 0x00,
    ushort Subcommand = FlashNewsSubcommand.ServerMessage,
    byte Unknown5 = 0x01,
    byte MaintenanceTime = 0x00);

/// <summary>Outcome of a ticker broadcast.</summary>
/// <param name="Recipients">Number of clients the announcement was written to.</param>
/// <param name="PayloadLength">Length of the payload that was written.</param>
public sealed record FlashNewsBroadcastResult(int Recipients, int PayloadLength);

/// <summary>
/// Builds the ticker packet and writes it to the gameplay lobby clients the
/// server currently serves.
/// </summary>
/// <param name="activeGameSessions">Sessions of the running lobby instances.</param>
/// <param name="sessionHelper">Helper used to write the packets.</param>
public sealed class FlashNewsService(
    ActiveGameSessionsService activeGameSessions,
    SessionHelper sessionHelper)
{
    /// <summary>Command the ticker is written with.</summary>
    private const ushort FlashNewsCommand = 0x4a50;

    /// <summary>Zero-filled padding that precedes the message text.</summary>
    private const int PrefixPaddingLength = 7;

    /// <summary>Builds the payload of one announcement.</summary>
    /// <param name="announcement">Announcement to encode.</param>
    public static byte[] BuildPayload(FlashNewsAnnouncement announcement)
    {
        var writer = new PacketWriter();
        writer
            .WriteUInt8(announcement.Unknown1)
            .WriteUInt8(announcement.Unknown2)
            .WriteUInt8(announcement.Subcommand >> 8)
            .WriteUInt8(announcement.Subcommand & 0xff)
            .WriteUInt8(announcement.Unknown5)
            .WriteUInt8(announcement.MaintenanceTime)
            .WritePadding(PrefixPaddingLength)
            .WriteBytes(System.Text.Encoding.UTF8.GetBytes(announcement.Message));

        return writer.Build();
    }

    /// <summary>Broadcasts an announcement to every client of this process's lobbies.</summary>
    /// <param name="announcement">Announcement to broadcast.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FlashNewsBroadcastResult> BroadcastAsync(
        FlashNewsAnnouncement announcement,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(announcement);
        var sessions = activeGameSessions.List();

        foreach (var session in sessions)
        {
            await sessionHelper.SendPacketAsync(session, FlashNewsCommand, payload, cancellationToken);
        }

        return new FlashNewsBroadcastResult(sessions.Count, payload.Length);
    }
}
