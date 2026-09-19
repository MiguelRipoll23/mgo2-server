using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The login half of the character service: when a character was last seen, which is the
/// pair of stamps the client draws and the clock one title family measures an absence
/// against.
/// </summary>
public sealed partial class CharacterService
{
    /// <summary>
    /// Records that a character has logged in: the stored pair rotates, so the login that
    /// was recorded becomes the previous one, and the gap since that login is returned.
    /// <para>
    /// The gap is returned rather than left to the caller because it has to be measured
    /// before the stamps move. The title that is unlocked by an absence is only reachable
    /// while the previous login is still readable, so a caller that rotated first would
    /// reduce every gap to zero and make that title unreachable — a bug that reads as a
    /// threshold being too strict.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Character that logged in.</param>
    /// <param name="now">Unix timestamp of the login.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whole days since the login the character had recorded, zero when it had none.</returns>
    public async Task<int> RecordLoginAsync(
        int characterIdentifier,
        int now,
        CancellationToken cancellationToken = default)
    {
        var character = await FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null)
        {
            return 0;
        }

        var stored = new CharacterLoginTimes(
            SecondsOf(character.PreviousLoginTime),
            SecondsOf(character.LastSeenAt));
        var daysSinceLastLogin = stored.DaysSinceLastLogin(now);
        var rotated = stored.RotatedTo(now);

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(row => row.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.PreviousLoginTime, InstantOf(rotated.PreviousLoginTime))
                    .SetProperty(row => row.LastSeenAt, InstantOf(rotated.LastLoginTime)),
                cancellationToken);

        return daysSinceLastLogin;
    }

    /// <summary>Converts a stored instant to the unix seconds the client reads.</summary>
    private static int? SecondsOf(DateTimeOffset? instant) =>
        instant?.ToUnixTimeSeconds() is { } seconds ? (int)seconds : null;

    /// <summary>Converts unix seconds carried by the client back to a stored instant.</summary>
    private static DateTimeOffset? InstantOf(int? seconds) =>
        seconds is { } value ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
}
