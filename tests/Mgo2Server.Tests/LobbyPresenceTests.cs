using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// The coordinator counts what the lobbies report, and the count is what the
/// global total and the Discord channel are built from.
/// </summary>
[Trait("Category", "Http")]
public sealed class LobbyPresenceServiceTests
{
    [Fact]
    public void RegistrationReplacesWhatWasKnownAboutTheLobby()
    {
        var presence = new LobbyPresenceService();

        presence.RegisterLobby(7, "Free Battle", [1, 2, 3]);

        Assert.Equal(3, presence.TotalPlayers);
        Assert.Equal(3, presence.GetPlayerCount(7));
        Assert.Equal("Free Battle", presence.GetLobbyName(7));

        // A lobby that reconnects reports the truth, so the deltas the
        // coordinator missed are not counted twice.
        Assert.Equal(1, presence.RegisterLobby(7, "Free Battle", [5]));
        Assert.Equal(1, presence.TotalPlayers);
    }

    [Fact]
    public void TotalAddsEveryLobby()
    {
        var presence = new LobbyPresenceService();

        presence.RegisterLobby(1, "Free Battle", [1, 2]);
        presence.RegisterLobby(2, "Survival", [3]);

        Assert.Equal(3, presence.TotalPlayers);
        Assert.Equal(2, presence.ListLobbies().Count);
    }

    [Fact]
    public void APlayerIsCountedOnce()
    {
        var presence = new LobbyPresenceService();
        presence.RegisterLobby(1, "Free Battle", []);

        Assert.True(presence.AddPlayer(1, 42, out var first));
        Assert.Equal(1, first);

        // The client may leave the lobby and be torn down right after it, which
        // reports the same departure twice.
        Assert.False(presence.AddPlayer(1, 42, out var second));
        Assert.Equal(1, second);
    }

    [Fact]
    public void DeparturesOfUnknownPlayersAreIgnored()
    {
        var presence = new LobbyPresenceService();
        presence.RegisterLobby(1, "Free Battle", [42]);

        Assert.True(presence.RemovePlayer(1, 42, out var total));
        Assert.Equal(0, total);
        Assert.False(presence.RemovePlayer(1, 42, out _));
        Assert.False(presence.RemovePlayer(99, 42, out _));
    }

    [Fact]
    public void LobbyThatGoesAwayReleasesItsPlayers()
    {
        var presence = new LobbyPresenceService();
        presence.RegisterLobby(1, "Free Battle", [1, 2]);

        Assert.True(presence.RemoveLobby(1, out var total));
        Assert.Equal(0, total);
        Assert.Equal(0, presence.TotalPlayers);
        Assert.False(presence.RemoveLobby(1, out _));
    }
}

/// <summary>The registry is what makes the API the central point of cross-lobby communication.</summary>
[Trait("Category", "Http")]
public sealed class LobbyConnectionRegistryServiceTests
{
    [Fact]
    public void BroadcastReachesEveryConnectedLobby()
    {
        var registry = CreateRegistry();
        var freeBattle = registry.Open(1, "Free Battle");
        registry.Open(2, "Survival");

        Assert.Equal(2, registry.Broadcast(new HttpEvent { FlashNews = new FlashNewsBroadcast { Message = "hi" } }));

        Assert.True(freeBattle.Outgoing.TryRead(out var message));
        Assert.Equal("hi", message.FlashNews.Message);
    }

    [Fact]
    public void AReplacedStreamIsNotTheStreamOfItsLobby()
    {
        var registry = CreateRegistry();
        var first = registry.Open(1, "Free Battle");
        var second = registry.Open(1, "Free Battle");

        Assert.False(registry.Close(first));
        Assert.Equal(1, registry.Count);

        Assert.Equal(1, registry.Broadcast(new HttpEvent { FlashNews = new FlashNewsBroadcast() }));
        Assert.True(second.Outgoing.TryRead(out _));

        Assert.True(registry.Close(second));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void BroadcastWithoutLobbiesReachesNobody()
    {
        Assert.Equal(0, CreateRegistry().Broadcast(new HttpEvent()));
    }

    private static LobbyConnectionRegistryService CreateRegistry() =>
        new(NullLogger<LobbyConnectionRegistryService>.Instance);
}
