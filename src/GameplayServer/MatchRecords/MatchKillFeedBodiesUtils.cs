using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The three-byte record the working host writes when a player goes down:
/// <c>09 &lt;killer slot&gt; &lt;victim slot&gt;</c>, under the operations
/// stream's identifier (<c>0x1a54</c> and its <c>0x800</c> sibling) and numbered
/// on that stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on `.pcapngs/game-with-bots-workingserver.pcapng`.</b> Every one
/// of its kill feeds lands on the beat a vitals record reaches zero:
/// <c>09 02 08</c> at t+311.03 against slot 8's health reaching <c>0</c> at
/// t+310.81, <c>09 07 05</c> at t+313.75 against slot 5's at t+313.71, and
/// <c>09 06 01</c> at t+317.38 against slot 1's at t+317.34. The three bytes are
/// the replay parser's opcode <c>0x09</c> and the two slots, and the body is
/// written under the same identifier pair as the round clock
/// (<c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>, <c>OperationsOpcode.KillFeed</c>).
/// </para>
/// <para>
/// **The four-byte body this class used to build under <c>0x1a58</c> is a chat
/// macro, not a kill feed.** It is <c>[kind, slot, macro, 0x02]</c>: a client
/// sends kind <c>0x01</c>, the host broadcasts the same actor and macro as kind
/// <c>0x02</c>, and macro <c>0x00</c> is idle. The room of 2026-11-10 21:00 wrote
/// one per kill; the client read its third byte as a macro and announced
/// <i>Roger that</i> instead of the death. The two slots the old writer meant as
/// killer and victim were the chat macro's slot and phrase index. It is not a
/// kill feed and this host no longer writes it.
/// </para>
/// </remarks>
public static class MatchKillFeedBodiesUtils
{
    /// <summary>Length of the body the working host writes: the opcode and two slots.</summary>
    public const int BodyLength = 3;

    /// <summary>Leading byte of the recorded body, the operations stream's kill feed opcode.</summary>
    public const byte Opcode = (byte)OperationsOpcode.KillFeed;

    /// <summary>
    /// The record one death travels as.
    /// </summary>
    /// <param name="killerSlot">Slot the kill is credited to.</param>
    /// <param name="victimSlot">Slot that went down.</param>
    /// <returns>The three-byte body.</returns>
    public static byte[] Build(byte killerSlot, byte victimSlot) =>
        [Opcode, killerSlot, victimSlot];
}
