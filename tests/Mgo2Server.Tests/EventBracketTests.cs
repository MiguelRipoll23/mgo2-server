using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the bracket records and the two derivations behind them: which
/// entrants a round's bitmap marks, and what the standings say. The status
/// column and the bracket rows are counted by the client from its own state
/// rather than from the wire, so a wrong count here desyncs the record that
/// follows instead of truncating this one.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventBracketTests
{
    [Fact]
    public void Round_result_is_32_bytes_plus_one_state_per_entrant()
    {
        var statuses = new byte[] { 2, 5, 0, 0 };
        var writer = new PacketWriter();
        EventBracketUtils.WriteRoundResult(
            writer,
            correlationIdentifier: 700,
            sequence: 5,
            phase: EventConstants.ActiveEventAssignedState,
            round: 2,
            bitmapWords: [1, 0, 0, 0],
            entrantStatuses: statuses);

        var payload = writer.Build();
        Assert.Equal(EventConstants.RoundResultBaseWireSize + statuses.Length, payload.Length);

        // The identity pair the client validates, then the event slot, which it
        // fills from the same stored value as the pair's first word.
        Assert.Equal(700, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(700, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));

        // The byte whose meaning is not established, then the phase, the half
        // this server cannot state, and the round the bracket moved to.
        Assert.Equal(0, payload[10]);
        Assert.Equal(EventConstants.ActiveEventAssignedState, payload[11]);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(12)));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(14)));

        // One 16-byte bitmap row, then the status column.
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(16)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(20)));
        Assert.Equal(statuses[0], payload[32]);
        Assert.Equal(statuses[1], payload[33]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Round_result_refuses_a_round_the_client_cannot_address(int round)
    {
        // A ninth round's row would be written over the snapshot the renderer
        // diffs against, so an unaddressable round is refused rather than sent.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EventBracketUtils.WriteRoundResult(
                new PacketWriter(),
                700,
                1,
                EventConstants.ActiveEventAssignedState,
                round,
                new int[EventConstants.BracketBitmapWordCount],
                []));
    }

    [Fact]
    public void Bracket_state_is_one_row_per_round_plus_the_status_column()
    {
        var rows = new List<int[]>
        {
            new int[] { 0b0101, 0, 0, 0 },
            new int[] { 0b0001, 0, 0, 0 },
        };
        var statuses = new byte[] { 2, 0, 0, 0, 5, 0, 0, 0 };

        var writer = new PacketWriter();
        EventBracketUtils.WriteBracketState(writer, correlationIdentifier: 700, rows, statuses);
        var payload = writer.Build();

        Assert.Equal(
            EventConstants.BracketStateBaseWireSize
                + (2 * EventConstants.BracketBitmapWireSize)
                + statuses.Length,
            payload.Length);

        // No identity header on this record: it opens on the echo identifier the
        // client compares, then the row count.
        Assert.Equal(700, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(0b0101, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));
        Assert.Equal(0b0001, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(22)));
        Assert.Equal(statuses[0], payload[38]);
        Assert.Equal(statuses[4], payload[42]);
    }

    [Fact]
    public void Final_standings_are_46_bytes_and_name_the_champion_first()
    {
        var writer = new PacketWriter();
        EventBracketUtils.WriteFinalStandings(
            writer,
            correlationIdentifier: 700,
            sequence: 9,
            standings: EventBracketUtils.BuildStandings(11, 22),
            reward: 2500);

        var payload = writer.Build();
        Assert.Equal(EventConstants.BracketFinalStandingWireSize, payload.Length);
        Assert.Equal(700, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)9, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(700, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));

        // The client prints the first standing as the winning team's name, and the
        // seven after it are the placings it does not display.
        Assert.Equal(11, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(10)));
        Assert.Equal(22, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(14)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(18)));
        Assert.Equal(2500, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(42)));
    }

    [Fact]
    public void A_round_bitmap_marks_the_entrants_that_advanced()
    {
        var seeds = new[] { 11, 22, 33, 44 };
        var tree = new TournamentBracketTree(seeds);

        // Nothing has been played, so no entrant has advanced anywhere.
        Assert.Equal(new[] { 0, 0, 0, 0 }, EventBracketUtils.BuildRoundBitmap(tree, seeds, 1));

        // Node 2 is the fixture between seeds 1 and 2, and its winner is seed 1.
        Assert.Equal(
            TournamentResultOutcome.Recorded,
            tree.RecordWinner(2, 11));
        Assert.Equal(0b0001, EventBracketUtils.BuildRoundBitmap(tree, seeds, 1)[0]);

        // Node 3 is the other first-round fixture. The first round now marks both
        // of its winners, and the final still marks nobody, because it is unplayed.
        Assert.Equal(TournamentResultOutcome.Recorded, tree.RecordWinner(3, 44));
        Assert.Equal(0b1001, EventBracketUtils.BuildRoundBitmap(tree, seeds, 1)[0]);
        Assert.Equal(0, EventBracketUtils.BuildRoundBitmap(tree, seeds, 2)[0]);

        Assert.Equal(TournamentResultOutcome.Recorded, tree.RecordWinner(1, 11));
        Assert.Equal(0b0001, EventBracketUtils.BuildRoundBitmap(tree, seeds, 2)[0]);
    }

    [Fact]
    public void A_bye_advances_in_the_round_it_was_drawn_in()
    {
        // Three entrants in a four-place bracket: the fourth place is empty, so
        // seed 3 meets nobody and its first-round bit is set without a game.
        var seeds = new[] { 7, 8, 9 };
        var tree = new TournamentBracketTree(seeds);

        var firstRound = EventBracketUtils.BuildRoundBitmap(tree, seeds, 1);
        Assert.Equal(0b100, firstRound[0]);
    }

    [Fact]
    public void Every_round_gets_one_row()
    {
        var seeds = new[] { 11, 22, 33, 44, 55, 66, 77, 88 };
        var tree = new TournamentBracketTree(seeds);

        var rows = EventBracketUtils.BuildRoundBitmaps(tree, seeds);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(EventConstants.BracketBitmapWordCount, row.Length));
    }

    [Fact]
    public void A_ready_fixture_is_named_by_the_two_teams_playing_it()
    {
        var seeds = new[] { 11, 22, 33, 44 };
        var tree = new TournamentBracketTree(seeds);

        // The first round is the two pairs the seed order produced, and either
        // order names the same fixture.
        var first = tree.ReadyFixtureOf(11, 22);
        Assert.Equal(tree.ReadyFixtureOf(22, 11).Node, first.Node);
        Assert.Equal(2, first.Node);
        Assert.Equal(3, tree.ReadyFixtureOf(33, 44).Node);

        // Two teams the bracket never put together are not a fixture, which is
        // what stops a match formed outside the bracket from resolving one.
        Assert.Equal(0, tree.ReadyFixtureOf(11, 33).Node);

        // Only a fixture that is ready counts: the final is not being played
        // while the first round is.
        Assert.Equal(0, tree.ReadyFixtureOf(11, 44).Node);

        tree.RecordWinner(2, 11);
        tree.RecordWinner(3, 44);
        Assert.Equal(1, tree.ReadyFixtureOf(11, 44).Node);
    }

    [Fact]
    public void The_runner_up_is_the_finalist_that_is_not_the_champion()
    {
        var seeds = new[] { 11, 22, 33, 44 };
        var tree = new TournamentBracketTree(seeds);

        // While the bracket is in play there is no champion, so there is nobody
        // it beat either.
        Assert.Equal(0, tree.RunnerUp());

        tree.RecordWinner(2, 11);
        tree.RecordWinner(3, 33);
        Assert.Equal(0, tree.RunnerUp());

        tree.RecordWinner(1, 33);
        Assert.Equal(33, tree.Champion());
        Assert.Equal(11, tree.RunnerUp());
    }

    [Fact]
    public void The_next_round_is_the_lowest_one_still_to_play()
    {
        var seeds = new[] { 11, 22, 33, 44 };
        var tree = new TournamentBracketTree(seeds);

        // Both first-round fixtures are ready, so the bracket is waiting on round
        // one rather than on the final it will reach.
        Assert.Equal(1, tree.NextRound());

        tree.RecordWinner(2, 11);
        tree.RecordWinner(3, 33);
        Assert.Equal(2, tree.NextRound());

        tree.RecordWinner(1, 33);
        Assert.Equal(tree.RoundCount, tree.NextRound());
    }

    [Fact]
    public void Standings_name_the_champion_and_the_runner_up()
    {
        var standings = EventBracketUtils.BuildStandings(11, 22);
        Assert.Equal(EventConstants.BracketStandingWordCount, standings.Length);
        Assert.Equal(11, standings[0]);
        Assert.Equal(22, standings[1]);
        Assert.All(standings[2..], standing => Assert.Equal(0, standing));
    }
}
