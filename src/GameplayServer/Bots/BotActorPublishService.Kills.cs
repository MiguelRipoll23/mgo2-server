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
    /// Whether a kill drained this frame owes the scoreboard a record.
    /// </summary>
    /// <remarks>
    /// The feed leaves with the frame and the scoreboard follows it, which is the
    /// order the capture writes: the <c>09 &lt;killer&gt; &lt;victim&gt;</c> record
    /// arrives first and the side's ticket and the two deltas answer it. Publishing
    /// inside this method put every delta on the wire a frame ahead of the feed the
    /// death was announced by.
    /// </remarks>
    private bool scoreboardDue;

    /// <summary>
    /// Writes the deaths waiting in the simulation as kill-feed records and credits
    /// them; the scoreboard the credit changed is published behind the frame.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="records">The beat's records, which the feeds are appended to.</param>
    private void DrainKills(MatchRecordGenerationUtils generation, List<UdpMessage> records)
    {
        var kills = simulation.DrainKills();
        if (kills.Count == 0)
        {
            return;
        }

        foreach (var kill in kills)
        {
            // The death is counted here for every victim, a player as well as a bot.
            // A player's own vitals report cannot be the count: the host has already
            // put that player at zero when its hit landed, so the client's report of
            // zero reads as "still dead" rather than as a death
            // (PlayerVitalsHandler). Measured on the run of 2026-11-11 13:01 —
            // `Player in slot 4 killed the player in slot 8; 1 kill(s), 0 death(s)`,
            // with no victim delta and no ticket for that side, where the working
            // capture answers a player's death with both. A death a client reports
            // on its own, with no feed behind it, is still counted at that report.
            score.NoteKill(kill.KillerSlot, kill.VictimSlot);

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

        scoreboardDue = true;
    }
}
