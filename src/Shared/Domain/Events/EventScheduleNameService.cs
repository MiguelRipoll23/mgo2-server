using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the name a schedule is addressed by.
/// <para>
/// A schedule is announced to players and typed by moderators, so the name is
/// the handle the surfaces use and the identifier is only what the protocol
/// carries. It is a service of its own because naming is a concern with its own
/// rules — folding, composing, refusing a duplicate — rather than one more
/// column read.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventScheduleNameService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory) : DomainService(contextFactory)
{
    /// <summary>
    /// Longest name a schedule is stored under, matching the column. A name is
    /// announced to players in the lobby, so it is short by design rather than
    /// by whatever the column would have accepted.
    /// </summary>
    public const int MaximumLength = 64;

    /// <summary>
    /// Finds an event's schedule by the name an operator gave it.
    /// <para>
    /// The name is matched without regard to case or surrounding spaces,
    /// because it is typed by a person and a handle that only works when it is
    /// spelled exactly the way the row happens to hold it is not a handle.
    /// </para>
    /// </summary>
    /// <param name="name">Name the event was given.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventSchedule?> FindAsync(
        string? name,
        CancellationToken cancellationToken = default)
    {
        var wanted = Normalise(name);
        if (wanted.Length == 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventSchedules
            .Where(schedule => schedule.Name.ToLower() == wanted)
            .OrderBy(schedule => schedule.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Composes a name for a schedule that was not given one, so the machine
    /// surface with no name to offer still writes a row somebody can read and
    /// address.
    /// </summary>
    /// <param name="mode">Lobby mode the event is played in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> ComposeAsync(int mode, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        // The suffix walks past the names already taken rather than counting
        // rows: a mode with a withdrawn event leaves a gap, and "Survival 2"
        // being the second name of the mode is a detail nobody reading the list
        // has to notice.
        for (var ordinal = 1; ; ordinal++)
        {
            var candidate = $"{ModeName(mode)} {ordinal}";
            if (!await context.EventSchedules.AnyAsync(
                    schedule => schedule.Name.ToLower() == candidate.ToLower(),
                    cancellationToken))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Trims a name and folds its case, which is the form a name is stored and
    /// compared in. Collapsing the inner runs of spaces is what stops
    /// "Night  Three" and "Night Three" from being two events.
    /// </summary>
    /// <param name="name">Name as it was typed.</param>
    public static string Normalise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > MaximumLength
            ? collapsed[..MaximumLength]
            : collapsed;
    }

    /// <summary>Name a lobby mode is written with in a schedule name.</summary>
    /// <param name="mode">Lobby mode to name.</param>
    public static string ModeName(int mode) => mode switch
    {
        EventConstants.SurvivalSelector => "Survival",
        EventConstants.TournamentSelector => "Tournament",
        EventConstants.TournamentRegistrationSelector => "Registration",
        _ => $"Mode {mode}",
    };
}
