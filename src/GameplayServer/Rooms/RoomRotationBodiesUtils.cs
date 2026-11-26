using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room-state body's two entry points for callers that already hold a rotation:
/// the default body and this room's own.
/// </summary>
/// <remarks>
/// Both are one call into <see cref="RoomRotationPayloadUtils"/>, which owns the
/// writers; they live here rather than on <see cref="PlayerSettingsBurstService"/> so
/// that service stays inside the size rule.
/// </remarks>
public static class RoomRotationBodiesUtils
{
    /// <summary>
    /// The working capture's rotation and round, with the room's population and
    /// settings generation.
    /// </summary>
    /// <remarks>
    /// The rotation is the default's, because a room whose rotation could not
    /// be read has nothing better to tell a joiner; the population is not, because
    /// the joiner counts the room it is in and not the capture's two seats.
    /// </remarks>
    /// <param name="playerCount">The room's population, host included.</param>
    /// <param name="teamTickets">The round's ticket limit, both sides.</param>
    /// <param name="levelLimit">The room's level band.</param>
    /// <param name="settingsGeneration">The room's settings generation at body <c>0x9d</c>.</param>
    /// <param name="sneakingDogTags">The room's SNE dog-tag count, body <c>0x8e</c>.</param>
    /// <param name="checkpoints">The room's checkpoint count, body <c>0x95</c>.</param>
    /// <returns>The body.</returns>
    public static byte[] DefaultRotationBody(
        byte playerCount,
        int teamTickets,
        LevelLimitBand? levelLimit = null,
        byte settingsGeneration = MatchSettingsService.SettingsGenerationSeed,
        byte sneakingDogTags = 2,
        byte checkpoints = 5,
        RoomBodySettings? settings = null) =>
        RoomRotationPayloadUtils.BuildDefaultBody(
            playerCount,
            teamTickets,
            levelLimit,
            settingsGeneration,
            sneakingDogTags,
            checkpoints,
            settings);

    /// <summary>
    /// A settings body with this room's rotation and the state of the room it is
    /// being written for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each triple is the parser's layout — map, then rule, then one more byte
    /// (<see cref="PlayerSettingsBurstService.RotationOffset"/>) — and the room stores
    /// the same three values the other way round, as <see cref="MatchRotationEntry"/>:
    /// a rule, a map and the flags byte the client is handed in the room's details
    /// over TCP.
    /// </para>
    /// <para>
    /// <b>The third byte is the round's own options, and it is the byte that tells
    /// a joiner the round is a Drebin Points round.</b> <c>0x02</c> is Drebin
    /// Points, <c>0x04</c> Headshots Only and <c>0x00</c> neither
    /// (<see cref="Mgo2Server.Shared.Types.RoundOption"/>): a room with Drebin
    /// points switched on writes <c>0x02</c> on its configured triples and the same
    /// room's disabled capture writes <c>0x00</c> on all of its own. Writing the
    /// room's own byte therefore reproduces the triple the room was published with,
    /// where writing zero would tell a joiner an ordinary round.
    /// </para>
    /// <para>
    /// <b>Unused entries are zeroed, not repeated.</b> The field has sixteen
    /// slots, but a shorter room rotation only names its configured rounds. The
    /// remaining triples are zero, as they are in the lobby's rotation field and
    /// the working capture. A longer rotation contributes its first sixteen,
    /// because that is all this field holds.
    /// </para>
    /// </remarks>
    /// <param name="rotation">The room's rotation, in play order.</param>
    /// <param name="state">The room state.</param>
    /// <param name="currentRotationIndex">Slot of the round the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    /// <param name="autoAssignTeams">Whether the room puts joiners on a side itself.</param>
    /// <param name="levelLimit">The room's level band.</param>
    /// <param name="settingsGeneration">The room's settings generation at body <c>0x9d</c>.</param>
    /// <param name="sneakingDogTags">The room's SNE dog-tag count, body <c>0x8e</c>.</param>
    /// <param name="checkpoints">The room's checkpoint count, body <c>0x95</c>.</param>
    /// <param name="uniqueCharacters">Whether the round assigns the unique characters.</param>
    /// <param name="uniqueRed">Slot the first unique character occupies, or <c>0x7f</c>.</param>
    /// <param name="uniqueBlue">Slot the second unique character occupies, or <c>0x7f</c>.</param>
    /// <returns>The body.</returns>
    public static byte[] RoomRotationBody(
        IReadOnlyList<MatchRotationEntry> rotation,
        RoomState state,
        int currentRotationIndex,
        byte playerCount,
        RoomBodySettings settings,
        bool autoAssignTeams = true,
        LevelLimitBand? levelLimit = null,
        byte settingsGeneration = MatchSettingsService.SettingsGenerationSeed,
        byte sneakingDogTags = 2,
        byte checkpoints = 5,
        bool uniqueCharacters = false,
        byte uniqueRed = RoomStateBodyConstants.UnassignedUniqueCharacterSlot,
        byte uniqueBlue = RoomStateBodyConstants.UnassignedUniqueCharacterSlot) =>
        RoomRotationPayloadUtils.BuildDefaultBody(
            rotation,
            state,
            currentRotationIndex,
            playerCount,
            RoomRotationPayloadUtils.TeamTickets(rotation, currentRotationIndex),
            autoAssignTeams,
            levelLimit,
            settingsGeneration,
            sneakingDogTags,
            checkpoints,
            uniqueCharacters,
            uniqueRed,
            uniqueBlue,
            settings);
}
