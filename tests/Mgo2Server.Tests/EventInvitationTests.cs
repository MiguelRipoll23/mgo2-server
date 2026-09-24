using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the invitation lifetime and reservation rules. They are the part with
/// no wire assertion to catch a mistake, so the state machine is exercised
/// directly with explicit clocks.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventInvitationTests
{
    private const int Leader = 100;
    private const int Team = 7;

    private static EventInvitationService CreateService() => new();

    private static EventInvitation Create(
        EventInvitationService service,
        int target,
        long now,
        int team = Team) =>
        service.Create(Leader, "LEADER", target, $"TARGET{target}", team, 12, EventInvitationService.SurvivalMode, now)!;

    [Fact]
    public void A_duplicate_invitation_to_the_same_target_is_refused()
    {
        var service = CreateService();

        Assert.NotNull(Create(service, 200, now: 1_000));
        Assert.Null(service.Create(Leader, "LEADER", 200, "TARGET200", Team, 12, EventInvitationService.SurvivalMode, 1_000));
    }

    [Fact]
    public void Only_the_named_target_can_take_a_pending_invitation()
    {
        var service = CreateService();
        var invitation = Create(service, 200, now: 1_000);

        Assert.Null(service.TakePending(invitation.Identifier, targetCharacterIdentifier: 201, nowSeconds: 1_000));
        Assert.NotNull(service.TakePending(invitation.Identifier, 200, 1_000));
        // It is removed by the take, so a second answer finds nothing.
        Assert.Null(service.TakePending(invitation.Identifier, 200, 1_000));
    }

    [Fact]
    public void A_pending_invitation_expires_after_its_lifetime()
    {
        var service = CreateService();
        var invitation = Create(service, 200, now: 1_000);

        Assert.Null(service.TakePending(
            invitation.Identifier,
            200,
            1_000 + EventInvitationService.PendingLifetimeSeconds + 1));
    }

    [Fact]
    public void An_accepted_reservation_expires_after_its_lifetime()
    {
        var service = CreateService();
        var invitation = Create(service, 200, now: 1_000);
        service.Accept(invitation, nowSeconds: 1_000);

        Assert.NotNull(service.FindAccepted(200, 1_000));
        Assert.Null(service.FindAccepted(200, 1_000 + EventInvitationService.AcceptedLifetimeSeconds + 1));
    }

    [Fact]
    public void Answering_one_invitation_cancels_the_targets_others()
    {
        var service = CreateService();
        var answered = Create(service, 200, now: 1_000);
        var other = service.Create(
            Leader + 1,
            "OTHER",
            200,
            "TARGET200",
            teamIdentifier: 8,
            lobbyIdentifier: 12,
            EventInvitationService.SurvivalMode,
            nowSeconds: 1_000)!;

        var cancelled = service.CancelOthersForTarget(200, answered.Identifier, nowSeconds: 1_000);

        Assert.Single(cancelled);
        Assert.Equal(other.Identifier, cancelled[0].Identifier);
        Assert.NotNull(service.TakePending(answered.Identifier, 200, 1_000));
    }

    [Fact]
    public void Reserved_slots_count_pending_and_accepted_invitations()
    {
        var service = CreateService();
        var first = Create(service, 200, now: 1_000);
        Create(service, 201, now: 1_000);
        service.Accept(first, nowSeconds: 1_000);

        Assert.Equal(2, service.CountReservedForTeam(Team, nowSeconds: 1_000));
        Assert.Equal(0, service.CountReservedForTeam(teamIdentifier: 99, nowSeconds: 1_000));
    }

    [Fact]
    public void Ending_a_team_releases_its_invitations()
    {
        var service = CreateService();
        var invitation = Create(service, 200, now: 1_000);
        service.Accept(invitation, nowSeconds: 1_000);

        service.RemoveForTeam(Team);

        Assert.Equal(0, service.CountReservedForTeam(Team, nowSeconds: 1_000));
        Assert.Null(service.FindAccepted(200, 1_000));
    }
}
