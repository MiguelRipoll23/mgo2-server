using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The membership half of the room's bot population: trimming it to the count
/// the pass decided, choosing each bot's side, seating it, and taking it out
/// again.
/// </summary>
/// <remarks>
/// Split out of <c>BotPopulationService.cs</c> to keep both files under the
/// 400-line limit; the pass that reads the room and decides the numbers is
/// there.
/// </remarks>
public sealed partial class BotPopulationService
{
    /// <summary>Removes bots until the room holds the target number.</summary>
    /// <param name="target">Bots the room should hold.</param>
    /// <param name="mode">How a bot holds its slot.</param>
    /// <param name="humanCount">Humans on the roster, the host not counted.</param>
    /// <param name="waitingForPlayer">Whether a fill is still waiting for its player's join report.</param>
    private void Trim(int target, BotPopulationMode mode, int humanCount, bool waitingForPlayer)
    {
        while (Bots.Count > target)
        {
            var victim = Bots.OrderByDescending(bot => bot.Identity.Number).First();
            Remove(
                victim,
                mode switch
                {
                    BotPopulationMode.Fill when waitingForPlayer =>
                        "no player has reported its join to fill",
                    BotPopulationMode.Fill when humanCount == 0 =>
                        "the room holds no player for it to fill",
                    BotPopulationMode.Fill => "a human took its slot",
                    _ => "the room holds fewer bots than it did",
                });
        }
    }

    /// <summary>Restates each bot's side from the room as it is now.</summary>
    /// <param name="policy">Which sides bots may hold.</param>
    /// <param name="humanSides">Sides the room's humans hold.</param>
    private void ResolveSides(BotTeamPolicy policy, IReadOnlyList<short> humanSides)
    {
        foreach (var bot in Bots)
        {
            var others = Bots
                .Where(other => other.Name != bot.Name && other.Team is not null)
                .Select(other => other.Team!.Value)
                .ToList();
            var side = teamAssignment.ChooseTeam(policy, humanSides, others);
            if (side == bot.Team)
            {
                continue;
            }

            lock (gate)
            {
                bots[bot.Name] = bot.OnTeam(side);
            }

            sides.Set(bot.SessionSlot, side);

            logger.LogInformation(
                "Bot {BotName} moved to side {Side}",
                bot.Name,
                side?.ToString() ?? "none");
        }
    }

    /// <summary>Adds bots until the room holds the target number.</summary>
    /// <param name="target">Bots the room should hold.</param>
    /// <param name="policy">Which sides bots may hold.</param>
    /// <param name="humanSides">Sides the room's humans hold.</param>
    /// <param name="rule">The round's rule, which tilts the roles.</param>
    private void Spawn(
        int target,
        BotTeamPolicy policy,
        IReadOnlyList<short> humanSides,
        int rule)
    {
        while (Bots.Count < target)
        {
            var botSides = Bots
                .Where(bot => bot.Team is not null)
                .Select(bot => bot.Team!.Value)
                .ToList();
            var side = teamAssignment.ChooseTeam(policy, humanSides, botSides);
            if (side is null)
            {
                logger.LogWarning(
                    "No side is free for a bot under {Policy}, so the room holds {BotCount} of {TargetCount} "
                    + "bot(s)",
                    policy,
                    Bots.Count,
                    target);
                return;
            }

            var identity = identities.Allocate();
            var member = roster.RegisterBot(identity.Name);
            var role = roleAssignment.Choose(identity.Number, rule);
            var participant = new BotParticipant(
                identity,
                member.SessionSlot,
                member.RosterIndex,
                side,
                role,
                BotConfiguration.Default.WithRole(role));

            lock (gate)
            {
                bots[identity.Name] = participant;
            }

            sides.Set(participant.SessionSlot, side);

            // The recorded hosts' bots are ready in the record that adds them and
            // never send a report, which is what this is.
            briefings.NoteBriefingReady(
                participant.RosterKey,
                participant.SessionSlot,
                BriefingReadyBodiesUtils.ReadyState);

            logger.LogInformation(
                "Added bot {BotName} in slot {BotSlot} on side {Side} as {Role}",
                participant.Name,
                participant.SessionSlot,
                side,
                role);
        }
    }

    /// <summary>Takes one bot out of the room and releases its name.</summary>
    /// <param name="bot">The bot to remove.</param>
    /// <param name="reason">Why it is being removed.</param>
    private void Remove(BotParticipant bot, string reason)
    {
        roster.Remove(bot.RosterKey);
        identities.Release(bot.Name);
        play.Forget(bot.Name);
        sides.Remove(bot.SessionSlot);
        lock (gate)
        {
            bots.Remove(bot.Name);
        }

        logger.LogInformation(
            "Removed bot {BotName} from slot {BotSlot}: {Reason}",
            bot.Name,
            bot.SessionSlot,
            reason);
    }

    /// <summary>Removes every bot, which is what a disabled population does.</summary>
    /// <param name="reason">Why they are all going.</param>
    private void RemoveAll(string reason)
    {
        foreach (var bot in Bots)
        {
            Remove(bot, reason);
        }
    }
}
