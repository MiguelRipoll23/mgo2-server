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
    /// How long a death waits before its feed is written: none, because the feed
    /// rides the beat the victim's vitals reach zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured on `.pcapngs/actions/actions.pcapng`, the working host writes the
    /// two in one frame</b> — <c>09 01 12</c> at t+43.78 against slot 12's vitals
    /// reaching <c>0</c> at t+43.78, <c>09 16 02</c> at t+48.55 against t+48.53,
    /// <c>09 08 07</c> at t+49.71 against t+49.68, and <c>09 09 15</c> at t+55.05
    /// against t+55.05 — never more than one frame apart.
    /// </para>
    /// <para>
    /// <b>This host wrote them 0.22 to 0.24 s apart.</b> On `.pcapngs/myserver.pcapng`
    /// slot 5's vitals reach <c>0</c> at t+32.32 and <c>09 10 05</c> follows at
    /// t+32.56; slot 10's at t+35.53 against <c>09 13 10</c> at t+35.76; slot 12's at
    /// t+42.59 against <c>09 15 12</c> at t+42.82. Eight beats of separation, and the
    /// client builds its kill-log entry on the beat the feed arrives: by then it has
    /// already drawn the death it names, so the entry is never written and the kill is
    /// drawn on the one row the client always has, the host's.
    /// </para>
    /// <para>
    /// The delay was 220 ms, taken from a reading of a different capture's
    /// <c>0x12</c> at t+310.81 and its <c>09 02 08</c> at t+311.03. This one states it
    /// the other way, and the reference is the one this host is measured against.
    /// </para>
    /// </remarks>
    private const int FeedDelayMilliseconds = 0;

    /// <summary>
    /// Age at which a queued feed is dropped instead of written: a room that emptied
    /// inside the delay must not announce its deaths into the round that replaces it.
    /// </summary>
    private const int StaleFeedMilliseconds = 2_000;

    /// <summary>
    /// Deaths waiting for the beat their feed is written on: when the record is due,
    /// and the two slots it names.
    /// </summary>
    private readonly List<(long Due, byte Killer, byte Victim, bool Headshot)> pendingFeeds = [];

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
    /// <returns>The feeds due, each with the victim whose block it is written before.</returns>
    private List<(byte Victim, UdpMessage Record)> DrainKills(MatchRecordGenerationUtils generation)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var kill in simulation.DrainKills())
        {
            pendingFeeds.Add((now + FeedDelayMilliseconds, kill.KillerSlot, kill.VictimSlot, kill.Headshot));
        }

        pendingFeeds.RemoveAll(feed => feed.Due < now - StaleFeedMilliseconds);

        var due = pendingFeeds.Where(feed => feed.Due <= now).ToList();
        if (due.Count == 0)
        {
            return [];
        }

        pendingFeeds.RemoveAll(feed => feed.Due <= now);

        var feeds = new List<(byte Victim, UdpMessage Record)>(due.Count);
        foreach (var kill in due)
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
            score.NoteKill(kill.Killer, kill.Victim, headshot: kill.Headshot);

            // A death with nobody to credit is counted and not announced. A feed
            // whose killer is slot zero is drawn on the dedicated host's own
            // character — `server-99999` — which is where every kill this host could
            // not name went; the reference's feed never names a slot the round does
            // not hold.
            //
            // The same holds for any slot the room's roster does not seat. The
            // replay console resolves a feed's killer as a lobby slot and nothing
            // else — `rpdt.js` reads byte +1 of the `09` record and hands it to
            // `slotPlayer`, which is the roster's slot-to-player map built from the
            // profile entries (`slot2idx`) — so a byte no entry names resolves to no
            // player, and the client draws the kill on the one row it always has,
            // the host's. Naming a slot nobody holds is the same bug as naming zero
            // and is not written for the same reason.
            if (kill.Killer == 0 || !IsSeatedSlot(kill.Killer))
            {
                continue;
            }

            // Three bytes under the operations stream, `09 <killer> <victim>`,
            // numbered on that stream. The item-box identifier an earlier writer
            // used carries a chat macro, which the client played as "Roger that" on
            // every kill.
            feeds.Add((kill.Victim, new UdpMessage(
                generation.Operations,
                (byte)MatchKillFeedBodiesUtils.BodyLength,
                sequences.Take(generation.Operations),
                MatchKillFeedBodiesUtils.Build(kill.Killer, kill.Victim))));
        }

        scoreboardDue = true;
        return feeds;
    }

    /// <summary>
    /// Whether the room's roster holds a member at a slot, which is what makes a
    /// kill-feed record's killer resolvable to a player.
    /// </summary>
    /// <param name="slot">Slot the feed would name as the killer.</param>
    /// <returns><c>true</c> when a player or bot of the room holds the slot.</returns>
    private bool IsSeatedSlot(byte slot) =>
        bots.Bots.Any(bot => bot.SessionSlot == slot) ||
        roster.Snapshot().Any(member => member.SessionSlot == slot);
}
