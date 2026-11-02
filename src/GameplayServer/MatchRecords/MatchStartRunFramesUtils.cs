using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The records the recorded host writes when a round opens: the marker, the
/// thirteen records behind it, and the join state record's answer inside them.
/// </summary>
/// <remarks>
/// <para>
/// The pairing is measured and the bytes are named individually in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2 and
/// <c>docs/protocol/DATA_PHASE_DELIVERY.md</c>: the marker is the reporting
/// client's own record identifier with the answer bit set (<c>0x5ade</c> for
/// <c>0x1ade</c>, <c>0x52de</c> for <c>0x12de</c>), and the payload behind it
/// is <see cref="MatchStartBodiesUtils.MatchStartPayload"/> — the opcode
/// <c>e0</c> and the byte the client reported, echoed back. Nothing here can be
/// written before a report arrives, because there is no byte to echo.
/// </para>
/// <para>
/// <b>The order below is measured, and it settles the open point this file used
/// to carry.</b> The run is a compressed frame, so its records are only legible
/// after decompression; decompressed, the run is <b>thirteen records</b> and the
/// marker is a record of its own beside them — the marker's own datagram in
/// <c>mgo2-game1.pcapng</c> (t+46.415, counter 24) and the run behind it in one
/// frame (counter 32793), and both inside one frame in
/// <c>mgo2-game-started-on-briefing.pcapng</c> (t+198.371, counter 655). The two
/// captures write the same thirteen, in the same order:
/// </para>
/// <code>
/// 0x5001 len 0            the peer's entry answered, twice
/// 0x5001 len 0
/// &lt;ops&gt;   len 90           the state block
/// &lt;ops&gt;   len 2            0f 00
/// 0x1001  len 26          the team array      (index)
/// 0x1001  len 26          the occupancy array (index)
/// 0x1001  len 20          the close pair
/// 0x1001  len 20
/// 0x1001  len 7           07 00 00 00 00 00 03 — the roster close
/// &lt;ops&gt;   len 3            the round clock
/// &lt;payload&gt; len 6          the answer to the report
/// 0x0a61  len 1           the empty rules list, under each class
/// 0x0a61  len 1
/// </code>
/// <para>
/// Two things in that shape were missing here before it was measured: the pair
/// the run opens on, and the roster close that ends its
/// <c>0x1001</c> tail (<c>PlayerProfileRecordUtility.BuildRosterClose()</c>, the
/// same seven-byte record a join's roster run closes on). The close pair is
/// therefore a close <em>triple</em>, and the run is not the twelve records this
/// file used to write.
/// </para>
/// <para>
/// <b>The opening pair is the peer's entry answered, not a recorded head.</b>
/// The two <c>0x5001</c> records carry the fourth bytes of the client's own
/// echo — <c>0x5001</c> ordinals <c>3</c> and <c>4</c> answering the
/// <c>0x1001</c> pair of the same ordinals in
/// <c>briefing-then-solo-game</c> — so a run written here opens on
/// <see cref="MatchStartRunService.EntryAnswersOf"/>. A peer whose echo this
/// host never heard falls back to the recorded empty heads, which carry
/// ordinal <c>0</c>.
/// </para>
/// <para>
/// The companion's answer is not in either capture's run — both write it beside
/// the report, within half a second of it — so its place here is the one
/// <c>DATA_PHASE_DELIVERY.md</c> measures and keeps: behind the two arrays, so
/// the client's <c>82 NN</c> answer names the sequence the run published. The
/// state block and the clock are placed where the captures place them, which is
/// not where this file used to: the block comes before the arrays, the clock
/// after both closes, and the payload behind the clock rather than in front of
/// everything.
/// </para>
/// </remarks>
public static class MatchStartRunFramesUtils
{
    // The run is built once and written once, and then written again whole one
    // tick later: the captures' repeats are whole record sets seconds apart on a
    // later tick and never the same frame twice, which is what a number of copies
    // stamped inside the pass that built the run got wrong — those stood or fell
    // with one datagram. The repeat's own shape is RepeatOf below.

    /// <summary>
    /// Operations records the run writes — its state block, the short record behind
    /// it and the round's clock — and so the values it takes from the operations
    /// stream. The capture's run takes <c>00 01 02</c> and its beat's clocks go on
    /// from <c>03</c>, which is what makes the run the opener of that stream rather
    /// than a record in the middle of one.
    /// </summary>
    public const int OperationsRecordCount = 3;

