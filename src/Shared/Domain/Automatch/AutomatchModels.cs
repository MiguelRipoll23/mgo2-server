using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.Shared.Domain.Automatch;

/// <summary>Constants and pure policy of the automatch queue.</summary>
public static class AutomatchConstants
{
    /// <summary>"Do not specify rules" — the sentinel outside the 0-10 label range.</summary>
    public const int AnyRule = 11;

    /// <summary>
    /// Rule filters accepted from the start-search command: every rule the stock
    /// client can offer, plus the ones a patched client may send.
    /// </summary>
    public static readonly HashSet<int> RuleFilters = [0, 1, 2, 3, 4, 5, 6, 7, 8, AnyRule];

    /// <summary>Rules a wildcard search may be given — only ones stock clients can play.</summary>
    public static readonly int[] WildcardRules = [0, 1, 2, 3, 4, 5, 7];

    /// <summary>The maps automatching may pick — the disc's five shipping stages.</summary>
    public static readonly int[] MapPool = [2, 3, 4, 7, 12];

    /// <summary>How long a searcher may sit in the queue before being dropped.</summary>
    public static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(25);

    /// <summary>How long an elected host has to create the game before the group is told it failed.</summary>
    public static readonly TimeSpan HostCreateTimeout = TimeSpan.FromSeconds(45);
}

/// <summary>Lifecycle of a searcher.</summary>
public enum AutomatchState
{
    /// <summary>Waiting in the queue.</summary>
    Searching = 0,

    /// <summary>Elected into a group whose host must create the game.</summary>
    AwaitingHostCreate = 1,

    /// <summary>A game was produced for the group.</summary>
    Matched = 2,
}

/// <summary>One client waiting for a match.</summary>
public sealed class Searcher(
    int characterIdentifier,
    int ruleFilter,
    int experience,
    DateTimeOffset joinedAt,
    bool active)
{
    /// <summary>Character that is searching.</summary>
    public int CharacterIdentifier { get; } = characterIdentifier;

    /// <summary>Rule the searcher asked for, or <see cref="AutomatchConstants.AnyRule"/>.</summary>
    public int RuleFilter { get; } = ruleFilter;

    /// <summary>Experience used to derive the level.</summary>
    public int Experience { get; } = experience;

    /// <summary>Moment the searcher joined the queue.</summary>
    public DateTimeOffset JoinedAt { get; } = joinedAt;

    /// <summary>Whether the connection is still alive.</summary>
    public bool Active { get; set; } = active;

    /// <summary>Current lifecycle state.</summary>
    public AutomatchState State { get; set; } = AutomatchState.Searching;

    /// <summary>Group this searcher was placed in, when it has one.</summary>
    public PendingMatch? Match { get; set; }

    /// <summary>The level the client draws this searcher's window around.</summary>
    public int Level => CharacterService.CalculateLevel(Experience);

    /// <summary>How long the searcher has waited.</summary>
    public TimeSpan Waited => DateTimeOffset.UtcNow - JoinedAt;
}

/// <summary>A group told to form a game, and the host elected to create it.</summary>
public sealed class PendingMatch(
    int hostCharacterIdentifier,
    IReadOnlyList<int> members,
    DateTimeOffset electedAt,
    int gameIdentifierBefore,
    IReadOnlyList<int> rules,
    int map)
{
    /// <summary>Identifier of the game once the host created it.</summary>
    public int GameIdentifier { get; set; }

    /// <summary>Whether the elected host vanished before creating the game.</summary>
    public bool HostLost { get; set; }

    /// <summary>Character elected to create the game.</summary>
    public int HostCharacterIdentifier { get; } = hostCharacterIdentifier;

    /// <summary>Characters in the group.</summary>
    public IReadOnlyList<int> Members { get; } = members;

    /// <summary>Moment the host was elected.</summary>
    public DateTimeOffset ElectedAt { get; } = electedAt;

    /// <summary>Any game the elected host already hosted when chosen.</summary>
    public int GameIdentifierBefore { get; } = gameIdentifierBefore;

    /// <summary>Rotation rules for the match, entry zero first.</summary>
    public IReadOnlyList<int> Rules { get; } = rules;

    /// <summary>Map the match runs.</summary>
    public int Map { get; } = map;
}

/// <summary>Matchmaking policy: bands widen and requirements decay with wait time.</summary>
public sealed class AutomatchPolicy(
    int minimumPlayers = 2,
    int minimumPlayersAtStart = 12,
    int minimumPlayersStepMilliseconds = 30_000,
    int bandAtStart = 1,
    int bandStepMilliseconds = 30_000,
    int bandMaximum = 22,
    int modeRelaxMilliseconds = 90_000)
{
    /// <summary>Players a group needs once fully relaxed.</summary>
    public int MinimumPlayers { get; } = minimumPlayers;

    /// <summary>Players a group needs when it forms immediately.</summary>
    public int MinimumPlayersAtStart { get; } = minimumPlayersAtStart;

    /// <summary>Milliseconds between one player off the requirement.</summary>
    public int MinimumPlayersStepMilliseconds { get; } = minimumPlayersStepMilliseconds;

    /// <summary>Level half-width at the start.</summary>
    public int BandAtStart { get; } = bandAtStart;

    /// <summary>Milliseconds between one level of widening.</summary>
    public int BandStepMilliseconds { get; } = bandStepMilliseconds;

    /// <summary>Maximum level half-width.</summary>
    public int BandMaximum { get; } = bandMaximum;

    /// <summary>Wait after which a searcher accepts any mode.</summary>
    public int ModeRelaxMilliseconds { get; } = modeRelaxMilliseconds;

    /// <summary>The level half-width for a searcher who has waited this long.</summary>
    /// <param name="waited">Time the searcher has waited.</param>
    public int BandAfter(TimeSpan waited)
    {
        var steps = (int)(waited.TotalMilliseconds / BandStepMilliseconds);
        return Math.Min(BandAtStart + steps, BandMaximum);
    }

    /// <summary>How many players a group needs after this wait.</summary>
    /// <param name="waited">Time the searcher has waited.</param>
    public int RequiredPlayersAfter(TimeSpan waited)
    {
        if (MinimumPlayersStepMilliseconds == 0)
        {
            return MinimumPlayers;
        }

        var steps = (int)(waited.TotalMilliseconds / MinimumPlayersStepMilliseconds);
        return Math.Max(MinimumPlayers, MinimumPlayersAtStart - steps);
    }

    /// <summary>Whether a searcher who has waited this long accepts any mode.</summary>
    /// <param name="waited">Time the searcher has waited.</param>
    public bool AcceptsAnyModeAfter(TimeSpan waited) =>
        waited.TotalMilliseconds >= ModeRelaxMilliseconds;
}

/// <summary>Callbacks the automatch queue needs into the game layer.</summary>
public interface IAutomatchHooks
{
    /// <summary>Returns the identifier of a game this character hosts in this lobby, or zero.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Character to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task<int> FindHostedGameIdentifierAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken);

    /// <summary>Renames a formed game and clears any password on it.</summary>
    /// <param name="gameIdentifier">Identifier of the game.</param>
    /// <param name="name">Name to give the game.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task RenameAndUnlockAsync(int gameIdentifier, string name, CancellationToken cancellationToken);
}
