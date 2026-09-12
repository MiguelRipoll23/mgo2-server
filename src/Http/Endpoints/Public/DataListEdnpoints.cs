using Mgo2Server.Http.Contracts;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The data list endpoint.</summary>
internal static class DataListEdnpoints
{
    /// <summary>Maps the data list endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapDataListEdnpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/jp/mgo2/data/datalist.html", GetDataListAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Data list")
            .WithDescription("Game updates list");
    }

    private static async Task<IResult> GetDataListAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = DataListForm.From(await RequestBodyValidation.ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        return issues.Count > 0
            ? RequestBodyValidation.Reject([.. issues])
            : Results.Text(string.Empty, RequestBodyValidation.PlainTextContentType);
    }
}
