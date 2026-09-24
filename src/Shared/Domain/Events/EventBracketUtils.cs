using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the Tournament bracket records: the round the bracket advanced to,
/// the whole bracket state, and the final standings with the reward a team was
/// paid.
/// <para>
/// The identity these records carry is the value <c>0x4A00</c> stamped for the
/// recipient's team, not the identifier of the event row. The client validates
/// the leading pair against the object it holds and the event field against the
/// record that object belongs to, and it fills both slots from the same stored
/// value — so the same correlation identifier is written into both rather than
/// two different ones the client would have to reconcile.
/// </para>
/// <para>
/// Two lengths on the wire come from the client's own state rather than from a
/// count: the per-entrant status column is one byte per entrant, and the bracket
/// state's rows are one per round. Both are passed in, so a caller cannot write
/// the record without deciding them.
/// </para>
/// </summary>
public static class EventBracketUtils
{
    /// <summary>
    /// Writes one round's result: the bitmap of the entrants that won it and the
    /// status column of the whole field. The client advances its bracket by one
    /// round from this record, which is why the round number is on the wire and
    /// why it is validated here rather than clamped.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="correlationIdentifier">Team correlation the client holds; never zero.</param>
    /// <param name="sequence">Sequence the client holds for that team.</param>
    /// <param name="phase">Event phase byte, shared with the event record.</param>
    /// <param name="round">Round the bracket advanced to, counted from one.</param>
    /// <param name="bitmapWords">Four words of that round's 128-bit entrant bitmap.</param>
    /// <param name="entrantStatuses">One status byte per entrant of the field.</param>
    public static void WriteRoundResult(
        PacketWriter writer,
        int correlationIdentifier,
        int sequence,
        int phase,
        int round,
        int[] bitmapWords,
        byte[] entrantStatuses)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(bitmapWords, EventConstants.BracketBitmapWordCount, "round bitmap");
        ArgumentNullException.ThrowIfNull(entrantStatuses);
        if (round < 1 || round > EventConstants.BracketMaximumRound)
        {
            // A round the client cannot address would write its bitmap over the
            // snapshot the renderer diffs against.
            throw new ArgumentOutOfRangeException(
                nameof(round),
                $"A bracket round is numbered from one to {EventConstants.BracketMaximumRound}.");
        }

        var start = writer.Size;
        WriteIdentity(writer, correlationIdentifier, sequence);
        writer.WriteInt32(correlationIdentifier);
        writer.WriteUInt8(0);
        writer.WriteUInt8(phase);

        // The half whose meaning is not established, and which the parser reads
        // before the round number. Zero is the only value this server can state:
        // an invented one would be indistinguishable from a correct field.
        writer.WriteUInt16(0);
        writer.WriteUInt16(round);
        WriteBitmap(writer, bitmapWords);
        writer.WriteBytes(entrantStatuses);

