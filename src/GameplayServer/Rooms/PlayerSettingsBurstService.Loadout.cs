namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The settings payload the roster run answers a joining peer with — and the one
/// the burst falls back to when the room's own loadouts could not be read.
/// </summary>
/// <remarks>
/// It is split from the burst for the size limit rather than for a second
/// question: both callers ask for the same body, and
/// <see cref="PlayerSettingsBurstService.AnsweringLoadoutBody"/> is the only place
/// either of them builds it. The burst itself writes one record per player of the
/// room (<see cref="PlayerSettingsBurstService.BuildSettingsBurst"/>), because a
/// peer that is told only about itself has no appearance for anyone else.
/// </remarks>
public sealed partial class PlayerSettingsBurstService
{
    /// <summary>
    /// The answered peer's settings payload, which the join's roster run and the
    /// burst behind it both carry.
    /// </summary>
    /// <remarks>
    /// The two are one body, not two: `mgo2-game-started-on-briefing` t+6.004
    /// closes its roster answer with the fifty-four-byte `0b 02` payload and
    /// t+8.125 writes the same bytes again in the burst. A join that named the
    /// peer only 2.1 s later left the roster answer describing the room and never
    /// the client it was addressed to.
    /// </remarks>
    /// <param name="loadouts">
    /// The room's own players, or <c>null</c> for a room whose rows could not be
    /// read, which keeps the recorded body.
    /// </param>
    /// <param name="answeringSlot">
    /// The answered peer's slot byte (<c>key + 1</c>), or <c>0</c> for a peer the
    /// roster cannot name.
    /// </param>
    /// <returns>The record body.</returns>
    public byte[] AnsweringLoadoutBody(IReadOnlyList<LobbyLoadout>? loadouts, byte answeringSlot)
    {
        if (loadouts is not { Count: > 0 })
        {
            // The recorded loadout, for a room whose players could not be read.
            return LoadoutBodiesUtils.PlayerLoadout();
        }

        var answering = loadouts.FirstOrDefault(
                loadout => (byte)(loadout.Slot + 1) == answeringSlot)
            ?? loadouts.OrderBy(loadout => loadout.Slot).First();
        return LoadoutBody(answering);
    }

    /// <summary>
    /// One player's loadout body: its own character's fields written into the
    /// recorded body of the shape its skills have.
    /// </summary>
    /// <param name="loadout">The player as the room reads it.</param>
    /// <returns>The record body.</returns>
    private byte[] LoadoutBody(LobbyLoadout loadout)
    {
        var template = LoadoutBodiesUtils.RecordedLoadoutFor(loadout.Skills.Count);
        if (template is not null && LoadoutFramesUtils.TryWrite(loadout, template, out var body))
        {
            return body;
        }

        // A skill count no shape is written for. Every count from none to three
        // has one — the captured bodies for none, two and three, and the built
        // one for a single pair, which the captures never held — so this is a
        // player with four equipped skills, whose length the rule gives and no
        // capture confirms. A body written at the wrong length is one the peer
        // reads as nothing, and the decoder's own skill reader stops at three
        // pairs, so the player is still named, in its own slot, with the recorded
        // no-pairs body behind that one byte.
        logger.LogWarning(
            "Player in slot {Slot} has {SkillCount} equipped skill(s), which no loadout body is written for; "
            + "the recorded no-pairs body is written behind its own slot byte instead",
            loadout.Slot,
            loadout.Skills.Count);

        var fallback = LoadoutBodiesUtils.PlayerLoadout();
        fallback[LoadoutFramesUtils.SlotByteOffset] = (byte)(loadout.Slot + 1);
        return fallback;
    }
}
