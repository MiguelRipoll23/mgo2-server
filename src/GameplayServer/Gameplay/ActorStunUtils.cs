namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The knock-out rule both a bot's body and a player's slot use: the blow that
/// empties the stamina bar decides whether the actor sleeps or is only stunned,
/// and the last such blow wins.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device and the tranquilliser sleep; CQC and the knife stun.</b>
/// <c>.pcapngs/actions/sleep-by-sgmines.pcapng</c> lays slot 1 down at t+133.46
/// and stands it at t+160.66, and slot 2 from t+119.06 to t+146.26, while health
/// stays 250. The tranquilliser's <c>0x41</c>/<c>0x42</c> hit sleeps an emptied
/// bar the same way; a CQC impact or knife shows the <c>C1 1A</c> stun pose while
/// stamina refills.
/// </para>
/// <para>
/// <b>The short stun is not measured.</b> The records say what a blow costs, not
/// how long the body stays down, so three seconds is this port's own number —
/// the one value in this path a capture here does not state.
/// </para>
/// </remarks>
public static class ActorStunUtils
{
    /// <summary>How long a short stun lasts, in milliseconds.</summary>
    public const int StunMilliseconds = 3_000;

    /// <summary>How long a sleeping device's sleep lasts, in milliseconds.</summary>
    public const int SleepDeviceMilliseconds = 26_000;

    /// <summary>What a knock-out blow leaves the actor holding.</summary>
    /// <param name="Sleeping">Whether the actor is asleep.</param>
    /// <param name="StunnedUntilMilliseconds">When the knock-out ends.</param>
    /// <param name="SleepUntilMilliseconds">When a sleep ends; zero when it is not asleep.</param>
    public readonly record struct State(
        bool Sleeping,
        long StunnedUntilMilliseconds,
        long SleepUntilMilliseconds);

    /// <summary>
    /// Applies one knock-out blow's decision to a stamina bar.
    /// </summary>
    /// <remarks>
    /// A blow that leaves stamina above zero only extends the short stun. A blow
    /// that empties it sleeps the actor when its family sleeps and leaves the short
    /// stun otherwise; in both cases the blow replaces what an earlier one set, so
    /// a CQC blow wakes a sleeping body into the stun pose while a tranquilliser
    /// keeps it asleep.
    /// </remarks>
    /// <param name="stamina">Stamina after the blow's points were spent.</param>
    /// <param name="wasSleeping">Whether the actor was asleep before the blow.</param>
    /// <param name="sleeps">Whether this blow's family sleeps an emptied bar.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <param name="stunnedUntilMilliseconds">When the current knock-out ends.</param>
    /// <param name="sleepUntilMilliseconds">When the current sleep ends.</param>
    /// <param name="stunMilliseconds">How long the short stun lasts.</param>
    /// <param name="sleepMilliseconds">How long this sleep lasts.</param>
    /// <returns>The state the blow leaves.</returns>
    public static State Apply(
        short stamina,
        bool wasSleeping,
        bool sleeps,
        long now,
        long stunnedUntilMilliseconds,
        long sleepUntilMilliseconds,
        int stunMilliseconds = StunMilliseconds,
        int sleepMilliseconds = SleepDeviceMilliseconds)
    {
        var shortStunUntil = now + stunMilliseconds;
        if (stamina > 0)
        {
            return new(
                wasSleeping,
                Math.Max(stunnedUntilMilliseconds, shortStunUntil),
                sleepUntilMilliseconds);
        }

        if (sleeps)
        {
            var sleepUntil = Math.Max(
                sleepUntilMilliseconds,
                now + sleepMilliseconds);
            return new(
                true,
                Math.Max(stunnedUntilMilliseconds, sleepUntil),
                sleepUntil);
        }

        return new(false, shortStunUntil, 0);
    }
}
