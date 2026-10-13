using System.Text.Json;
using Mgo2Server.Shared.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The room this host serves, built from this instance's own configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every field is a setting rather than a row.</b> The rule and the map are
/// named the way the replay console and the documents name them — <c>TDM</c>,
/// <c>Blood Bath</c> — and those names become the ids a rotation carries through
/// <see cref="MatchRuleUtils"/> and <see cref="MatchMapUtils"/>, both ported from
/// <c>docs/analysis/replay-console/rpdt.js</c>. The briefing time and each round's
/// length and kill limit are the settings beside them
/// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §1, §2).
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
/// The reading order is the reason this file exists rather than a reader per
/// caller, and it is now a single step: configuration first and configuration
/// last. A name that is no rule or no map is refused
/// (<see cref="MatchRuleUtils.Parse"/>, <see cref="MatchMapUtils.Parse"/>) instead
/// of being mapped onto a neighbouring id, because a host that runs a rule or a map
/// no client was told about is a host the client is not playing.
/// </para>
/// </remarks>
public static class MatchSettingsUtils
{
    /// <summary>
    /// Flags byte a round's triple carries. Its meaning is [U] in the reply's
    /// layout (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2) and zero is the
    /// value fifteen of the sixteen recorded triples hold, so no setting states one
    /// and a published room carries the reading the captures show.
    /// </summary>
    private const int Flags = 0;

    /// <summary>
    /// The round the gameplay server's room plays, named and timed by its own
    /// settings.
    /// </summary>
    /// <param name="options">Options of this instance, which state the round.</param>
    /// <returns>The round's rule, map, flags, length and kill limit.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured rule or map is not one the client knows.
    /// </exception>
    public static MatchRotationEntry Round(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new MatchRotationEntry(
            MatchRuleUtils.Parse(options.GameplayServerRule),
            MatchMapUtils.Parse(options.GameplayServerMap),
            Flags,
            options.GameplayServerRoundMinutes,
            options.GameplayServerKillLimit);
    }

    /// <summary>
    /// The settings of a room this host publishes: its identity from the room row
    /// it wrote, and everything it runs from its own configuration.
    /// </summary>
    /// <param name="options">Options of this instance, which state the room.</param>
    /// <param name="gameIdentifier">Identifier of the room row this host published.</param>
    /// <param name="name">Name that row is published under.</param>
    /// <returns>The room's settings.</returns>
    public static MatchSettings From(ServerOptions options, int gameIdentifier, string name)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new MatchSettings(
            gameIdentifier,
            name,
            options.GameplayServerBriefingMinutes,
            [Round(options)]);
    }

    /// <summary>
    /// The rotation as the room row stores it: one three-byte triple per round,
    /// rule then map then flags, in the JSON <c>games.games</c> holds.
    /// </summary>
    /// <remarks>
    /// It is the shape the client is handed by the lobby's <c>0x4313</c> reply,
    /// which reads that column rather than a copy, so the room a client sees in its
    /// list and the round this host opens are written from one rotation
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2).
    /// </remarks>
    /// <param name="rotation">Rotation to store.</param>
    /// <returns>The stored form.</returns>
    public static string StoredRotationJson(IReadOnlyList<MatchRotationEntry> rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        return JsonSerializer.Serialize(
            rotation.Select(entry => new[] { entry.Rule, entry.Map, entry.Flags }).ToArray());
    }

    /// <summary>
    /// Refuses a configuration this server cannot serve, before it serves any of
    /// it.
    /// </summary>
    /// <remarks>
    /// It is called once, at startup, because every fault below is one a client
    /// would otherwise discover: a rule or a map no client knows is a round it
    /// cannot play, and a time or a limit of zero is a round that ends as it opens.
    /// A named fault is refused with the setting's own environment variable in the
    /// message, so the fix is named and not searched for.
    /// </remarks>
    /// <param name="options">Options of this instance, which state the room.</param>
    /// <exception cref="InvalidOperationException">Thrown when the room cannot be served.</exception>
    public static void Validate(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The two names are parsed here and not at first use: this is the one
        // moment a fault stops a process rather than a round, and it precedes
        // every peer this host will ever answer.
        _ = Round(options);

        if (options.GameplayServerBriefingMinutes <= 0)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_BRIEFING_MINUTES must be at least a minute "
                + $"(got {options.GameplayServerBriefingMinutes}); a briefing of zero ends the moment it opens.");
        }

        if (options.GameplayServerRoundMinutes <= 0)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_ROUND_MINUTES must be at least a minute "
                + $"(got {options.GameplayServerRoundMinutes}); a round of zero ends the moment it opens.");
        }

        if (options.GameplayServerKillLimit is { } limit && limit <= 0)
        {
            throw new InvalidOperationException(
                $"GAMEPLAY_SERVER_KILL_LIMIT must be a positive number of kills (got {limit}), or unset for a "
                + "round that ends on its clock alone.");
        }
    }
}
