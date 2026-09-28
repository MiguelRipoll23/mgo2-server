using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The team half of the game service: the roster slot each player occupies.
/// <para>
/// Nothing here assigns a team. Only the client knows which one it is on, so the
/// slot is reported to us — in the peer register (<c>0x4344</c>) and in the
/// player's own team change (<c>0x4440</c>) — and the roster is where it is kept
/// so a chat line can be narrowed to a team later.
/// </para>
/// </summary>
public sealed partial class GameService
{
    /// <summary>Returns the team slot of every player in a room, by character id.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Dictionary<int, short>> GetPlayerTeamsAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.GameIdentifier == gameIdentifier)
            .ToDictionaryAsync(
                player => player.CharacterIdentifier,
                player => player.Team,
                cancellationToken);
    }

    /// <summary>
    /// Stores the team slot reported for one player of a room. A character that is
    /// not on the room's roster is not an error and not a row: the report is only
    /// ever about someone the room already holds.
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Character the slot was reported for.</param>
    /// <param name="team">Slot to store, encoded as the roster stores it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetPlayerTeamAsync(
        int gameIdentifier,
        int characterIdentifier,
        short team,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.GamePlayers
            .Where(player => player.GameIdentifier == gameIdentifier &&
                player.CharacterIdentifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(player => player.Team, team),
                cancellationToken);
    }
}
