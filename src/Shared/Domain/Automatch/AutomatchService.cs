using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Automatch;

/// <summary>Outcome of cancelling a search.</summary>
public enum AutomatchCancelOutcome
{
    /// <summary>The search was cancelled.</summary>
    Cancelled,

    /// <summary>The searcher was already matched, so the cancel was too late.</summary>
    TooLate,
}

/// <summary>
/// The automatch queue and the matchmaking policy that drains it. The queue is
/// per process and single-threaded by the tick, so a plain lock guards it. The
/// policy that drains the queue lives in the matchmaking half of this class, and
/// the settings it is built from arrive from the environment.
/// </summary>
/// <param name="options">Operator policy for automatching.</param>
public sealed partial class AutomatchService(IOptions<AutomatchOptions> options)
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, Searcher> searchers = [];
    private readonly List<PendingMatch> pending = [];
    private readonly List<PendingMatch> formed = [];
    private readonly List<PendingMatch> released = [];
    private readonly List<PendingMatch> failed = [];
    private readonly AutomatchOptions settings = options.Value;
    private readonly AutomatchPolicy policy = options.Value.ToPolicy();
    private readonly List<AutomatchWindow> windows = AutomatchWindowUtils.Parse(options.Value.Windows);
    private readonly TimeZoneInfo zone = AutomatchWindowUtils.ResolveZone(options.Value.TimeZone);
    private IAutomatchHooks? hooks;
    private int lobbyIdentifier;
    private int gameNumber;

    /// <summary>
    /// Whether automatching is open at a moment: enabled, and inside one of the
    /// configured windows when any are configured.
    /// </summary>
    /// <param name="when">Moment to test.</param>
    public bool IsOpen(DateTimeOffset when)
    {
        if (!settings.Enabled)
        {
            return false;
        }

        if (windows.Count == 0)
        {
            return true;
        }

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(when, zone).DateTime);
        return windows.Exists(window => window.Contains(local));
    }

    /// <summary>Binds the queue to the lobby and the game layer it forms matches in.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby this queue serves.</param>
    /// <param name="hooks">Callbacks into the game layer.</param>
    public void SetLobby(int lobbyIdentifier, IAutomatchHooks hooks)
    {
        this.lobbyIdentifier = lobbyIdentifier;
        this.hooks = hooks;
    }

    /// <summary>Adds a searcher, replacing any earlier entry for the same character.</summary>
    /// <param name="characterIdentifier">Character that is searching.</param>
    /// <param name="ruleFilter">Rule the searcher asked for.</param>
    /// <param name="experience">Experience used to derive the level.</param>
    /// <param name="active">Whether the connection is alive.</param>
    public void Enqueue(int characterIdentifier, int ruleFilter, int experience, bool active)
    {
        lock (gate)
        {
            searchers[characterIdentifier] = new Searcher(
                characterIdentifier,
                ruleFilter,
                experience,
                DateTimeOffset.UtcNow,
                active);
        }
    }

    /// <summary>
    /// Removes a searcher. The outcome is decided by the state at the moment
    /// this runs, which makes the race with the tick decidable.
    /// </summary>
    /// <param name="characterIdentifier">Character that cancels.</param>
    public AutomatchCancelOutcome Cancel(int characterIdentifier)
    {
        lock (gate)
        {
            if (!searchers.TryGetValue(characterIdentifier, out var searcher))
            {
                return AutomatchCancelOutcome.Cancelled;
            }

            if (searcher.State != AutomatchState.Searching)
            {
                return AutomatchCancelOutcome.TooLate;
            }

            searchers.Remove(characterIdentifier);
            return AutomatchCancelOutcome.Cancelled;
        }
    }

    /// <summary>Marks a searcher's connection dead, so the tick reaps it.</summary>
    /// <param name="characterIdentifier">Character whose connection dropped.</param>
    public void Disconnect(int characterIdentifier)
    {
        lock (gate)
        {
            if (searchers.TryGetValue(characterIdentifier, out var searcher))
            {
                searcher.Active = false;
            }
        }
    }

    /// <summary>Returns how many searchers are queued.</summary>
    public int Size
    {
        get
        {
            lock (gate)
            {
                return searchers.Count;
            }
        }
    }

    /// <summary>
    /// How many more players this searcher needs, counted among those they can
    /// actually reach. The searcher counts themselves.
    /// </summary>
    /// <param name="searcher">Searcher to measure.</param>
    public int PlayersNeeded(Searcher searcher)
    {
        lock (gate)
        {
            var reachable = searchers.Values.Count(other =>
                other.State == AutomatchState.Searching &&
                other.Active &&
                WindowsOverlap(searcher, other) &&
                ModesCompatible(searcher, other));

            var others = Math.Max(0, reachable - 1);
            return Math.Max(0, policy.RequiredPlayersAfter(searcher.Waited) - others);
        }
    }

    /// <summary>How many players a group needs the moment a searcher arrives.</summary>
    public int PlayersNeededOnArrival() => Math.Max(0, policy.RequiredPlayersAfter(TimeSpan.Zero));

    /// <summary>The level half-width a searcher is given the moment they arrive, before waiting widens it.</summary>
    public int BandOnArrival() => policy.BandAfter(TimeSpan.Zero);

    /// <summary>This searcher's half-width right now, which is also what is sent to them.</summary>
    /// <param name="searcher">Searcher to measure.</param>
    public int Band(Searcher searcher) => policy.BandAfter(searcher.Waited);

    /// <summary>Returns one queued searcher, or <c>null</c> when the character is not queued.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    public Searcher? Find(int characterIdentifier)
    {
        lock (gate)
        {
            return searchers.GetValueOrDefault(characterIdentifier);
        }
    }

    /// <summary>Returns a snapshot of every queued searcher.</summary>
    public List<Searcher> Snapshot()
    {
        lock (gate)
        {
            return [.. searchers.Values];
        }
    }

    /// <summary>
    /// Builds the search panel of every searcher still searching: the population
    /// they could actually be matched with, counted per level, plus their own band
    /// and shortfall.
    /// <para>
    /// The count excludes the recipient and counts only searchers they could be
    /// matched with right now, using the same rule the grouping uses — so the graph
    /// fills out on its own as the mode relaxation opens, and the column count can
    /// never drift from the matching rule.
    /// </para>
    /// </summary>
    /// <param name="columns">Number of columns, which is the client's own bar count.</param>
    public List<SearcherPanel> BuildSearchPanels(int columns)
    {
        lock (gate)
        {
            var panels = new List<SearcherPanel>(searchers.Count);
            foreach (var searcher in searchers.Values)
            {
                if (searcher.State != AutomatchState.Searching || !searcher.Active)
                {
                    continue;
                }

                var counts = new int[columns];
                foreach (var other in searchers.Values)
                {
                    if (ReferenceEquals(other, searcher) ||
                        other.State != AutomatchState.Searching ||
                        !other.Active ||
                        !ModesCompatible(searcher, other))
                    {
                        continue;
                    }

                    counts[Math.Clamp(other.Level, 0, columns - 1)]++;
                }

                panels.Add(new SearcherPanel(
                    searcher.CharacterIdentifier,
                    counts,
                    policy.BandAfter(searcher.Waited),
                    PlayersNeeded(searcher)));
            }

            return panels;
        }
    }

    /// <summary>Drains the matches formed by the last tick.</summary>
    public List<PendingMatch> TakeFormedMatches()
    {
        lock (gate)
        {
            var drained = formed.ToList();
            formed.Clear();
            return drained;
        }
    }

    /// <summary>Drains the matches whose game exists and whose members have to be told about it.</summary>
    public List<PendingMatch> TakeReleasedMatches()
    {
        lock (gate)
        {
            var drained = released.ToList();
            released.Clear();
            return drained;
        }
    }

    /// <summary>Drains the matches whose host never created a game.</summary>
    public List<PendingMatch> TakeFailedMatches()
    {
        lock (gate)
        {
            var drained = failed.ToList();
            failed.Clear();
            return drained;
        }
    }

    /// <summary>Told by create-game that a game now exists, so the tick need not wait.</summary>
    /// <param name="hostCharacterIdentifier">Character that hosted the game.</param>
    /// <param name="gameIdentifier">Identifier of the created game.</param>
    public void GameCreated(int hostCharacterIdentifier, int gameIdentifier)
    {
        lock (gate)
        {
            foreach (var match in pending)
            {
                if (match.HostCharacterIdentifier == hostCharacterIdentifier && match.GameIdentifier == 0)
                {
                    match.GameIdentifier = gameIdentifier;
                }
            }
        }
    }

    /// <summary>
    /// One pass of the matchmaker: reap first, then release and form.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            Reap();
        }

        await ReleaseCreatedMatchesAsync(cancellationToken);

        lock (gate)
        {
            FormMatches();
        }
    }
}
