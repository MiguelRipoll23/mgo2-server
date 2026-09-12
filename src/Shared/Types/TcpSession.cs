using System.Net.Sockets;

namespace Mgo2Server.Shared.Types;

/// <summary>
/// State of one client connection: protocol position, the identities the client
/// has proved so far, and the stream packets are written to.
/// </summary>
public sealed class TcpSession
{
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>Role of the server that accepted the connection.</summary>
    public required ServerType ServerType { get; init; }

    /// <summary>Prefix used for the log lines of this connection.</summary>
    public required string LogPrefix { get; init; }

    /// <summary>Remote endpoint, formatted as "address:port".</summary>
    public required string RemoteAddress { get; init; }

    /// <summary>Stream packets are written to.</summary>
    public required NetworkStream Connection { get; init; }

    /// <summary>
    /// Sequence number of the next inbound packet. The first packet a client
    /// sends carries sequence zero.
    /// </summary>
    public uint SequenceIn { get; set; }

    /// <summary>
    /// Sequence number of the next outbound packet. The first packet the server
    /// sends carries sequence one.
    /// </summary>
    public uint SequenceOut { get; set; } = 1;

    /// <summary>Account the client proved ownership of, when it has.</summary>
    public int? UserIdentifier { get; set; }

    /// <summary>Character the client selected, when it has.</summary>
    public int? CharacterIdentifier { get; set; }

    /// <summary>Lobby the connection landed in, when the server hosts one.</summary>
    public int? LobbyIdentifier { get; set; }

    /// <summary>Room the client is in, when it joined one.</summary>
    public int? GameIdentifier { get; set; }

    /// <summary>Writes bytes to the connection, one writer at a time.</summary>
    /// <param name="bytes">Bytes to write.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public async Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await Connection.WriteAsync(bytes, cancellationToken);
            await Connection.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
