using System.Net.Sockets;

namespace Mgo2Server.Shared.Types;

/// <summary>
/// State of one client connection: protocol position, the identities the client
/// has proved so far, and the stream packets are written to.
/// </summary>
public sealed class TcpSession
{
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private int sequenceOut;

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
    /// Sequence number of the most recently reserved outbound packet. The first
    /// packet the server sends carries sequence one.
    /// </summary>
    public uint SequenceOut => (uint)Volatile.Read(ref sequenceOut);

    /// <summary>
    /// Reserves the next outbound sequence number. The increment is atomic, so
    /// a broadcast written by the HTTP layer and a reply written by this
    /// session's own handler can never draw the same value.
    /// </summary>
    /// <returns>The sequence number to write into the packet.</returns>
    public uint NextSequenceOut() => (uint)Interlocked.Increment(ref sequenceOut);

    /// <summary>Account the client proved ownership of, when it has.</summary>
    public int? AccountIdentifier { get; set; }

    /// <summary>Character the client selected, when it has.</summary>
    public int? CharacterIdentifier { get; set; }

    /// <summary>Lobby the connection landed in, when the server hosts one.</summary>
    public int? LobbyIdentifier { get; set; }

    /// <summary>Room the client is in, when it joined one.</summary>
    public int? GameIdentifier { get; set; }

    /// <summary>
    /// Event team the connection currently belongs to, when it belongs to one.
    /// Only routing context: the team itself lives in the database, so this is
    /// how a roster push finds the sockets of a team without making the team
    /// itself resident in one process.
    /// </summary>
    public int? EventTeamIdentifier { get; set; }

    /// <summary>
    /// Event whose detail the connection last opened. It is routing context for
    /// the commands that follow a detail screen and carry no event of their own,
    /// such as entering the event: the client states which screen it is on by the
    /// order it asks in, so the server has to remember that order rather than
    /// requiring the identifier again. It is cleared whenever the connection's
    /// event routing changes, so it can never be carried across events.
    /// </summary>
    public int? SelectedEventIdentifier { get; set; }

    /// <summary>
    /// Set once a handler has decided the connection should end. A handler
    /// returns nothing, so this is how the disconnect command reaches the read
    /// loop that owns the socket: dispatch stops as soon as the handler returns.
    /// </summary>
    public bool DisconnectRequested { get; private set; }

    /// <summary>Asks the server to tear the connection down once the current handler returns.</summary>
    public void RequestDisconnect() => DisconnectRequested = true;

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
