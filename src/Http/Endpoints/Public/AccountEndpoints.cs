using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Authentication;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The account endpoints.</summary>
internal static class AccountEndpoints
{
    /// <summary>Maps the account endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapAccountEndpoints(this RouteGroupBuilder group)
    {
        var accounts = group.MapGroup("/account")
            .WithTags("Account");

        accounts.MapPost("/register", RegisterAsync)
            .DisableAntiforgery()
            .WithSummary("Register account")
            .WithDescription("Creates a new player account.")
            .Produces<RegistrationResponseContract>(StatusCodes.Status201Created);
    }

    private static async Task<IResult> RegisterAsync(
        RegistrationService registrationService,
        RegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var issues = RequestBodyValidation.Validate(request);
        if (issues.Count > 0)
        {
            return RequestBodyValidation.Reject([.. issues]);
        }

        var created = await registrationService.RegisterAsync(request.DisplayName, request.Password, cancellationToken);
        return Results.Json(new RegistrationResponseContract(created.Identifier, created.DisplayName), statusCode: StatusCodes.Status201Created);
    }
}
