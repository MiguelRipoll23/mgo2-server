using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codecs of the invitation flow. The invite reply and the answer reply carry
/// fixed sizes, so each is asserted; the notification is a fixed 32 bytes the
/// client reads a state byte out of.
/// </summary>
public static class EventInvitationUtils
{
    /// <summary>Size of an invitation notification.</summary>
    public const int NotificationWireSize = 32;

    /// <summary>Size of an answer reply.</summary>
    public const int AnswerResponseWireSize = 9;

    /// <summary>Writes the invite reply, which is a result then one entry per target.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="result">Result word.</param>
    /// <param name="items">Per-target results, which are only written on success.</param>
    public static void WriteInviteResponse(
        PacketWriter writer,
        int result,
        IReadOnlyList<(int TargetCharacterIdentifier, int Result)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        writer.WriteInt32(result);
        if (result != 0)
        {
            return;
        }

        writer.WriteInt32(items.Count);
        foreach (var item in items)
        {
            writer.WriteInt32(item.TargetCharacterIdentifier);
            writer.WriteInt32(item.Result);
        }
    }

    /// <summary>Writes an invitation notification, asserting its size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="lobbyIdentifier">Lobby the invitation belongs to.</param>
    /// <param name="identifier">Invitation identifier, or the target's character identifier.</param>
    /// <param name="timestamp">Second the invitation was created at.</param>
    /// <param name="state">State the client displays.</param>
    /// <param name="mode">Mode byte.</param>
    /// <param name="opaque">Unused word.</param>
    /// <param name="characterName">Name of the other party.</param>
    public static void WriteNotification(
        PacketWriter writer,
        int lobbyIdentifier,
        int identifier,
        long timestamp,
        int state,
        int mode,
        int opaque,
        string characterName)
    {
        var start = writer.Size;
        writer.WriteUInt16(lobbyIdentifier);
        writer.WriteInt32(identifier);
        writer.WriteUInt32((uint)timestamp);
        writer.WriteUInt8(state);
        writer.WriteUInt8(mode);
        writer.WriteInt32(opaque);
        writer.WriteFixedString(characterName ?? string.Empty, 16);

        var written = writer.Size - start;
        if (written != NotificationWireSize)
        {
            throw new InvalidOperationException(
                $"An invitation notification is {written} bytes, expected {NotificationWireSize}.");
        }
    }

    /// <summary>Writes an answer reply, asserting its size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="result">Result word.</param>
    /// <param name="invitationIdentifier">Invitation that was answered.</param>
    /// <param name="state">Choice the target made.</param>
    public static void WriteAnswerResponse(
        PacketWriter writer,
        int result,
        int invitationIdentifier,
        int state)
    {
        var start = writer.Size;
        writer.WriteInt32(result);
        writer.WriteInt32(invitationIdentifier);
        writer.WriteUInt8(state);

        var written = writer.Size - start;
        if (written != AnswerResponseWireSize)
        {
            throw new InvalidOperationException(
                $"An answer reply is {written} bytes, expected {AnswerResponseWireSize}.");
        }
    }
}
