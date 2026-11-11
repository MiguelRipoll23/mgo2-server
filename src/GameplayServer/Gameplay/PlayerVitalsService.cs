using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// Holds the health and stamina each player last reported, keyed by the endpoint
/// the report arrived from.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one place a player's vitals are read, and a hit the bots land is
/// the one value written back.</b> A client reports its own health and stamina under
/// <c>0x0080</c> (and <c>0x0880</c> for the second player's record), which
/// <c>UDP_GAME_CAPTURE.md</c> §5 measures end to end — 250 is full, 0 is death,
/// and the ladder between them is the game's own — and those records reach this
/// host already. What they were not was *kept*: a tick record with no handler of
/// its own is relayed to the room's other peers and forgotten, so the host could
/// not say what any player's health was.
/// </para>
/// <para>
/// <b>The beat's constant <c>fa fa</c> is kept until this host has a hit of its
/// own to state.</b> The steady stream's two-byte vitals record is <c>fa fa</c> in
/// all 4 298 captured observations, including while <c>0x0080</c> reports death
/// (<see cref="MatchRecords.MatchRecordBodiesUtils.Vitals"/>), and that is what
/// this host writes while <see cref="NoteDamage"/> has set nothing. Once it has,
/// the beat carries the health the hit left: this room's beat writes that record
/// under the player's own actor identifier (<see cref="MatchRecords.MatchRecordService"/>),
/// where a constant full health is a bar that never moves, which is the report of
/// 2026-11-11 — bots that aim, hit nothing visible, and a player who never falls.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class PlayerVitalsService(ILogger<PlayerVitalsService> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PlayerVitals> lastReported = [];

    /// <summary>
    /// Players whose health this host has set itself, with a hit it landed. A
    /// report from such a player does not raise the value again: the client does
    /// not know about the hit yet, and its own <c>fa fa</c> would put the bar back
    /// to full on the next tick — the room of 2026-11-11, whose player never fell.
    /// </summary>
    private readonly HashSet<string> held = [];

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
            if (held.Contains(remoteAddress))
            {
                return false;
            }

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
    /// Lowers a player's held health to what a hit this host landed leaves, and
    /// holds it against the client's own reports until the respawn clears it.
    /// </summary>
    /// <remarks>
    /// The beat's vitals record rides the player's own actor identifier in this
    /// room (<see cref="MatchRecords.MatchRecordService"/>), so a constant
    /// <c>fa fa</c> written there every 33 ms is a health bar that never moves. The
    /// value held here is what that record carries while a hit stands.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint of the player that was hit.</param>
    /// <param name="vitals">Health the hit leaves.</param>
    public void NoteDamage(string remoteAddress, PlayerVitals vitals)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(vitals);

        lock (gate)
        {
            if (lastReported.TryGetValue(remoteAddress, out var existing)
                && existing.Health <= vitals.Health)
            {
                return;
            }

            lastReported[remoteAddress] = vitals;
            held.Add(remoteAddress);
        }
    }

    /// <summary>
    /// Clears a held hit, because the player has stood up again at full health.
    /// </summary>
    /// <param name="remoteAddress">Endpoint of the player that respawned.</param>
    public void Restore(string remoteAddress)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);

        lock (gate)
        {
            held.Remove(remoteAddress);
            lastReported[remoteAddress] = PlayerVitals.Full;
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

    /// <summary>Drops every player's vitals, because the room has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            lastReported.Clear();
            held.Clear();
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
            held.Remove(remoteAddress);
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
