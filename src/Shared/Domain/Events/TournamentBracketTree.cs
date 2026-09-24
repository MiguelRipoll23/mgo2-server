namespace Mgo2Server.Shared.Domain.Events;

/// <summary>A fixture the bracket is ready to play.</summary>
/// <param name="Node">Node the fixture sits at, which is also its identifier.</param>
/// <param name="Round">Round number, counted from one.</param>
/// <param name="FirstTeamIdentifier">First team of the fixture.</param>
/// <param name="SecondTeamIdentifier">Second team of the fixture.</param>
public readonly record struct TournamentFixture(
    int Node,
    int Round,
    int FirstTeamIdentifier,
    int SecondTeamIdentifier);

/// <summary>
/// What advancing a bracket produced. It carries the bracket as it now stands
/// rather than only the outcome, because the caller pushes it: a bracket that
/// moved and was not shown to the teams that moved in it is a result they cannot
/// see.
/// </summary>
/// <param name="EventIdentifier">Event whose bracket advanced.</param>
/// <param name="MatchIdentifier">Match the fixture was played as.</param>
/// <param name="Round">Round the advance belongs to, counted from one.</param>
/// <param name="ChampionTeamIdentifier">Champion, or zero while the bracket is in play.</param>
/// <param name="RunnerUpTeamIdentifier">Team the champion beat in the final, or zero.</param>
/// <param name="Outcome">What happened to the bracket.</param>
public readonly record struct TournamentAdvance(
    int EventIdentifier,
    int MatchIdentifier,
    int Round,
    int ChampionTeamIdentifier,
    int RunnerUpTeamIdentifier,
    TournamentResultOutcome Outcome);

/// <summary>Outcome of reporting a fixture's winner.</summary>
public enum TournamentResultOutcome
{
    /// <summary>The fixture was resolved and the winner advanced.</summary>
    Recorded,

    /// <summary>The same winner was reported again, which changes nothing.</summary>
    Replayed,

    /// <summary>The result does not name either team of a ready fixture.</summary>
    NotAFixture,
}

/// <summary>
/// A single-elimination bracket held as a complete binary tree in one array.
/// The entrants are the frozen seed order and adjacent seeds meet, so the seed
/// order is the bracket order and the tree needs no shuffling: node
/// <c>n</c>'s children are <c>2n</c> and <c>2n+1</c>, and the root is the final.
/// <para>
/// A field that is not a power of two is padded with empty nodes, and an empty
/// node loses to its opponent immediately, which is a bye. That keeps the
/// arithmetic the same for every field size instead of special-casing the
/// first round.
/// </para>
/// <para>
/// The tree is deliberately not the storage. A caller rebuilds it from the
/// frozen seeds and replays the recorded results, so a restart resumes at the
/// same bracket rather than at one reconstructed from process memory.
/// </para>
/// </summary>
public sealed class TournamentBracketTree
{
    private readonly object gate = new();
    private readonly int[] winners;
    private readonly bool[] resolved;
    private readonly bool[] played;

    /// <summary>Builds a bracket from its frozen seed order.</summary>
    /// <param name="seedTeamIdentifiers">Team identifiers in seed order; zero is an empty place.</param>
    public TournamentBracketTree(IReadOnlyList<int> seedTeamIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(seedTeamIdentifiers);
        if (seedTeamIdentifiers.Count < 1
            || seedTeamIdentifiers.Count > EventConstants.BracketMaximumEntrants)
        {
            throw new ArgumentException(
                $"A bracket holds between 1 and {EventConstants.BracketMaximumEntrants} entrants.",
                nameof(seedTeamIdentifiers));
        }

        var seen = new HashSet<int>();
        foreach (var teamIdentifier in seedTeamIdentifiers)
        {
            // Zero is an empty place rather than an entrant, so it may repeat.
            if (teamIdentifier < 0
                || (teamIdentifier != 0 && !seen.Add(teamIdentifier)))
            {
                throw new ArgumentException(
                    "A bracket entrant is either an empty place or a distinct team.",
                    nameof(seedTeamIdentifiers));
            }
        }

        SeedCount = seedTeamIdentifiers.Count;
        Size = 1;
        while (Size < SeedCount)
        {
            Size *= 2;
        }

        winners = new int[Size * 2];
        resolved = new bool[Size * 2];
        played = new bool[Size * 2];

        for (var index = 0; index < Size; index++)
        {
            winners[Size + index] = index < SeedCount ? seedTeamIdentifiers[index] : 0;
            resolved[Size + index] = true;
        }

        AdvanceByes();
    }

    /// <summary>Entrants the bracket was built from.</summary>
    public int SeedCount { get; }

    /// <summary>Places in the bracket, the next power of two at or above the entrant count.</summary>
    public int Size { get; }

    /// <summary>Rounds a field of this size plays.</summary>
    public int RoundCount => Size <= 1 ? 0 : (int)Math.Log2(Size);

