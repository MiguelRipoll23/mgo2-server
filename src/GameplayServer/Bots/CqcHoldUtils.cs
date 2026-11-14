using Mgo2Server.GameplayServer.Gameplay;

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
/// actions are <c>0x57</c>, <c>0x58</c>, <c>0x59</c> and <c>0x5A</c> —
/// <see cref="ActorAction.CqcHold"/> through <see cref="ActorAction.CqcThrow"/>
/// with the <c>0x40</c> acting bit — and the same client writes that family
/// around t+157 and t+313 as well.
/// </remarks>
public static class CqcHoldUtils
{
    /// <summary>
    /// Heading a held body turns through in one beat, as the working capture states
    /// it.
    /// </summary>
    /// <remarks>
    /// <c>.pcapngs/actions/actions.pcapng</c> slot 15 runs <c>0x200c</c>, <c>0x2894</c>,
    /// <c>0x311c</c>, <c>0x3da4</c> while held — <c>+0x888</c> a beat — until it faces
    /// the client that holds it. A stage heading is a sixteenth of a turn, so that is
    /// about 47 degrees.
    /// </remarks>
    private const int HoldTurnPerBeat = 0x888;

    /// <summary>Reads the slot a CQC hold names.</summary>
    /// <param name="body">State body a peer wrote.</param>
    /// <returns>The held slot, or <c>null</c> when the record is not a hold.</returns>
    public static byte? HeldSlotOf(ReadOnlySpan<byte> body)
    {
        // The hold is the nine-byte acting state, never an aim body. A six-byte
        // class-0x05 aim body also starts `57` in this capture, and reading it as
        // a hold named a random slot on every aim beat.
        if (body.Length != 9 || !ActorActionUtils.IsActing(body[0]))
        {
            return null;
        }

        var hold = ActorActionUtils.Of(body[0])
            is ActorAction.CqcHold
                or ActorAction.CqcStrike
                or ActorAction.CqcFinish
                or ActorAction.CqcThrow;
        if (!hold)
        {
            return null;
        }

        // `0xff` is the standing form's "no target", which names no actor.
        return body[1] is > 0 and < 24 ? body[1] : null;
    }

    /// <summary>
    /// Turns a held body towards the client holding it, by at most one beat's turn.
    /// </summary>
    /// <remarks>
    /// The body keeps its feet and its aim point; only the heading moves, which is
    /// what the capture's held slot does — <c>0x200c</c> through <c>0x3da4</c> at
    /// <c>+0x888</c> a beat until it faces the holder.
    /// </remarks>
    /// <param name="body">The body being held.</param>
    /// <param name="bodies">Every body in the room, which holds the holder's.</param>
    public static void TurnTowardsHolder(BotBodyState body, IReadOnlyList<BotBodyState> bodies)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(bodies);

        if (body.HeldBySlot == BotBodyState.NoTarget
            || bodies.FirstOrDefault(other => other.SessionSlot == body.HeldBySlot) is not { } holder)
        {
            return;
        }

        var wanted = BotLocomotionService.YawTowards(holder.X - body.X, holder.Z - body.Z);
        var delta = (int)wanted - body.Yaw;
        if (delta > 0x8000)
        {
            delta -= 0x10000;
        }
        else if (delta < -0x8000)
        {
            delta += 0x10000;
        }

        var step = Math.Clamp(delta, -HoldTurnPerBeat, HoldTurnPerBeat);
        body.Yaw = (ushort)((body.Yaw + step + 0x10000) % 0x10000);
    }
}
