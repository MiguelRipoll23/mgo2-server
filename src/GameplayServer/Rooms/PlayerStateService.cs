using System.Net;
using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Remembers the last health and death state each peer reported, so a handler
/// can say when a character died rather than repeating the same reading once a
/// tick.
/// </summary>
/// <remarks>
/// The gameplay channel sends a character's vitals on almost every tick, and a
/// character between fights sits at its ceiling for thousands of records in a
/// row. Logging each of those at information level would bury the two records
/// a round actually cares about, so the state is kept here and the change is
/// what gets reported.
/// <para>
/// Health and the position record's death bit are two readings of the same
/// thing that arrive on different records, so both are folded into one
/// death flag per peer. The capture shows them agreeing: every position record
/// that set the death bit belonged to a character whose health record was
/// reading zero.
/// </para>
/// <para>
/// State is filed per remote endpoint, the same key the roster uses, so a peer
/// that reconnects on a new port starts again from its first record rather than
/// reporting a change against state that is no longer its own.
/// </para>
/// </remarks>
public sealed class PlayerStateService
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PlayerObservation> observations = [];

    /// <summary>
    /// Records the vitals a peer reported and reports what they changed.
    /// </summary>
    /// <param name="remote">Endpoint the peer is reached at.</param>
    /// <param name="vitals">Health and stamina the peer reported.</param>
    /// <returns>What the record changed against the previous one.</returns>
    public PlayerStateChange ObserveVitals(IPEndPoint remote, PlayerVitals vitals) =>
        Observe(remote, vitals.Health, vitals.IsDead);

    /// <summary>
    /// Records the death state a position record reported and reports what it
    /// changed. The record carries no health, so the last health seen is kept.
    /// </summary>
    /// <param name="remote">Endpoint the peer is reached at.</param>
    /// <param name="isDead">Death bit of the position record.</param>
    /// <returns>What the record changed against the previous one.</returns>
    public PlayerStateChange ObservePosition(IPEndPoint remote, bool isDead) =>
        Observe(remote, null, isDead);

    private PlayerStateChange Observe(IPEndPoint remote, int? health, bool isDead)
    {
        var key = remote.ToString();

        lock (gate)
        {
            var previous = observations.GetValueOrDefault(key);
            observations[key] = new PlayerObservation(health ?? previous?.Health, isDead);
            return new PlayerStateChange(
                previous is not null,
                previous?.Health,
                previous?.IsDead ?? isDead,
                isDead);
        }
    }
}

/// <summary>
/// What one observation changed about a character's state, against the record
/// that came before it.
/// </summary>
/// <param name="HadPrevious">
/// Whether this peer had reported before. A position record carries no health,
/// so this is not the same question as whether a health was known, and the two
/// are kept apart: a change needs a previous record, not a previous number.
/// </param>
/// <param name="PreviousHealth">
/// Health the previous health-carrying record reported, or <c>null</c> when
/// none has arrived.
/// </param>
/// <param name="WasDead">Whether the character was reported dead before.</param>
/// <param name="IsDead">Whether the character is reported dead now.</param>
public sealed record PlayerStateChange(
    bool HadPrevious,
    int? PreviousHealth,
    bool WasDead,
    bool IsDead)
{
    /// <summary>Whether the character's death state flipped with this record.</summary>
    /// <remarks>
    /// The peer's first record is not a change: it has nothing to have changed
    /// from, so a character that joins already dead is not reported as dying.
    /// </remarks>
    public bool DeathChanged => HadPrevious && WasDead != IsDead;

    /// <summary>Whether the character died with this record.</summary>
    public bool Died => DeathChanged && IsDead;

    /// <summary>Whether the character came back with this record.</summary>
    public bool Revived => DeathChanged && !IsDead;
}

/// <summary>Last state seen for one peer.</summary>
/// <param name="Health">Health last reported, when any record has carried one.</param>
/// <param name="IsDead">Whether the character was last reported dead.</param>
internal sealed record PlayerObservation(int? Health, bool IsDead);