    /// <summary>
    /// Values the run's own <c>0x1001</c>-family records take from the roster
    /// exchange's counter, in send order: the two lobby-slot arrays, the close pair
    /// behind them, the room's own stage, the roster close that ends the tail and
    /// the pair the run writes again behind its payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine, and the values are consecutive because one counter hands them out:
    /// `briefing-then-solo-game`'s round numbers its family <c>19</c>…<c>21</c>.
    /// </para>
    /// <para>
    /// A fresh briefing's run takes seven of them and not nine: its counter runs
    /// <c>07</c>, <c>08</c>, <c>09</c>, <c>0a</c>, then the room's stage at
    /// <c>0b</c> and <c>0c</c> and the roster close at <c>0d</c>; the two a
    /// payload's second array pair would take go unused because that run has no
    /// payload (<see cref="BriefingRosterOrdinalCount"/>).
    /// </para>
    /// </remarks>
    public const int RosterOrdinalCount = 9;

    /// <summary>
    /// Operations records a fresh room's briefing run writes: its state block and
    /// the short record behind it. The round's clock is the third, and a briefing
    /// of a room that has not played a round has no round to carry one for.
    /// </summary>
    public const int BriefingOperationsRecordCount = 2;

    /// <summary>
    /// Values a fresh room's briefing run takes from the roster exchange's counter:
    /// the two arrays, the close pair, the room's stage and the roster close. It is
    /// two fewer than <see cref="RosterOrdinalCount"/> because the run carries no
    /// payload and so no second array pair, which is what puts the reference's
    /// roster close on <c>0d</c> (`briefing-then-solo-game` t+9.780).
    /// </summary>
    public const int BriefingRosterOrdinalCount = 7;

    /// <summary>
    /// Records a run that answers the report opens on and its repeat leaves out:
    /// the marker and the pair behind it, which answer the peer's report and its
    /// entry.
    /// </summary>
    public const int RunOpeningCount = 3;

    /// <summary>
    /// Records a briefing's run opens on and its repeat leaves out: the pair that
    /// answers the peer's entry, with no marker. The report was already answered
    /// beside its publication, so a marker here is the same record twice.
    /// </summary>
    public const int BriefingRunOpeningCount = 2;

