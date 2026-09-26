using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the coordination questions the API is waiting on. An in-memory team
/// has no row and no identifier any other part of the deployment could look up,
/// so the correlation between a question and the answer that travels back up
/// the stream is the only way a caller learns what a lobby is holding — and a
/// question whose answer is never matched is a caller that waits for nothing.
/// </summary>
[Trait("Category", "Http")]
public sealed class FakeTeamQueryTests
{
    private static LobbyTeamQueryService CreateService() =>
        new(NullLogger<LobbyTeamQueryService>.Instance);

    [Fact]
    public async Task The_answer_wakes_the_caller_that_asked()
    {
        var service = CreateService();
        var requestIdentifier = service.NextRequestIdentifier();

        Assert.True(service.Expect(requestIdentifier, out var answer));

        service.Complete(new FakeTeamListing
        {
            RequestIdentifier = requestIdentifier,
            Teams = { new FakeTeamSummary { TeamName = "TESTERS", MemberCount = 3 } },
        });

        var listing = await answer;
        Assert.Equal("TESTERS", Assert.Single(listing.Teams).TeamName);
    }

    [Fact]
    public void Every_question_gets_its_own_correlation()
    {
        var service = CreateService();

        var first = service.NextRequestIdentifier();
        var second = service.NextRequestIdentifier();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_second_caller_may_not_take_over_a_correlation_that_is_waiting()
    {
        var service = CreateService();
        var requestIdentifier = service.NextRequestIdentifier();

        Assert.True(service.Expect(requestIdentifier, out _));
        Assert.False(service.Expect(requestIdentifier, out _));
    }

    [Fact]
    public void An_answer_nobody_is_waiting_for_is_reported_rather_than_kept()
    {
        var service = CreateService();

        Assert.False(service.Complete(new FakeTeamListing { RequestIdentifier = 404 }));
    }

    [Fact]
    public async Task Abandoning_a_question_stops_the_wait_and_marks_a_late_answer_as_late()
    {
        var service = CreateService();
        var requestIdentifier = service.NextRequestIdentifier();
        Assert.True(service.Expect(requestIdentifier, out var answer));

        service.Abandon(requestIdentifier);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => answer);
        Assert.False(service.Complete(new FakeTeamListing { RequestIdentifier = requestIdentifier }));
    }
}
