namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Ignores every presence event. It is what a server that keeps no coordination
/// channel registers, so the lobby tracker always has a publisher and no
/// caller has to ask whether one is configured.
/// </summary>
public sealed class NullLobbyPresencePublisher : ILobbyPresencePublisher
{
    /// <inheritdoc />
    public void PlayerConnected(int lobbyIdentifier, int characterIdentifier)
    {
    }

    /// <inheritdoc />
    public void PlayerDisconnected(int lobbyIdentifier, int characterIdentifier)
    {
    }
}
