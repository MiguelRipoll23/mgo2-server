using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The kill-feed half of the actor writer: every death the simulation owes a
/// record, and the score that record changes.
/// </summary>
/// <remarks>
/// <b>A feed entry is also a score change, and the host is the side that has the
/// score.</b> Every kill feed and every scoreboard delta in the captures travels
/// host → client, so a death this host saw has to move the numbers itself: the
/// kill to the shooter and the death to the victim, queued as the two deltas
/// <see cref="MatchScoreService.NoteKill"/> leaves behind. A room that wrote the
/// feed alone — this host until now — announced every bot kill and left kills,
/// deaths, score and both sides' tickets at zero.
/// </remarks>
public sealed partial class BotActorPublishService
{
    /// <summary>
    /// Writes the deaths waiting in the simulation as kill-feed records, credits
    /// them, and publishes the scoreboard the credit changed.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="records">The beat's records, which the feeds are appended to.</param>
    private async Task DrainKillsAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        List<UdpMessage> records)
    {
        var kills = simulation.DrainKills();
        if (kills.Count == 0)
        {
            return;
        }

        foreach (var kill in kills)
        {
            // A bot's death is this host's to count: it has no client to report its
            // health reaching zero, where a player's has one and
            // PlayerVitalsHandler counts the death there. Counting both would make
            // one death read as two on the scoreboard, so the kill is credited
            // either way and the death only for the bot.
            score.NoteKill(kill.KillerSlot, kill.VictimSlot, countDeath: IsBotSlot(kill.VictimSlot));

            // Three bytes under the operations stream, `09 <killer> <victim>`,
            // numbered on that stream. The item-box identifier an earlier writer
            // used carries a chat macro, which the client played as "Roger that" on
            // every kill.
            records.Add(new UdpMessage(
                generation.Operations,
                (byte)MatchKillFeedBodiesUtils.BodyLength,
                sequences.Take(generation.Operations),
                MatchKillFeedBodiesUtils.Build(kill.KillerSlot, kill.VictimSlot)));
        }

        await scoreboard.PublishAsync(context);
    }
}