    /// <summary>
    /// The run's records as the tick behind it writes them again: everything the
    /// run carries except its opening — the pair behind the marker, and the
    /// marker itself when the run is the report's answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every capture writes the run twice, and the second one is this.</b> The
    /// run answering a report and the set behind it are one tick apart —
    /// <c>mgo2-started-briefing-then-solo-game</c> t+57.626 and t+59.993,
    /// <c>mgo2-game-started-on-briefing</c> t+58.255 and t+60.383, and its round's
    /// t+198.371 and t+200.869, <c>mgo2-game3</c> t+59.596 and t+62.093 — and the
    /// second carries the state block, the short record behind it, the arrays, the
    /// room's stage, the close triple, the clock, the payload and the rules pair
    /// with no marker in front of them and no roster heads. The records keep the
    /// ordinals the run gave them, which is what makes the repeat the same set
    /// re-sent rather than a second run.
    /// </para>
    /// <para>
    /// The marker, where the run carries one, is the answer to the peer's own
    /// report and there is nothing for it to answer a second time; the pair
    /// behind it is the peer's entry answered — or the recorded empty heads when
    /// this host heard no entry. Both are the run's opening rather than its state,
    /// which is what the tick behind it is for. They are dropped by their place in
    /// the run and not by their identifier, because the pair is the peer's own
    /// records answered and so carries that peer's identifiers. A briefing's run
    /// opens on the pair alone — its marker went out beside the report it answered
    /// — so the count it drops is the one it was built with.
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set the run was written under.</param>
    /// <param name="run">The run as it was written.</param>
    /// <returns>The records to write behind the run.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> RepeatOf(
        MatchRecordGenerationUtils generation,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> run)
    {
        // A briefing's run opens on the entry's pair alone, so the marker is not
        // there to drop: the opening count is the one that run was built with.
        // The marker, where a run carries one, sits directly behind that pair,
        // which is the place BuildRun writes it in — not its first record, because
        // the pair is the peer's entry answered and every run carries it.
        var answersReport = run.Count > BriefingRunOpeningCount
            && run[BriefingRunOpeningCount].Type == generation.MatchStartMarker;

        // The item-box group is left behind as well, because its bytes are
        // sequence numbers: a repeat would hand the client a second record with a
        // number it has already accepted, which is the shape the family's window
        // never drains under (`ItemBoxFramesUtils`). The repeat writes its own
        // groups on the counters instead
        // (`MatchStartRunService.RepeatItemBoxGroupsAsync`).
        return
        [
            .. run
                .Skip(answersReport ? RunOpeningCount : BriefingRunOpeningCount)
                .Where(record => record.Type != generation.ItemBox
                    && record.Type != generation.ItemBoxState),
        ];
    }

    /// <summary>
    /// The run's records, in send order, built for the identifier set the round
    /// plays under.
    /// </summary>
    /// <param name="generation">Identifier set of the round being opened.</param>
    /// <param name="reported">Byte the reporting client carried.</param>
    /// <param name="teamArraySequence">
    /// Sequence the lobby-slot array pair opens on. The client answers it and
    /// <see cref="Rooms.TeamArrayService"/> confirms it and moves the pair on.
    /// </param>
    /// <param name="slots">
    /// The room's own players, by the slot each holds in its player table; a fresh
    /// briefing's run names none of them.
    /// </param>
    /// <param name="runOrdinals">
    /// The values the run's own <c>0x1001</c> identifiers take, in send order and
    /// <see cref="RosterOrdinalCount"/> of them, from the roster exchange's counter
    /// (<see cref="Rooms.RoomRosterService.TakeRunOrdinals"/>) rather than pinned:
    /// the two lobby-slot arrays, the room's own stage and the close triple. The
    /// replay parser tells a pair by its two records being consecutive, and they
    /// are consecutive because they are the values one counter handed out — the
    /// recorded run carries 7 and 8 for the arrays and 9, 10 and 11 for the
    /// closes, which is where a join's own records leave the counter. Eight values
    /// and not two, because the closes and the room's stage are under the same
    /// identifiers and take the values behind the pair.
    /// </param>
    /// <param name="operationsOrdinals">
    /// The three values the run's operations records take, in send order
    /// (<see cref="OperationsRecordCount"/>): the state block, the short record
    /// behind it and the round's clock. They come from the round's own operations
    /// stream (<see cref="MatchRecordSequenceService"/>), which the run opens —
    /// the capture's run takes <c>00 01 02</c> and the beat's clocks behind it go
    /// on from <c>03</c>.
    /// </param>
    /// <param name="reportingSet">
    /// The room's reporting players as the join state answer's bit set, one bit per
    /// player in the order it reported (<see cref="Rooms.BriefingStateService.JoinedMask"/>).
    /// It is the room's own set and not a constant: a room of one writes the bit
    /// of its only reporter, and a room of four the run of bits the briefing
    /// capture writes as its players arrive. A fresh briefing's run writes zeros
    /// instead: nobody is placed in the room yet at `briefing-then-solo-game`'s
    /// t+9.780.
    /// </param>
    /// <param name="timeLimitUnits">
    /// Units of the round clock, in the stream's own 125 ms ticks: the round's own
    /// rule time × <see cref="MatchRecordBodiesUtils.ClockUnitsPerMinute"/>, which
    /// the room's settings state (<see cref="Match.MatchRotationEntry.TimeLimitUnits"/>).
    /// The clock is the only place on this channel a round's length is said, so a
    /// host that wrote its own default here would run a round neither the client
    /// nor the host could agree on.
    /// </param>
    /// <param name="entryAnswers">
    /// The answers the peer's entry echo draws, which are the run's opening pair
    /// (<see cref="MatchStartRunService.NoteEntryAnswers"/>). Empty for a peer
    /// whose entry this host never heard, which falls back to the recorded empty
    /// <c>0x5001</c> heads.
    /// </param>
    /// <param name="carriesRoundState">
    /// Whether the run carries the round's own records — its clock, the report's
    /// answer and the rules pair. A briefing that precedes a room's first round
    /// carries none of them, which is the reference's fresh-room run: the records
    /// tell a client a round exists, and a client told that while it is still being
    /// briefed enters the game instead of showing the briefing screen. The room's
    /// stage and its close pair are not among them: the reference's fresh briefing
    /// writes both, the close pair at <c>09</c>/<c>0a</c>, the stage at
    /// <c>0b</c>/<c>0c</c> and the roster close at <c>0d</c>
    /// (`briefing-then-solo-game` t+9.780).
    /// </param>
    /// <param name="answersReport">
    /// Whether this run is the report's answer and so opens on the round's marker.
    /// A briefing's run is not: its report was answered beside the publication it
    /// drew, with the same marker, and writing it again here hands the peer a
    /// second round start three seconds after its entry — where the reference
    /// host's fresh-room run (t+58.255) carries none, its marker having gone out
    /// beside the report at t+56.123.
    /// </param>
    /// <returns>The records of the run.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> BuildRun(
        MatchRecordGenerationUtils generation,
        byte reported,
        byte teamArraySequence,
        IReadOnlyList<Rooms.LobbyTeamSlot> slots,
        IReadOnlyList<byte> runOrdinals,
        IReadOnlyList<byte> operationsOrdinals,
        byte reportingSet,
        int timeLimitUnits,
        IReadOnlyList<UdpMessage> entryAnswers,
        bool carriesRoundState = true,
        bool answersReport = true)
    {
        // The arrays name the room's own players once a round exists: the slot each
        // holds is marked taken and its side is named, which is what
        // `briefing-then-solo-game` publishes at t+147.650. A fresh briefing's run is
        // all free there and the mark arrives in the pair three seconds later
        // (t+12.887), so it belongs to the publications and not to this run.
        var (teams, occupancy) = Rooms.TeamArrayFramesUtils.BuildPair(
            teamArraySequence,
            carriesRoundState ? slots : Array.Empty<Rooms.LobbyTeamSlot>());

        // The run's opening pair is the peer's entry echo answered: the two
        // records <c>82 NN</c>/<c>84 NN</c> draw, carrying the echo's own fourth
        // bytes. `briefing-then-solo-game` writes them at t+9.780 as
        // <c>0x5001</c> ordinals 3 and 4 — the echo it took a second earlier — and
        // its round's run at t+147.650 as the answers to the second echo
        // (ordinals 0x22/0x23). A peer whose echo this host never heard is
        // written the recorded empty heads instead. The pair is always two
        // records, because the repeat behind the run drops exactly that many by
        // their place (RepeatOf).
        var heads = OpeningPair(entryAnswers);

        return
        [
        // The run's opening pair: the answers to the peer's entry echo, or the
        // recorded empty 0x5001 heads when this host heard no echo.
        .. heads,

        // The marker the round is opened with, for the run that is the report's
        // answer — behind the heads, which is where the reference writes it
        // (`briefing-then-solo-game`'s run opens `0xd001`, the two heads and then
        // `0x5ade`, t+147.650). A fresh briefing's answer is the publication written
        // beside the report instead: its run carries no payload, and its marker has
        // already gone out there.
        .. (answersReport
            ? new MatchRecordFramesUtils.RecordMessage[] { new(generation.MatchStartMarker, []) }
            : []),

        // The operations stream's state block and the short record behind it,
        // ahead of the arrays: the order both captures write them in.
        new(
            generation.Operations,
            MatchStateBodiesUtils.MatchStartStateBlock(
                MatchRecordBodiesUtils.OperationsStateOpcode,
                timeLimitUnits),
            operationsOrdinals[0]),
        new(generation.Operations, MatchRecordBodiesUtils.OperationsShort(), operationsOrdinals[1]),

        // The lobby-slot pair. Its two index bytes are the next two values of the
        // roster exchange's counter, which is what the replay parser tells a pair
        // by: consecutive, because one counter handed both out. Both records travel
        // under the roster's own identifier and not under the joiner's opening tag:
        // `mgo2-started-on-game-round1`'s briefings, which are the port client's own,
        // write `0x1001 len 26` twice and never `0x9001` in twenty-three minutes of
        // host traffic — the only `0x9001` in that capture is the joiner's own
        // request, which the host leaves unanswered.
        new(UdpCommandConstants.PlayerProfile, teams, runOrdinals[0]),
        new(UdpCommandConstants.PlayerProfile, occupancy, runOrdinals[1]),

        // The join state record's answer, behind the arrays: the room's reporting
        // players as a bit set. A fresh briefing's run carries the four zero bytes
        // `briefing-then-solo-game` writes — nobody is placed yet — and this host
        // writes the same; the set rides the answer a report draws beside it.
        new(
            generation.JoinState,
            MatchStartBodiesUtils.JoinStateAnswer(carriesRoundState ? reportingSet : (byte)0),
            MatchStartBodiesUtils.JoinStateAnswerClass),

        // The close pair, then the room's own stage, then the seven-byte roster
        // close that ends a 0x1001 tail: the order `briefing-then-solo-game`
        // writes at t+9.780, where the twenty-byte pair takes ordinals 09 and 0a
        // and the two twenty-two-byte stage records take 0b and 0c. The stage is
        // written whether or not a round has played — the reference's fresh
        // briefing carries it — so the values follow the close pair in both cases.
        new(
            UdpCommandConstants.PlayerProfile,
            MatchRecordBodiesUtils.MatchStartCloseFirst(),
            runOrdinals[2]),
        new(
            UdpCommandConstants.PlayerProfile,
            MatchRecordBodiesUtils.MatchStartCloseSecond(),
            runOrdinals[3]),
        // The room's own stage: the two twenty-two-byte records behind the close
        // pair, written in a round's run and in a briefing's alike. The recording
        // writes them before its first round exists — `briefing-then-solo-game`
        // t+9.780 carries the close pair at 09/0a, the stage at 0b/0c and the
        // roster close at 0d — and a fresh-briefing run that dropped them is the
        // join whose client stopped drawing the room's rule instructions.
        new(
            UdpCommandConstants.PlayerProfile,
            RoomStateRecordBodiesUtils.RoomStateRecord(),
            runOrdinals[4]),
        new(
            UdpCommandConstants.PlayerProfile,
            RoomStateRecordBodiesUtils.RoomStateRecordSecond(),
            runOrdinals[5]),
        new(
            UdpCommandConstants.PlayerProfile,
            PlayerProfileRecordUtility.BuildRosterClose(),
            runOrdinals[6]),

        // The round's clock, the answer to the report and the rules pair belong
        // to a round. A fresh room's briefing carries none of them: the reference
        // host writes the answer beside the report instead, and the briefing's
        // own rules pair rides the publications that follow it.
        .. (carriesRoundState
            ? new MatchRecordFramesUtils.RecordMessage[]
            {
                // The round's clock, behind the closes rather than in front of
                // them. Both records that carry a round's length carry this
                // round's own.
                new(
                    generation.Operations,
                    MatchRecordBodiesUtils.RoundTimeLimitUnits(timeLimitUnits),
                    operationsOrdinals[2]),

                // The answer to the report, the byte it carried echoed back under it.
                new(generation.MatchStartPayload, MatchStartBodiesUtils.MatchStartPayload(reported)),

                // The lobby-slot pair once more, behind the payload: the same two
                // records on the next two values the counter hands out (t+147.650,
                // at `20` and `21`). The rules pair travels on the room's tick.
                new(UdpCommandConstants.PlayerProfile, teams, runOrdinals[7]),
                new(UdpCommandConstants.PlayerProfile, occupancy, runOrdinals[8]),
            }
            : []),
        ];
    }

    /// <summary>
    /// The pair a run opens on: the peer's entry echo answered, or the recorded
    /// empty heads for a peer whose echo this host never heard.
    /// </summary>
    /// <remarks>
    /// <b>Two records, always.</b> The repeat behind the run drops its opening by
    /// their place and not by their identifier (<see cref="RepeatOf"/>), so a
    /// frame that carried only one of the echo's records cannot leave the run a
    /// head short and the repeat dropping a record of the run's own.
    /// </remarks>
    /// <param name="entryAnswers">Answers the echo drew, in echo order.</param>
    /// <returns>The two records the run opens on.</returns>
    private static MatchRecordFramesUtils.RecordMessage[] OpeningPair(
        IReadOnlyList<UdpMessage> entryAnswers)
    {
        var pair = new MatchRecordFramesUtils.RecordMessage[BriefingRunOpeningCount];
        for (var index = 0; index < pair.Length; index++)
        {
            pair[index] = index < entryAnswers.Count
                ? new MatchRecordFramesUtils.RecordMessage(
                    entryAnswers[index].Type,
                    entryAnswers[index].Body,
                    entryAnswers[index].Flags)
                : new MatchRecordFramesUtils.RecordMessage(UdpCommandConstants.RosterHead, []);
        }

        return pair;
    }
}
