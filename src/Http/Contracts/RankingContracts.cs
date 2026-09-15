using Microsoft.AspNetCore.Http;
using Mgo2Server.Shared.Domain.Rankings;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// The six parameters the Rankings screens post, always in the same order and
/// never omitted. Each is rendered with <c>%u</c>, and the client clamps
/// <c>rule</c> to four bits and <c>skey</c> to three before it sends them.
/// </summary>
public sealed class RankingForm
{
    /// <summary>Period selector; the low bit is the MONTH half of the screen's toggle.</summary>
    public int Term { get; init; }

    /// <summary>Game mode, used by the player score board only.</summary>
    public int Rule { get; init; }

    /// <summary>Board selector.</summary>
    public int SortKey { get; init; }

    /// <summary>Row offset of the window.</summary>
    public int From { get; init; }

    /// <summary>Rows the client asked for: 1 for its own standing, 100 for a page.</summary>
    public int Records { get; init; }

    /// <summary>Character identifier on the player endpoint, clan identifier on the clan endpoint.</summary>
    public int Subject { get; init; }

    /// <summary>Reads the posted form, clamping the fields the way the client clamps them.</summary>
    /// <param name="form">Form of the request, empty when none was posted.</param>
    /// <param name="subjectField">Name of the field carrying the subject identifier.</param>
    public static RankingForm Read(IFormCollection form, string subjectField) => new()
    {
        Term = ReadNumber(form, "term"),
        Rule = ReadNumber(form, "rule") & ((1 << RankingKeys.RuleBits) - 1),
        SortKey = ReadNumber(form, "skey") & ((1 << RankingKeys.SortKeyBits) - 1),
        From = Math.Max(ReadNumber(form, "from"), 0),
        Records = Math.Max(ReadNumber(form, "records"), 0),
        Subject = Math.Max(ReadNumber(form, subjectField), 0),
    };

    /// <summary>Reads one field as a number, defaulting an absent or unparsable value to zero.</summary>
    /// <param name="form">Form of the request.</param>
    /// <param name="field">Name of the field.</param>
    private static int ReadNumber(IFormCollection form, string field) =>
        int.TryParse(LoginForm.Read(form, field), out var value) ? value : 0;
}
