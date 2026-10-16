using Mgo2Server.GameplayServer.MatchRecords;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the two briefing records that decide a round's start: the countdown a
/// peer is told how much of the room's briefing is left with, and the ready state a
/// client reports in.
/// </summary>
/// <remarks>
/// Both bodies are held to the bytes the recorded host and its clients wrote in
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> and both are asked to refuse
/// the malformed bodies that arrive on the same identifier.
/// </remarks>
public sealed class BriefingRecordCodecTests
{
    /// <summary>
    /// The countdown round-trips its seconds and its whole ready set: the set is
    /// two bytes, and a bit past the first one is a slot a room really holds.
    /// </summary>
    [Fact]
    public void TheCountdownRoundTripsItsSecondsAndItsWholeReadySet()
    {
        // The capture's closing zero, t+113.758: bits 1, 3, 5, 6, 7 and 8.
        var closing = BriefingCountdownBodiesUtils.CountdownBody(seconds: 0, readySet: 0x01ea);

        Assert.Equal(BriefingCountdownBodiesUtils.RecordLength, closing.Length);
        Assert.True(BriefingCountdownBodiesUtils.IsCountdown(closing));
        Assert.Equal(0, BriefingCountdownBodiesUtils.SecondsOf(closing));
        Assert.Equal((ushort)0x01ea, BriefingCountdownBodiesUtils.ReadySetOf(closing));
        Assert.Equal(new byte[] { 0x0b, 0x00, 0x00, 0xea, 0x01, 0x00, 0x00 }, closing);

        // The same room's second briefing, t+811.040: 0b 30 00 02 7c 00 00 — the
        // set 0x7c02, whose second byte is what a one-byte set could not carry.
        var second = BriefingCountdownBodiesUtils.CountdownBody(seconds: 48, readySet: 0x7c02);

        Assert.Equal(48, BriefingCountdownBodiesUtils.SecondsOf(second));
        Assert.Equal((ushort)0x7c02, BriefingCountdownBodiesUtils.ReadySetOf(second));
        Assert.Equal(new byte[] { 0x0b, 0x30, 0x00, 0x02, 0x7c, 0x00, 0x00 }, second);

        // A room with nobody ready carries no bits at all, which is the record a
        // briefing writes before its first ready.
        Assert.Equal((ushort)0, BriefingCountdownBodiesUtils.ReadySetOf(
            BriefingCountdownBodiesUtils.CountdownBody(seconds: 60, readySet: 0)));
    }

    /// <summary>The countdown refuses a body that is not one of its own.</summary>
    [Fact]
    public void TheCountdownRefusesABodyThatIsNotOne()
    {
        Assert.False(BriefingCountdownBodiesUtils.IsCountdown([]));
        Assert.False(BriefingCountdownBodiesUtils.IsCountdown(new byte[6]));
        Assert.False(BriefingCountdownBodiesUtils.IsCountdown(new byte[8]));

        // The kill feed arrives on the same identifier and is three bytes long; a
        // record of the right length that opens on another opcode is not a count.
        var otherOpcode = BriefingCountdownBodiesUtils.CountdownBody(seconds: 60, readySet: 0x40);
        otherOpcode[0] = 0x09;
        Assert.False(BriefingCountdownBodiesUtils.IsCountdown(otherOpcode));
    }

    /// <summary>
    /// The ready record round-trips the player's own slot and the state it reports.
    /// </summary>
    [Fact]
    public void TheReadyRecordRoundTripsTheSlotAndTheState()
    {
        // The capture's own record, t+112.750 and again at t+844.858: 1f 05 01, the
        // room's reporting client at slot 5.
        var ready = new byte[] { BriefingReadyBodiesUtils.ReadyOpcode, 0x05, BriefingReadyBodiesUtils.ReadyState };

        Assert.Equal(BriefingReadyBodiesUtils.RecordLength, ready.Length);
        Assert.True(BriefingReadyBodiesUtils.IsBriefingReady(ready));
        Assert.Equal((byte)5, BriefingReadyBodiesUtils.SlotOf(ready));
        Assert.Equal(BriefingReadyBodiesUtils.ReadyState, BriefingReadyBodiesUtils.StateOf(ready));
        Assert.True(BriefingReadyBodiesUtils.IsReady(ready));

        // A state that is not the ready is a record the stream carries and not a
        // ready: no capture holds one, so the codec names only the value it has.
        var otherState = new byte[] { BriefingReadyBodiesUtils.ReadyOpcode, 0x05, 0x00 };

        Assert.True(BriefingReadyBodiesUtils.IsBriefingReady(otherState));
        Assert.False(BriefingReadyBodiesUtils.IsReady(otherState));
    }

    /// <summary>The ready record refuses a body that is not one of its own.</summary>
    [Fact]
    public void TheReadyRecordRefusesABodyThatIsNotOne()
    {
        // The stream's six-byte 1f record, which shares the opcode and not the
        // shape: it is a state record of the live round and not a briefing ready.
        Assert.False(BriefingReadyBodiesUtils.IsBriefingReady(new byte[] { 0x1f, 0x01, 0x34, 0xfe, 0xcd, 0x00 }));

        // The countdown, on the identifier a ready is written to.
        Assert.False(BriefingReadyBodiesUtils.IsBriefingReady(new byte[] { 0x0b, 0x05, 0x01 }));

        Assert.False(BriefingReadyBodiesUtils.IsBriefingReady([0x1f, 0x05]));
        Assert.False(BriefingReadyBodiesUtils.IsBriefingReady([]));
    }
}
