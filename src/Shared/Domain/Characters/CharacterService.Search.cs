using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The search half of the character service: the name lookup the player search
/// screen runs. It is split out of the record half because it is a query on its
/// own and there is not room left in that file.
/// </summary>
public sealed partial class CharacterService
{
    /// <summary>Escape character that keeps a wildcard in the term literal.</summary>
    private const string SearchEscape = "\\";

    /// <summary>
    /// Characters whose name matches a search term, ordered by name.
    /// <para>
    /// The client sends the term with a match-criteria toggle and an ignore-case
    /// toggle and does no matching of its own, so both are semantics the server
    /// decides: a partial search is a substring match, a full one is equality, and
    /// the ignore-case toggle chooses between <c>ilike</c> and <c>like</c>. Wildcards
    /// in the term are escaped, or a query of a lone <c>%</c> would list every
    /// character rather than whoever is named with one.
    /// </para>
    /// <para>
    /// Only active characters are returned: a deleted character keeps its row but
    /// is not a player anyone can meet, so it must not appear on the screen.
    /// </para>
    /// </summary>
    /// <param name="name">Term to match against character names.</param>
    /// <param name="fullMatch">Whether the term has to be the whole name rather than a fragment.</param>
    /// <param name="ignoreCase">Whether the match ignores letter case. The client's second toggle is
    /// the case-SENSITIVE flag: it sends zero for the screen's "Case Insensitive" option, so the
    /// caller inverts it before arriving here.</param>
    /// <param name="limit">Maximum number of characters to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Character>> SearchAsync(
        string name,
        bool fullMatch,
        bool ignoreCase,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var escaped = name
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var pattern = fullMatch ? escaped : $"%{escaped}%";

        await using var context = await CreateContextAsync(cancellationToken);

        var matches = context.Characters
            .AsNoTracking()
            .Where(character => character.Active);

        matches = ignoreCase
            ? matches.Where(character => EF.Functions.ILike(character.Name, pattern, SearchEscape))
            : matches.Where(character => EF.Functions.Like(character.Name, pattern, SearchEscape));

        return await matches
            .OrderBy(character => character.Name)
            .ThenBy(character => character.Identifier)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
