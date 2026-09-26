using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards that the event services its consumers pull can all be resolved. A
/// service that is registered but depends on one that is not only fails at
/// activation, when a lobby is already booting, so the graph is built eagerly
/// here and each consumer asked for by name.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventServiceRegistrationTests
{
    [Fact]
    public void Every_event_service_resolves_from_the_container()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServerServices(
            new ConfigurationBuilder().AddInMemoryCollection().Build());

        using var provider = services.BuildServiceProvider();

        // The assignment ticker resolves the whole assignment chain, and that
        // chain used to stop on the unregistered push service — the exact moment
        // every lobby's boot would fail.
        Assert.NotNull(provider.GetRequiredService<EventAssignmentService>());
        Assert.NotNull(provider.GetRequiredService<EventAssignmentPushService>());
        Assert.NotNull(provider.GetRequiredService<EventOutcomePushService>());
        Assert.NotNull(provider.GetRequiredService<EventMatchService>());
        Assert.NotNull(provider.GetRequiredService<EventMatchmakingService>());
        Assert.NotNull(provider.GetRequiredService<EventTeamService>());
        Assert.NotNull(provider.GetRequiredService<EventTeamPushService>());
        Assert.NotNull(provider.GetRequiredService<EventInvitationService>());
        Assert.NotNull(provider.GetRequiredService<EventSessionDirectoryService>());
        Assert.NotNull(provider.GetRequiredService<EventHostLeaseService>());
        Assert.NotNull(provider.GetRequiredService<EventRewardService>());
        Assert.NotNull(provider.GetRequiredService<EventOutcomeService>());
        Assert.NotNull(provider.GetRequiredService<EventBracketPushService>());
        Assert.NotNull(provider.GetRequiredService<TournamentRegistrationService>());
        Assert.NotNull(provider.GetRequiredService<TournamentBracketService>());
        Assert.NotNull(provider.GetRequiredService<TournamentMatchService>());
        Assert.NotNull(provider.GetRequiredService<EventGameEntryService>());
        Assert.NotNull(provider.GetRequiredService<EventEntryService>());
    }

    [Fact]
    public void The_schedule_and_fake_player_services_resolve_from_the_container()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServerServices(
            new ConfigurationBuilder().AddInMemoryCollection().Build());

        using var provider = services.BuildServiceProvider();

        // The schedule service took a dependency on the name service when events
        // became addressable by name, and the fake players are resolved by the
        // coordination handler on a lobby that has an event to put them in.
        Assert.NotNull(provider.GetRequiredService<EventScheduleNameService>());
        Assert.NotNull(provider.GetRequiredService<EventScheduleService>());
        Assert.NotNull(provider.GetRequiredService<FakePlayerService>());
    }
}