        AssertSize(
            writer,
            start,
            EventConstants.RoundResultBaseWireSize + entrantStatuses.Length,
            "round result");
    }

    /// <summary>
    /// Writes the whole bracket state: one bitmap row per round, then the status
    /// column of the field. It carries no identity header, so the echo identifier
    /// is what tells the client the state belongs to the object it is showing.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="correlationIdentifier">Team correlation the client holds; never zero.</param>
    /// <param name="bitmapRows">One four-word bitmap per round, from round one.</param>
    /// <param name="entrantStatuses">One status byte per entrant of the field.</param>
    public static void WriteBracketState(
        PacketWriter writer,
        int correlationIdentifier,
        IReadOnlyList<int[]> bitmapRows,
        byte[] entrantStatuses)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(bitmapRows);
        ArgumentNullException.ThrowIfNull(entrantStatuses);

        var start = writer.Size;
        writer.WriteInt32(correlationIdentifier);
        writer.WriteUInt16(bitmapRows.Count);
        foreach (var row in bitmapRows)
        {
            RequireLength(row, EventConstants.BracketBitmapWordCount, "bracket row");
            WriteBitmap(writer, row);
        }

        writer.WriteBytes(entrantStatuses);

        AssertSize(
            writer,
            start,
            EventConstants.BracketStateBaseWireSize
                + (bitmapRows.Count * EventConstants.BracketBitmapWireSize)
                + entrantStatuses.Length,
            "bracket state");
    }

    /// <summary>
    /// Writes the final standings and a reward. The client prints the first
    /// standing as the winning team's name and the reward as the amount "you"
    /// received, so the recipient's own payment is what belongs in the reward
    /// field rather than the champion's.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="correlationIdentifier">Team correlation the client holds; never zero.</param>
    /// <param name="sequence">Sequence the client holds for that team.</param>
    /// <param name="standings">Eight standing words; the first is the champion.</param>
    /// <param name="reward">Reward the recipient was paid.</param>
    public static void WriteFinalStandings(
        PacketWriter writer,
        int correlationIdentifier,
        int sequence,
        int[] standings,
        int reward)
    {
        ArgumentNullException.ThrowIfNull(writer);
        RequireLength(standings, EventConstants.BracketStandingWordCount, "final standings");

        var start = writer.Size;
        WriteIdentity(writer, correlationIdentifier, sequence);
        writer.WriteInt32(correlationIdentifier);
        foreach (var standing in standings)
        {
            writer.WriteInt32(standing);
        }

        writer.WriteInt32(reward);

        AssertSize(writer, start, EventConstants.BracketFinalStandingWireSize, "final standings");
    }

    /// <summary>
    /// Builds the bitmap of one round: bit <c>n</c> is set for entrant <c>n</c>
    /// when its team won a fixture of that round. A fixture that is ready but
    /// unplayed has no bit, because the bitmap records what advanced rather than
    /// who is waiting to play.
    /// </summary>
    /// <param name="tree">Bracket to read.</param>
    /// <param name="seedTeamIdentifiers">Entrants in seed order, one per bit.</param>
    /// <param name="round">Round to describe, counted from one.</param>
    /// <returns>Four words of the round's bitmap.</returns>
    public static int[] BuildRoundBitmap(
        TournamentBracketTree tree,
        IReadOnlyList<int> seedTeamIdentifiers,
        int round)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(seedTeamIdentifiers);

        var words = new int[EventConstants.BracketBitmapWordCount];
        for (var node = 1; node < tree.Size; node++)
        {
            if (tree.RoundOf(node) != round)
            {
                continue;
            }

            var winner = tree.WinnerAt(node);
            if (winner == 0)
            {
                continue;
            }

            var seedIndex = IndexOf(seedTeamIdentifiers, winner);
            if (seedIndex < 0)
            {
                continue;
            }

            words[seedIndex / 32] |= 1 << (seedIndex % 32);
        }

        return words;
    }

    /// <summary>Builds every round's bitmap, from round one to the final.</summary>
    /// <param name="tree">Bracket to read.</param>
    /// <param name="seedTeamIdentifiers">Entrants in seed order.</param>
    /// <returns>One bitmap per round; empty for a field with no rounds to play.</returns>
    public static List<int[]> BuildRoundBitmaps(
        TournamentBracketTree tree,
        IReadOnlyList<int> seedTeamIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var rows = new List<int[]>();
        for (var round = 1; round <= tree.RoundCount; round++)
        {
            rows.Add(BuildRoundBitmap(tree, seedTeamIdentifiers, round));
        }

        return rows;
    }

    /// <summary>
    /// Builds the eight standing words. The first is the champion, which is the
    /// only one the client prints by name; the runner-up follows it and the rest
    /// are zero.
    /// </summary>
    /// <param name="championTeamIdentifier">Winning team, or zero while undecided.</param>
    /// <param name="runnerUpTeamIdentifier">Team it beat in the final, or zero.</param>
    /// <returns>The standing words.</returns>
    public static int[] BuildStandings(int championTeamIdentifier, int runnerUpTeamIdentifier)
    {
        var standings = new int[EventConstants.BracketStandingWordCount];
        standings[0] = Math.Max(0, championTeamIdentifier);
        standings[1] = Math.Max(0, runnerUpTeamIdentifier);
        return standings;
    }

    private static int IndexOf(IReadOnlyList<int> values, int value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == value)
            {
                return index;
            }
        }

        return -1;
    }

    private static void WriteIdentity(PacketWriter writer, int correlationIdentifier, int sequence)
    {
        if (correlationIdentifier == 0)
        {
            throw new ArgumentException(
                "A bracket record needs a nonzero team correlation identifier.",
                nameof(correlationIdentifier));
        }

        writer.WriteInt32(correlationIdentifier);
        writer.WriteUInt16(sequence);
    }

    private static void WriteBitmap(PacketWriter writer, int[] words)
    {
        foreach (var word in words)
        {
            writer.WriteInt32(word);
        }
    }

    private static void RequireLength<T>(T[] values, int expected, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != expected)
        {
            throw new ArgumentException($"{name} must contain exactly {expected} values.", nameof(values));
        }
    }

    private static void AssertSize(PacketWriter writer, int start, int expected, string name)
    {
        var written = writer.Size - start;
        if (written != expected)
        {
            throw new InvalidOperationException(
                $"The {name} record is {written} bytes, expected {expected}.");
        }
    }
}
