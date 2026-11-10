using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Utils;
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
    /// <b>The bots belong to the answer and not to a run behind it.</b> The
    /// game-with-bots capture answers a join with the host's entry, <c>BOT-01</c>…
    /// <c>BOT-07</c> at ordinals 1–7, the player at 8, the close at 9 and the room's
    /// count at 10, and adds nothing after it. Seating them on the player's report
    /// instead wrote a second run seconds into the briefing; the client of
    /// 2026-11-10 answered none of it and its briefing held no bot.
    /// </para>
    /// <para>
    /// <b>The seat is counted with the player just filed</b> because that player is
    /// already on the roster here, so the run the answer carries holds the player and
    /// the bots and no more than the room's capacity. The recorded host answers with
    /// nine players named in a room that holds more than nine, so the count names the
    /// population and not the capacity: naming every seat the room holds is what the
    /// client of 2026-11-10 refused.
    /// </para>
    /// </remarks>
    /// <param name="remoteAddress">The player being answered, still on its way in.</param>
    /// <param name="cancellationToken">Token that cancels the read of the roster.</param>
    /// <returns><c>true</c> when the room's bots changed.</returns>
    public async Task<bool> MakeRoomForPlayerAsync(string remoteAddress, CancellationToken cancellationToken)
    {
        await reconcileGate.WaitAsync(cancellationToken);
        try
        {
            // A round that is playing keeps its bots. The seat a joining player would
            // take is free again at the next briefing, and taking it now would remove
            // an actor from a round the client is already in: its block stops being
            // written the moment the population stops holding it.
            if (roundState.IsOpen)
            {
                logger.LogDebug(
                    "A round is playing, so no bot gives up its seat for a joining player yet");
                return false;
            }

            var mode = BotOptionsUtils.PopulationMode(botOptions);
            var members = roster.Snapshot();
            var humans = members.Count(member => !member.IsBot);
            var before = Bots.Count;

            // A player this host has not filed yet counts as one on its way in: the
            // bots take the seats below it and the player is filed after them, which
            // is the order the working host's run names them in — its bots at 1–7 and
            // the player at 8. A re-sent profile finds itself on the roster and adds
            // nothing, so the second copy cannot trim a bot it has just admitted.
            var incoming = members.Any(
                member => !member.IsBot && member.RemoteAddress == remoteAddress) ? 0 : 1;
            var target = TargetFor(mode, members, incoming);
            Trim(target, mode, humans, waitingForPlayer: false);

            var policy = BotOptionsUtils.Team(botOptions);
            var matchIdentifier = match.MatchIdentifier;
            if (policy != BotTeamPolicy.None && matchIdentifier != 0 && Bots.Count < target)
            {
                var teams = await gameService.GetPlayerTeamsAsync(matchIdentifier, cancellationToken);
                var humanSides = members
                    .Where(member => !member.IsBot)
                    .Select(member => teams.TryGetValue(member.CharacterId, out var side) ? side : (short?)null)
                    .Where(RoomSideUtils.IsSide)
                    .Select(side => side!.Value)
                    .ToList();
                var rotation = MatchSettingsUtils.Rotation(serverOptions);
                var entry = rotation[Math.Max(0, roundState.RoundNumber) % rotation.Count];
                await SpawnAsync(target, policy, humanSides, entry.Rule, cancellationToken);
            }

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
        briefings.Forget(bot.RosterKey);
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

    /// <summary>Drops every bot and its state, because the room has emptied.</summary>
    public void Reset()
    {
        RemoveAll("the room emptied and the server is resetting");
        identities.Reset();
        play.Reset();
        sides.Reset();
    }
}
