using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// Holds the health and stamina each player last reported, keyed by the endpoint
/// the report arrived from.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one place a player's vitals are read, and nothing is written
/// back with them.</b> A client reports its own health and stamina under
/// <c>0x0080</c> (and <c>0x0880</c> for the second player's record), which
/// <c>UDP_GAME_CAPTURE.md</c> §5 measures end to end — 250 is full, 0 is death,
/// and the ladder between them is the game's own — and those records reach this
/// host already. What they were not was *kept*: a tick record with no handler of
/// its own is relayed to the room's other peers and forgotten, so the host could
/// not say what any player's health was.
/// </para>
/// <para>
/// <b>The host's own beat record is not written from here.</b> The steady stream
/// carries a two-byte <c>0x090c</c> record whose body is <c>fa fa</c> in all
/// 4 298 captured observations, including while <c>0x0080</c> reports death; the
/// capture's own note is to keep it and not to substitute a player's live vitals
/// into it, and that is what this server does
/// (<see cref="MatchRecords.MatchRecordBodiesUtils.Vitals"/>). What the values
/// kept here are for is the host's own reading: a round's scoreboard, a vitals
/// display, or a question about a player asked after the fact.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class PlayerVitalsService(ILogger<PlayerVitalsService> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PlayerVitals> lastReported = [];

    /// <summary>
    /// Records a player's vitals, and reports whether that is news.
    /// </summary>
    /// <remarks>
    /// Vitals arrive on the game's own tick — thousands of records a round — so
    /// only a change is news and only a change is logged at information. The
    /// first report of a player is news as well, because it is the point from
    /// which the host knows anything at all about the player's health.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the report came from.</param>
    /// <param name="vitals">Health and stamina the report carried.</param>
    /// <returns><c>true</c> when the value differs from the one held.</returns>
    public bool Note(string remoteAddress, PlayerVitals vitals)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(vitals);

        lock (gate)
        {
            if (lastReported.TryGetValue(remoteAddress, out var existing))
            {
                if (existing == vitals)
                {
                    return false;
                }

                lastReported[remoteAddress] = vitals;
                logger.LogInformation(
                    "{RemoteAddress} is now at {Health} health and {Stamina} stamina, from {PreviousHealth}/{PreviousStamina}",
                    remoteAddress,
                    vitals.Health,
                    vitals.Stamina,
                    existing.Health,
                    existing.Stamina);
                return true;
            }

            lastReported[remoteAddress] = vitals;
            logger.LogInformation(
                "{RemoteAddress} first reported {Health} health and {Stamina} stamina",
                remoteAddress,
                vitals.Health,
                vitals.Stamina);
            return true;
        }
    }

    /// <summary>
    /// The vitals a player last reported.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <param name="vitals">The value held, when there is one.</param>
    /// <returns><c>true</c> when the player has reported vitals.</returns>
    public bool TryGet(string remoteAddress, out PlayerVitals vitals)
    {
        lock (gate)
        {
            return lastReported.TryGetValue(remoteAddress, out vitals!);
        }
    }

    /// <summary>
    /// Drops a player's vitals, because the player has left the room. Called by
    /// the session reaper.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player was filed under.</param>
    /// <returns><c>true</c> when the player had reported a value.</returns>
    public bool Forget(string remoteAddress)
    {
        lock (gate)
        {
            if (!lastReported.Remove(remoteAddress))
            {
                return false;
            }

            logger.LogDebug(
                "{RemoteAddress} left with vitals held; {PlayerCount} player(s) still have a value",
                remoteAddress,
                lastReported.Count);
            return true;
        }
    }
}
