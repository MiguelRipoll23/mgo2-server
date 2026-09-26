using System.Threading.Channels;
using Mgo2Server.Shared.InternalGrpc.Contracts;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// The queue of messages this lobby has yet to send to the coordinator.
/// <para>
/// It is a service of its own rather than a field of the coordination client so
/// that the parts which answer a question do not have to depend on the client
/// that is holding the connection. The client drains the queue; the presence
/// reporter and the request handlers fill it.
/// </para>
/// <para>
/// The queue is bounded and drops rather than blocks, because the first message
/// a new stream opens with is the truth about the lobby: an event that arrives
/// while the queue is full describes something the coordinator never saw, and
/// the registration carries that state anyway. Stalling a lobby that is still
/// serving its players to deliver it would be the worse trade.
/// </para>
/// </summary>
public sealed class LobbyEventQueueService
{
    /// <summary>Events a lobby may queue for the coordinator before it is considered stuck.</summary>
    private const int OutgoingCapacity = 1024;

    private readonly Channel<LobbyEvent> outgoing = Channel.CreateBounded<LobbyEvent>(
        new BoundedChannelOptions(OutgoingCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    /// <summary>Queues one message for the coordinator.</summary>
    /// <param name="message">Message to send when the stream is open.</param>
    /// <returns>Whether the queue took it.</returns>
    public bool TryEnqueue(LobbyEvent message) => outgoing.Writer.TryWrite(message);

    /// <summary>Reads the queued messages, in the order they were queued.</summary>
    /// <param name="cancellationToken">Token that stops the read.</param>
    public IAsyncEnumerable<LobbyEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        outgoing.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Throws away everything queued. A stream that is opening again describes
    /// the lobby as it is now, so the deltas that were waiting describe a
    /// population the registration is about to contradict.
    /// </summary>
    public void DiscardQueued()
    {
        while (outgoing.Reader.TryRead(out _))
        {
        }
    }
}
