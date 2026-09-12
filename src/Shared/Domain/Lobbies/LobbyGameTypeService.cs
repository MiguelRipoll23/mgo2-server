using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Resolves the game type of a lobby from the text carried by the environment.
/// The lookup table is the source of truth, so the accepted text is the name of
/// a row of <c>lobby_game_types</c>: <c>TRAINING</c> selects the training game
/// type, which carries identifier seven.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class LobbyGameTypeService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Resolves the game type named by a configuration value.</summary>
    /// <param name="subtype">Name of the game type, matched without regard to case or separators.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when no game type carries that name.</exception>
    public async Task<LobbyGameType> ResolveAsync(string subtype, CancellationToken cancellationToken = default)
    {
        var name = Normalize(subtype);

        if (name.Length == 0)
        {
            throw new InvalidOperationException("The lobby subtype must not be empty.");
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var gameTypes = await context.LobbyGameTypes
            .AsNoTracking()
            .OrderBy(gameType => gameType.Identifier)
            .ToListAsync(cancellationToken);

        if (gameTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "The lobby_game_types table is empty, so no lobby subtype can be resolved.");
        }

        // The identifier is accepted directly as well, which makes the value
        // unambiguous where two game types share a name.
        if (int.TryParse(name, out var identifier)
            && gameTypes.FirstOrDefault(gameType => gameType.Identifier == identifier) is { } byIdentifier)
        {
            return byIdentifier;
        }

        if (gameTypes.FirstOrDefault(gameType => Normalize(gameType.Name) == name) is { } byName)
        {
            return byName;
        }

        var known = string.Join(
            ", ",
            gameTypes.Select(gameType => gameType.Name).Distinct(StringComparer.OrdinalIgnoreCase));

        throw new InvalidOperationException(
            $"Unknown lobby subtype '{subtype}'. Configure the name of a game type of the " +
            $"lobby_game_types table; known names are: {known}.");
    }

    /// <summary>
    /// Reduces a subtype to letters and digits, so that the name of a game type
    /// can be written with any spacing or casing.
    /// </summary>
    /// <param name="text">Text to normalise.</param>
    private static string Normalize(string text) =>
        new([.. text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);
}
