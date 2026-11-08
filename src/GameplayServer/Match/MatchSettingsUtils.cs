using System.Text.Json;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The room this host serves, built from this instance's own configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every field is a setting rather than a row.</b> The rotation is the round list
/// the configuration states — each entry a rule named the way the replay console and
/// the documents name it (<c>TDM</c>, <c>BASE</c>) on a map named the way a player
/// knows it (<c>Blood Bath</c>) — and those names become the ids a rotation carries
/// through <see cref="MatchRuleUtils"/> and <see cref="MatchMapUtils"/>, both ported
/// from <c>docs/analysis/replay-console/rpdt.js</c>. The briefing time is the
/// setting beside them, and each round's length and kill limit come from the
/// per-rule bank the room states
/// (<see cref="MatchRotationUtils"/>, <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2).
/// </para>
/// <para>
/// <b>The room row is written from this and never read back.</b> A player-hosted
/// room's settings arrive as the client's own <c>0x4312</c> push and live in rows;
/// a dedicated host receives no such push, so a row read would only ever return
/// what this host put there when it published the room — <see cref="StoredRotationJson"/>
/// is that write — and could only go stale against a setting that changed since.
/// What the client is handed over TCP and what the host plays are therefore one
/// rotation, taken from one place.
/// </para>
/// <para>
/// A name that is no rule or no map is refused
/// (<see cref="MatchRuleUtils.Parse"/>, <see cref="MatchMapUtils.Parse"/>) instead of
/// being mapped onto a neighbouring id, because a host that runs a round no client
/// was told about is a host the client is not playing.
/// </para>
/// </remarks>
public static class MatchSettingsUtils
{
    /// <summary>
    /// The room's rotation, entry zero first: every round the configuration states,
    /// each with its own rule, map, length and kill limit.
    /// </summary>
    /// <param name="options">Options of this instance, which state the rotation.</param>
    /// <returns>The rotation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a name is no rule or no map, or when the bank does not fit the
    /// reply's seventeen words.
    /// </exception>
    public static IReadOnlyList<MatchRotationEntry> Rotation(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return MatchRotationUtils.Parse(options.GameplayServerRotation, RuleTimers(options));
    }

    /// <summary>
    /// The room's seventeen per-rule words, in the order the lobby's game-details
    /// reply states them.
    /// </summary>
    /// <remarks>
    /// They are what a client reads a round's own length out of, so a room that
    /// states none of them is a room whose round is zero minutes long as far as the
    /// joiner is told. The same words are written to the host's settings row
    /// (<see cref="HostSettings"/>), so the reply the lobby gives a browser and the
    /// round this host opens are one setting.
    /// </remarks>
    /// <param name="options">Options of this instance, which state the bank.</param>
    /// <returns>The seventeen words.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the bank does not fit the reply.</exception>
    public static int[] RuleTimers(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return MatchRotationUtils.ParseRuleTimers(options.GameplayServerRuleTimers);
    }

    /// <summary>
    /// The host's settings row, as this instance's configuration states it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the row a browsing client's room details are read from.</b> The
    /// lobby answers <c>0x4313</c> with the host character's row — its briefing
    /// time, its level-limit base, its per-rule bank and its rotation — so a
    /// dedicated host that wrote none of them told every browser its room briefed
    /// for no minutes, played rounds of no length, and ran a rotation of nothing.
    /// Writing the row from the same settings the rounds are opened from is what
    /// keeps the two from disagreeing.
    /// </para>
    /// <para>
    /// It is the whole row rather than a merge: a dedicated host states every field
    /// it serves, so a field left from an earlier build is a setting nothing here
    /// chose.
    /// </para>
    /// </remarks>
    /// <param name="options">Options of this instance, which state the room.</param>
    /// <returns>The row to store.</returns>
    public static CharacterHostSettings HostSettings(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rotation = Rotation(options);

        return new CharacterHostSettings
        {
            // The room's own identity, which is what a browser reads the name off.
            Name = options.GameplayServerRoomName,
            Comment = "Gameplay server",
            MaxPlayers = (short)options.GameplayServerMaxPlayers,
            BriefingTime = options.GameplayServerBriefingMinutes,
            LevelLimitBase = options.GameplayServerLevelLimitBase,

            // The rotation as the client's own room screen holds it: one entry per
            // round, rule then map then flags, which is the shape the 0x4310 block
            // and the 0x4313 reply both carry.
            RotationRules = [.. rotation.Select(entry => (short)entry.Rule)],
            RotationMaps = [.. rotation.Select(entry => (short)entry.Map)],
            RotationFlags = [.. rotation.Select(entry => (short)entry.RoundOptions)],
            RuleTimers = RuleTimers(options),
            Dedicated = true,
        };
    }

    /// <summary>
    /// The rotation as the room row stores it: one three-byte triple per round,
    /// rule then map then flags, in the JSON <c>games.games</c> holds.
    /// </summary>
    /// <remarks>
    /// It is the shape the client is handed by the lobby's <c>0x4313</c> reply,
    /// which reads that column rather than a copy, so the room a client sees in its
    /// list and the rounds this host opens are written from one rotation
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2).
    /// </remarks>
    /// <param name="rotation">Rotation to store.</param>
    /// <returns>The stored form.</returns>
    public static string StoredRotationJson(IReadOnlyList<MatchRotationEntry> rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        return JsonSerializer.Serialize(
            rotation.Select(entry => new[] { entry.Rule, entry.Map, (int)entry.RoundOptions }).ToArray());
    }

    /// <summary>
    /// Refuses a configuration this server cannot serve, before it serves any of
    /// it.
    /// </summary>
    /// <remarks>
    /// It is called once, at startup, because every fault below is one a client
    /// would otherwise discover: a rule or a map no client knows is a round it
    /// cannot play, and a time of zero is a round that ends as it opens. A named
    /// fault is refused with the setting's own environment variable in the message,
    /// so the fix is named and not searched for.
    /// </remarks>
    /// <param name="options">Options of this instance, which state the room.</param>
    /// <exception cref="InvalidOperationException">Thrown when the room cannot be served.</exception>
    public static void Validate(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Every name is parsed here and not at first use: this is the one moment a
        // fault stops a process rather than a round, and it precedes every peer
        // this host will ever answer.
        _ = Rotation(options);

        if (options.GameplayServerBriefingMinutes <= 0)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_BRIEFING_MINUTES must be at least a minute "
                + $"(got {options.GameplayServerBriefingMinutes}); a briefing of zero ends the moment it opens.");
        }

        if (options.GameplayServerMaxPlayers <= 0)
        {
            throw new InvalidOperationException(
                $"GAMEPLAY_SERVER_MAX_PLAYERS must be a positive number of players "
                + $"(got {options.GameplayServerMaxPlayers}); a room of zero admits nobody.");
        }

        if (options.GameplayServerStartRound < 1)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_START_ROUND counts a round from one and cannot be "
                + $"{options.GameplayServerStartRound}; the round it names is one a client is told "
                + "about as the room's current round.");
        }

        if (options.GameplayServerLevelLimitBase < 0)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_LEVEL_LIMIT_BASE cannot be negative "
                + $"(got {options.GameplayServerLevelLimitBase}).");
        }
    }
}
