using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// The presence the lobbies report is what the global count and every observer
/// are fed, so a repeated event or a failing destination must not move it.
/// </summary>
[Trait("Category", "Http")]
public sealed class PlayerPresenceNotificationServiceTests
{
    [Fact]
    public async Task PresenceOfALobbyReachesTheObservers()
    {
        var presence = new LobbyPresenceService();
        var observer = new RecordingObserver();
        var service = CreateService(presence, observer);

        await service.RegisterLobbyAsync(1, "Free Battle", [], CancellationToken.None);
        await service.PlayerConnectedAsync(1, 42, CancellationToken.None);

        var notification = Assert.Single(observer.Presences);
        Assert.Equal(42, notification.CharacterIdentifier);
        Assert.True(notification.Connected);
        Assert.Equal(1, notification.TotalPlayers);
        Assert.Equal("Free Battle", notification.LobbyName);
        Assert.Equal(1, presence.TotalPlayers);
    }

    [Fact]
    public async Task ACharacterThatLeavesTwiceIsReportedOnce()
    {
        var presence = new LobbyPresenceService();
        var observer = new RecordingObserver();
        var service = CreateService(presence, observer);

        await service.PlayerConnectedAsync(1, 42, CancellationToken.None);
        await service.PlayerDisconnectedAsync(1, 42, CancellationToken.None);
        await service.PlayerDisconnectedAsync(1, 42, CancellationToken.None);

        Assert.Equal(2, observer.Presences.Count);
        Assert.False(observer.Presences[1].Connected);
        Assert.Equal(0, presence.TotalPlayers);
    }

    [Fact]
    public async Task ALobbyThatDisappearsReleasesItsPlayers()
    {
        var presence = new LobbyPresenceService();
        var observer = new RecordingObserver();
        var service = CreateService(presence, observer);

        await service.RegisterLobbyAsync(1, "Free Battle", [1, 2], CancellationToken.None);
        observer.Presences.Clear();

        await service.RemoveLobbyAsync(1, CancellationToken.None);

        // Nobody disconnected: the lobby is simply gone, so the total is only
        // corrected rather than announced player by player.
        Assert.Empty(observer.Presences);
        Assert.Equal(0, presence.TotalPlayers);
        Assert.Equal(new[] { 2, 0 }, observer.Totals);
    }

    [Fact]
    public async Task AnObserverThatFailsDoesNotStopTheCount()
    {
        var presence = new LobbyPresenceService();
        var failing = new RecordingObserver { Fails = true };
        var recording = new RecordingObserver();
        var service = new PlayerPresenceNotificationService(
            presence,
            CreateCharacterService(),
            [failing, recording],
            NullLogger<PlayerPresenceNotificationService>.Instance);

        await service.PlayerConnectedAsync(1, 42, CancellationToken.None);

        Assert.Equal(1, presence.TotalPlayers);
        Assert.Single(recording.Presences);
    }

    [Fact]
    public async Task ACharacterThatCannotBeReadIsStillCounted()
    {
        var presence = new LobbyPresenceService();
        var observer = new RecordingObserver();
        var service = CreateService(presence, observer);

        // The character service of this test points at a database that is not
        // there, which is the case the count has to survive.
        await service.PlayerConnectedAsync(1, 7, CancellationToken.None);

        var notification = Assert.Single(observer.Presences);
        Assert.Equal($"unknown character #{7}", notification.CharacterName);
    }

    private static PlayerPresenceNotificationService CreateService(
        LobbyPresenceService presence,
        RecordingObserver observer) =>
        new(
            presence,
            CreateCharacterService(),
            [observer],
            NullLogger<PlayerPresenceNotificationService>.Instance);

    private static CharacterService CreateCharacterService() =>
        new(new UnreachableDbContextFactory());

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }

    private sealed class RecordingObserver : IPlayerPresenceObserver
    {
        public List<PlayerPresenceNotification> Presences { get; } = [];

        public List<int> Totals { get; } = [];

        public bool Fails { get; init; }

        public Task PlayerPresenceChangedAsync(
            PlayerPresenceNotification notification,
            CancellationToken cancellationToken)
        {
            if (Fails)
            {
                throw new InvalidOperationException("the destination is down");
            }

            Presences.Add(notification);
            return Task.CompletedTask;
        }

        public Task PlayerTotalChangedAsync(int totalPlayers, CancellationToken cancellationToken)
        {
            Totals.Add(totalPlayers);
            return Task.CompletedTask;
        }
    }
}
