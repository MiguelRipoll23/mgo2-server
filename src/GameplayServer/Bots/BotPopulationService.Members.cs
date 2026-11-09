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
    /// <param name="reason">Why they are going, when the caller has a reason of its own.</param>
    private void Trim(
        int target,
        BotPopulationMode mode,
        int humanCount,
        bool waitingForPlayer,
        string? reason = null)
    {
        while (Bots.Count > target)
        {
            var victim = Bots.OrderByDescending(bot => bot.Identity.Number).First();
            Remove(
                victim,
                reason ?? (mode switch
                {
                    BotPopulationMode.Fill when waitingForPlayer =>
                        "no player has reported its join to fill",
                    BotPopulationMode.Fill when humanCount == 0 =>
                        "the room holds no player for it to fill",
                    BotPopulationMode.Fill => "a human took its slot",
                    _ => "the room holds fewer bots than it did",
                }));
        }
    }

    /// <summary>
    /// Gives up the bots that stand between a real player and a seat, before the
    /// room is written to that player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a trim and never a fill.</b> The profile pass answers the joining
    /// player with the room this leaves behind, and the room that player can enter is
    /// one with a seat still free in it: a roster naming as many players as the room
    /// holds is read as full, and the join is refused before the client is ever in its
    /// briefing. Seating at the profile is the failure this project already made once,
    /// so this only removes; what seats them again is the player's own join report,
    /// once it is in the briefing.
    /// </para>
    /// <para>
    /// <b>The seat is counted with the player just filed</b>, which is still on its
    /// way in and has no report yet (<see cref="TargetFor"/>), so the run that is
    /// answered holds one fewer entry than the room's capacity and the client is not
    /// told the room is full. A bot the fill adds back on the report is the price of
    /// that guarantee, and it is preferred to turning a player away.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the read of the roster.</param>
    /// <returns><c>true</c> when a bot gave up its seat.</returns>
    public async Task<bool> MakeRoomForPlayerAsync(CancellationToken cancellationToken)
    {
        await reconcileGate.WaitAsync(cancellationToken);
        try
        {
            var mode = BotOptionsUtils.PopulationMode(botOptions);
            var members = roster.Snapshot();
            var humans = members.Count(member => !member.IsBot);

            // The same gate the pass uses: a fill that has heard no player in a
            // briefing holds no bots, so the first player to arrive is answered a room
            // of none rather than one holding the seats it is still loading towards.
            var waitingForPlayer = mode == BotPopulationMode.Fill && !PlayerInBriefing;
            var before = Bots.Count;
            Trim(
                waitingForPlayer ? 0 : TargetFor(mode, members),
                mode,
                humans,
                waitingForPlayer: false,
                reason: "a player is joining and the room must keep a seat free for it");
            return Bots.Count != before;
        }
        finally
        {
            reconcileGate.Release();
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
    /// <remarks>
    /// <b>A bot is seated only once its character exists.</b> The identity is
    /// allocated first, then the row is ensured and the roster is told which
    /// character the bot plays as, so an entry is never filed with an identifier
    /// that nothing holds. A pass that cannot create the row releases the name and
    /// seats nobody: the next pass retries rather than the room showing a bot the
    /// settings burst has no appearance for.
    /// </remarks>
    /// <param name="target">Bots the room should hold.</param>
    /// <param name="policy">Which sides bots may hold.</param>
    /// <param name="humanSides">Sides the room's humans hold.</param>
    /// <param name="rule">The round's rule, which tilts the roles.</param>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    private async Task SpawnAsync(
        int target,
        BotTeamPolicy policy,
        IReadOnlyList<short> humanSides,
        int rule,
        CancellationToken cancellationToken)
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
            int characterIdentifier;
            try
            {
                characterIdentifier = await characters.EnsureAsync(identity, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                identities.Release(identity.Name);
                throw;
            }
            catch (Exception exception)
            {
                identities.Release(identity.Name);
                logger.LogWarning(
                    exception,
                    "Bot {BotName} has no character this pass, so it is not seated; the next pass retries",
                    identity.Name);
                return;
            }

            var member = roster.RegisterBot(identity.Name, characterIdentifier);
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
                "Added bot {BotName} as character {CharacterIdentifier} in slot {BotSlot} on side {Side} as "
                + "{Role}",
                participant.Name,
                characterIdentifier,
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

    /// <summary>
    /// Removes every bot now, which is what an emptied room does.
    /// </summary>
    /// <remarks>
    /// <b>A bot is a seat kept free for a player, and a room with no player left has
    /// nothing to fill.</b> Keeping them would answer the next client with a roster
    /// that names every seat the room holds, which that client reads as a full room;
    /// the fill seats them again on the report the next client sends from its
    /// briefing, so what the arriving client is answered is a room it can enter.
    /// </remarks>
    /// <returns><c>true</c> when the room held a bot and it has been removed.</returns>
    public bool RemoveEveryBot()
    {
        var before = Bots.Count;
        RemoveAll("the room emptied and holds no player to fill");
        return Bots.Count != before;
    }
}
