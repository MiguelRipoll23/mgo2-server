using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Reply helpers used by the command handlers. Each one draws the session's
/// outbound sequence number and advances it, exactly once per packet.
/// </summary>
/// <param name="packetCodec">Codec the replies are encoded with.</param>
public sealed class SessionHelper(PacketCodecService packetCodec)
{
    /// <summary>Sends a packet with a payload, or with an empty payload when none is given.</summary>
    /// <param name="session">Connection to write to.</param>
    /// <param name="commandId">Command identifier of the reply.</param>
    /// <param name="payload">Payload bytes, when the reply carries any.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendPacketAsync(
        TcpSession session,
        ushort commandId,
        byte[]? payload = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = packetCodec.EncodePacket(
            commandId,
            payload ?? [],
            session.SequenceOut,
            session.LogPrefix);

        session.SequenceOut++;
        await session.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Sends the four-byte marker payload that opens or closes a multi-packet
    /// reply.
    /// </summary>
    /// <param name="session">Connection to write to.</param>
    /// <param name="commandId">Command identifier of the marker.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task SendStartEndPacketAsync(
        TcpSession session,
        ushort commandId,
        CancellationToken cancellationToken = default) =>
        SendPacketAsync(session, commandId, new byte[4], cancellationToken);

    /// <summary>
    /// Sends an explicit result word. An empty payload does not fail the client,
    /// because its readers bound-check the receive buffer rather than the
    /// payload length, but the explicit word is the shape the result-style
    /// parsers read.
    /// </summary>
    /// <param name="session">Connection to write to.</param>
    /// <param name="commandId">Command identifier of the reply.</param>
    /// <param name="result">Result code to send.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendResultAsync(
        TcpSession session,
        ushort commandId,
        uint result,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[4];
        BinaryUtility.WriteUInt32BigEndian(payload, 0, result);
        await SendPacketAsync(session, commandId, payload, cancellationToken);
    }

    /// <summary>Sends a packet with an empty payload.</summary>
    /// <param name="session">Connection to write to.</param>
    /// <param name="commandId">Command identifier of the reply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task SendAcknowledgementAsync(
        TcpSession session,
        ushort commandId,
        CancellationToken cancellationToken = default) =>
        SendPacketAsync(session, commandId, null, cancellationToken);

    /// <summary>Sends a packet carrying a masked error code.</summary>
    /// <param name="session">Connection to write to.</param>
    /// <param name="commandId">Command identifier of the reply.</param>
    /// <param name="errorCode">Error code to send.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendErrorAsync(
        TcpSession session,
        ushort commandId,
        uint errorCode,
        CancellationToken cancellationToken = default)
    {
        var bytes = packetCodec.EncodeErrorPacket(
            commandId,
            errorCode,
            session.SequenceOut,
            session.LogPrefix);

        session.SequenceOut++;
        await session.WriteAsync(bytes, cancellationToken);
    }
}
