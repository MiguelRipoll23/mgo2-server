using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The combat half of the dispatch: the tick records a client writes that this
/// host acts on instead of relaying — its hits, and the CQC hold it owns.
/// </summary>
/// <remarks>
/// Split out of <c>GameplayServerService.FrameDispatch.cs</c> to keep both files
/// inside the repository's size limit; the relay, the acknowledgement window and
/// the handler pass are there.
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Applies the two tick records that are the host's own business: a client's
    /// hit, and the actor a client's CQC hold names.
    /// </summary>
    /// <param name="message">The record as it arrived.</param>
    /// <param name="remoteAddress">Endpoint of the peer that wrote it.</param>
    private void NoteCombatTickRecord(UdpMessage message, string remoteAddress)
    {
        // A client's hit is a class-0x00 damage record: the identifier names the
        // victim's slot and the subtype says what the blow is — a body hit, a
        // headshot, a stun, a knife, or a grenade's blast (`Bots.BotHitUtils`). The
        // rest of that class is the operations and objective streams, whose first
        // byte is a count and not a subtype, so the record is decoded here and a rule
        // body is never read as damage.
        if (message.Flags == Bots.BotActorRecordUtils.DamageAttributeClass &&
            Bots.BotHitUtils.Decode(message.Type, message.Body) is { } hit)
        {
            // The echo carries the hit's subtype, and so the death drawn. It is taken
            // before the hit is applied: the echo drops a hit on an actor that is
            // already down, and the blow that kills is the one that carries the
            // headshot's impact point. Applied first, it zeroed the bot and its own
            // echo was then dropped as a hit on a corpse, so every killing headshot
            // was drawn as a body death.
            botActors.NoteDamageEcho(message.Type, message.Body, hit.Kind);

            // The two halves of the hit. A bot's body is the simulation's, and a
            // player's health is the actor writer's — which is the half that was
            // missing: a player damaging a teammate, or putting itself down with a
            // grenade, had its bar moved by nobody and its death counted by nothing.
            var shooter = roster.Find(remoteAddress)?.SessionSlot;
            if (!botSimulation.NoteIncomingDamage(hit, shooter))
            {
                botActors.NoteClientHit(hit, shooter);
            }
        }

        // A client's CQC hold is its own state record and not a hit: the only thing
        // it names is the slot it holds. Our bots kept aiming, firing and walking
        // through a grab, which is a target the client's grab could never settle on —
        // that is the grab this server refused (`Bots.CqcHoldUtils`).
        if (message.Flags == Bots.BotActorRecordUtils.StateAttributeClass &&
            Bots.CqcHoldUtils.HeldSlotOf(message.Body) is { } heldSlot)
        {
            botSimulation.HoldSlot(heldSlot, Environment.TickCount64);
        }
    }
}
