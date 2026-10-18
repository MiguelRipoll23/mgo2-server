using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The answer half of the keyed pipeline: which records of an inbound frame this
/// host answers at the record level, and which three kinds of record it leaves to
/// their handler or to nobody.
/// </summary>
/// <remarks>
/// It lives apart from the dispatch that calls it so the measurement behind the
/// three exceptions can be read as one piece, and because the dispatch half was
/// past the length a file is allowed.
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Answers the reliable records of a keyed frame, before they reach their
    /// handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Frame ACK entries are excluded by the utility, and a valid profile is the
    /// exception because its roster response starts with the same 0x5001
    /// record-level answer.
    /// </para>
    /// <para>
    /// The join report is the other exception, and it is the handler that answers
    /// it: the report that opens the round is answered by the round's marker, and
    /// only the report that does not open it draws a bare answer
    /// (<c>JoinReportHandler</c>). The recorded host answers `e1 NN` with the
    /// marker — `0x5ade`, and `0x52de` a 0x800 bit along — when its round opens
    /// on that report, and with the bare same record, empty, while the briefing
    /// still waits. Answering every report here would put a second marker on the
    /// wire in the same second as the run, ahead of the `0xd001` the capture
    /// writes first. `docs/protocol/DATA_PHASE_DELIVERY.md` carries both rules
    /// and `docs/protocol/UDP_GAME_CAPTURE.md` §3.2 the pairing.
    /// </para>
    /// <para>
    /// An earlier reading of this host's answers was that they cannot be what the
    /// peer waits for, because a live join was served them and still stalled. The
    /// measurement says otherwise once the answers are read beside the capture's:
    /// the answers served then carried an empty body, and every answer a
    /// 0x8000-class record draws in either capture carries one byte. The peer went
    /// on building records — eighteen of them, its sequence climbing a step each
    /// time — while the window's base stood still, which is what a window whose
    /// base is never moved does. <see cref="PeerAcknowledgementUtils"/> carries
    /// the rule and `docs/protocol/P2P_CONNECT_FSM.md` §7.1c the measurement,
    /// including what the object at `[0xc]` — the base the window advances from —
    /// is written by.
    /// </para>
    /// <para>
    /// The lobby-slot echo is the third exception, and it is measured the way the
    /// other two are: no capture answers `82 NN` · `84 NN`. Both reference
    /// briefings write nothing at all in the pass that receives the pair —
    /// `started-on-briefing` takes it at t+56.360 and its next datagram is its run
    /// 1.9 s later, `started-briefing-then-solo` at t+55.312 and its run 2.3 s
    /// later — and the client answered in that pass is the one that stops there
    /// (`myserver20.pcapng`; the confirmation the old entry pass wrote was removed
    /// for the same reason and this is what is left of it: the two record-level
    /// answers the echo draws, `0xd001` and `0x5001`). <c>PlayerProfileHandler</c>
    /// reads the echo and returns without writing anything, so suppressing the
    /// answers here is what makes the entry's pass silent as every capture's is.
    /// </para>
    /// </remarks>
    /// <param name="frame">Frame as it decoded.</param>
    /// <param name="session">Session the frame arrived on.</param>
    private void AnswerReliableRecords(UdpFrame frame, PeerSession session)
    {
        var answers = frame.Messages
            .Where(PeerAcknowledgementUtils.WaitsForAcknowledgement)
            .Where(message => !TeamArrayFramesUtils.IsArrayEcho(message.Body))
            .Where(message => !IsJoinReport(message))
            .Where(message =>
                (message.Type & ~PeerAcknowledgementUtils.PayloadClassBit) != UdpCommandConstants.PlayerProfile ||
                PlayerProfileRecordParseUtils.Parse(message.Body) is not { Name.Length: > 0 })
            .Select(PeerAcknowledgementUtils.AcknowledgementOf)
            .ToList();
        if (answers.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "[{LogPrefix}] OUT {RemoteAddress} answering {AnswerCount} reliable records: {MessageTypes}",
            LogPrefix,
            session.DialBack,
            answers.Count,
            string.Join(", ", answers.Select(message => $"0x{message.Type:x4}")));

        // Written in this pass, not scheduled off it: the answers carry the same
        // fourth byte as the records they answer, so a peer matches them against
        // the bookkeeping of the frame it has just sent.
        SendMessages(session, answers);
    }
}
