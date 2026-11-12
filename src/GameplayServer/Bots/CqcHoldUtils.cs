namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The client's own CQC record: the state body it writes while it holds an
/// enemy, and the slot of the actor it holds.
/// </summary>
/// <remarks>
/// <b>The grab belongs to the grabber's client, and the only thing it tells the
/// host is who it holds.</b> <c>.pcapngs/actions/actions.pcapng</c> writes
/// <c>59 0f 00 33 01 61 00 c6 07</c> under its own state identifier for about two
/// seconds from t+392.19: the first byte carries the action in its low six bits
/// and the held actor's slot at <c>+1</c>, where the standing form
/// <c>00 ff 00</c> carries <c>0xff</c> there and names nobody. The hold's three
/// actions are <c>0x57</c>, <c>0x58</c> and <c>0x59</c> — 23, 24 and 25 with the
/// <c>0x40</c> acting bit — and the same client writes that family around
/// t+157 and t+313 as well.
/// </remarks>
public static class CqcHoldUtils
{
    /// <summary>Bits of the state body's first byte that carry the action.</summary>
    private const byte ActionMask = 0x3F;

    /// <summary>Actions of the hold, as those bits state them.</summary>
    private static ReadOnlySpan<byte> HoldActions => [0x17, 0x18, 0x19];

    /// <summary>Reads the slot a CQC hold names.</summary>
    /// <param name="body">State body a peer wrote.</param>
    /// <returns>The held slot, or <c>null</c> when the record is not a hold.</returns>
    public static byte? HeldSlotOf(ReadOnlySpan<byte> body)
    {
        if (body.Length < 2)
        {
            return null;
        }

        if (!HoldActions.Contains((byte)(body[0] & ActionMask)))
        {
            return null;
        }

        // `0xff` is the standing form's "no target", which names no actor.
        return body[1] == BotBodyState.NoTarget ? null : body[1];
    }
}
