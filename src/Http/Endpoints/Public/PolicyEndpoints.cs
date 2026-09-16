using Mgo2Server.Http.Services;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The policy document endpoint.</summary>
internal static class PolicyEndpoints
{
    /// <summary>Maps the policy endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapPolicyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/jp/mgo2/policy/policy.txt", GetPolicyAsync)
            .WithTags("Game")
            .WithSummary("Policy document")
            .WithDescription("Returns the terms of service policy document");
    }

    private static async Task<IResult> GetPolicyAsync(
        PolicyService policyService,
        CancellationToken cancellationToken)
    {
        var body = await policyService.GetPolicyAsync(cancellationToken);
        return Results.Text(body, "text/html; charset=utf-8");
    }
}