    /// <summary>Returns the fixtures that are ready and not yet played.</summary>
    public List<TournamentFixture> ReadyFixtures()
    {
        lock (gate)
        {
            var ready = new List<TournamentFixture>();
            for (var node = Size - 1; node > 0; node--)
            {
                if (!resolved[node] && resolved[node * 2] && resolved[node * 2 + 1])
                {
                    ready.Add(new TournamentFixture(
                        node,
                        RoundOf(node),
                        winners[node * 2],
                        winners[node * 2 + 1]));
                }
            }

            return ready;
        }
    }

    /// <summary>
    /// Returns the ready fixture two teams form, or a fixture whose node is zero
    /// when they are not playing one. The order they are named in does not
    /// matter, because a fixture is a pair rather than a sequence.
    /// <para>
    /// This is what decides whether a played match belongs to the bracket: two
    /// teams that met outside it are not a fixture, however far each of them has
    /// advanced, so a match cannot resolve a pairing the bracket never made.
    /// </para>
    /// </summary>
    /// <param name="firstTeamIdentifier">One of the two teams.</param>
    /// <param name="secondTeamIdentifier">The other team.</param>
    /// <returns>The fixture, with a zero node when the two teams form none.</returns>
    public TournamentFixture ReadyFixtureOf(int firstTeamIdentifier, int secondTeamIdentifier)
    {
        lock (gate)
        {
            foreach (var fixture in ReadyFixtures())
            {
                var forward = fixture.FirstTeamIdentifier == firstTeamIdentifier
                    && fixture.SecondTeamIdentifier == secondTeamIdentifier;
                var reversed = fixture.FirstTeamIdentifier == secondTeamIdentifier
                    && fixture.SecondTeamIdentifier == firstTeamIdentifier;
                if (forward || reversed)
                {
                    return fixture;
                }
            }

            return default;
        }
    }

    /// <summary>Records a winner. A repeated identical result changes nothing.</summary>
    /// <param name="node">Node the fixture sits at.</param>
    /// <param name="teamIdentifier">Winning team.</param>
    /// <returns>What happened.</returns>
    public TournamentResultOutcome RecordWinner(int node, int teamIdentifier)
    {
        lock (gate)
        {
            if (node < 1 || node >= Size || teamIdentifier <= 0)
            {
                return TournamentResultOutcome.NotAFixture;
            }

            if (played[node] && winners[node] == teamIdentifier)
            {
                return TournamentResultOutcome.Replayed;
            }

            if (resolved[node]
                || !resolved[node * 2]
                || !resolved[node * 2 + 1]
                || (teamIdentifier != winners[node * 2] && teamIdentifier != winners[node * 2 + 1]))
            {
                return TournamentResultOutcome.NotAFixture;
            }

            winners[node] = teamIdentifier;
            resolved[node] = true;
            played[node] = true;
            AdvanceByes();
            return TournamentResultOutcome.Recorded;
        }
    }

    /// <summary>Returns the champion, or zero while the bracket is undecided.</summary>
    public int Champion()
    {
        lock (gate)
        {
            // A field of one crowns its entrant rather than reporting nobody: the
            // root of a one-team bracket is that team.
            return resolved[1] ? winners[1] : 0;
        }
    }

    /// <summary>
    /// Returns the team the champion beat in the final, or zero while the bracket
    /// is undecided. The final is the only fixture a champion played last, so its
    /// runner-up is the finalist that is not the champion.
    /// </summary>
    public int RunnerUp()
    {
        lock (gate)
        {
            var champion = resolved[1] ? winners[1] : 0;
            if (champion == 0)
            {
                return 0;
            }

            var firstFinalist = WinnerAt(2);
            var secondFinalist = WinnerAt(3);
            return firstFinalist == champion ? secondFinalist : firstFinalist;
        }
    }

    /// <summary>
    /// Returns the lowest round that still has a fixture to play, or the final
    /// round once the bracket is played out. It is the round the bracket is
    /// waiting on, which is what a client showing "round n" wants rather than the
    /// round that has just finished.
    /// </summary>
    public int NextRound()
    {
        lock (gate)
        {
            var ready = ReadyFixtures();
            return ready.Count == 0 ? RoundCount : ready.Min(fixture => fixture.Round);
        }
    }

    /// <summary>Returns the winning team at one node, or zero while it is undecided.</summary>
    /// <param name="node">Node to read.</param>
    public int WinnerAt(int node)
    {
        lock (gate)
        {
            return node >= 1 && node < Size && resolved[node] ? winners[node] : 0;
        }
    }

    /// <summary>Rounds a node plays in, counted from one.</summary>
    /// <param name="node">Node to measure.</param>
    public int RoundOf(int node) => Size <= 1 || node < 1 ? 1 : RoundCount - (int)Math.Log2(node);

    private void AdvanceByes()
    {
        // An empty place loses to its opponent, so the opponent advances without
        // a game. Walking upward lets one bye create the next one.
        for (var node = Size - 1; node > 0; node--)
        {
            if (!resolved[node]
                && resolved[node * 2]
                && resolved[node * 2 + 1]
                && (winners[node * 2] == 0 || winners[node * 2 + 1] == 0))
            {
                winners[node] = Math.Max(winners[node * 2], winners[node * 2 + 1]);
                resolved[node] = true;
            }
        }
    }
}
