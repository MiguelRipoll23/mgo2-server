using System.Threading.Channels;
using Mgo2Server.Shared.InternalGrpc.Contracts;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// One connected gameplay lobby: the stream the coordinator writes to, and the
/// identity the lobby registered with.
/// </summary>
/// <remarks>
/// Only the pump that owns the stream writes to it, so the announcements wait
/// in a queue instead of racing for the writer. The queue is bounded, so a
/// lobby that stops reading cannot grow the memory of the API without bound;
/// the old announcements of such a lobby are the ones dropped.
/// </remarks>
/// <param name="lobbyIdentifier">Identifier the lobby registered with.</param>
/// <param name="lobbyName">Name the lobby registered with.</param>
public sealed class LobbyConnection(int lobbyIdentifier, string lobbyName)
{
    /// <summary>Announcements a lobby may have queued before it is considered stuck.</summary>
    public const int OutgoingCapacity = 64;

    private readonly Channel<HttpEvent> outgoing = Channel.CreateBounded<HttpEvent>(
        new BoundedChannelOptions(OutgoingCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    /// <summary>Identifier the lobby registered with.</summary>
    public int LobbyIdentifier { get; } = lobbyIdentifier;

    /// <summary>Name the lobby registered with.</summary>
    public string LobbyName { get; } = lobbyName;

    /// <summary>Announcements waiting to be written to the stream.</summary>
    public ChannelReader<HttpEvent> Outgoing => outgoing.Reader;

    /// <summary>Queues an announcement for this lobby.</summary>
    /// <param name="message">Message to write to the stream.</param>
    public bool TryEnqueue(HttpEvent message) => outgoing.Writer.TryWrite(message);

    /// <summary>
    /// Completes the queue, so the pump that writes the stream stops once it
    /// has written what was already queued.
    /// </summary>
    public void Complete() => outgoing.Writer.TryComplete();
}
