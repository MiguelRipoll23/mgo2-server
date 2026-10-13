using Mgo2Server.GameplayServer.MatchRecords;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// One round of a room's rotation: the rule it plays, the map it plays on, the
/// triple's flags byte, and the round's own clock and kill limit.
/// </summary>
/// <remarks>
/// The first three are the room's three bytes as stored and sent, and the last two
/// are what the round is played for — its clock and, where it has one, the kill
/// limit it ends on (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2, §3). They are
/// resolved here rather than at the wire, because a round's end is read off them,
/// and a reader that has to look a limit up to know when a round is over is a
/// reader one step from getting it wrong.
/// </remarks>
/// <param name="Rule">Rule id of the triple, which <see cref="MatchRuleUtils"/> names.</param>
/// <param name="Map">Map id of the triple, which <see cref="MatchMapUtils"/> names.</param>
/// <param name="Flags">Flags byte of the triple, whose meaning is [U].</param>
/// <param name="TimeLimitMinutes">Minutes the round's clock runs for.</param>
/// <param name="Tickets">
/// The round's kill limit — the word the reply states a rule's time beside — and
/// <c>null</c> for a round that ends on its clock alone. Nothing on the gameplay
/// channel counts kills for a host, so a limit is only ever reached by the score
/// this host keeps itself.
/// </param>
public sealed record MatchRotationEntry(
    int Rule,
    int Map,
    int Flags,
    int TimeLimitMinutes,
    int? Tickets = null)
{
    /// <summary>
    /// The round's clock in the operations stream's own units: minutes × 480, the
    /// relation measured over three captures
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2).
    /// </summary>
    public int TimeLimitUnits => TimeLimitMinutes * MatchRecordBodiesUtils.ClockUnitsPerMinute;
}

/// <summary>
/// What the room this host serves is running, resolved from this instance's own
/// configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host is the room's host, so its room is described by its settings and by
/// no row.</b> The game id and name are this host's own identity — the row it
/// publishes under — and the rule, the map, the briefing time, each round's length
/// and its kill limit are this instance's configuration
/// (<c>MatchSettingsUtils</c>). Nothing of it is read back from the database: a
/// dedicated host never receives the client's <c>0x4312</c> push a player-hosted
/// room's settings come from, so a row read would only ever be the value this host
/// wrote there itself in the first place.
/// </para>
/// <para>
/// It is resolved per round rather than cached, so a round opens on the settings
/// of the process that is opening it and not on a copy taken before them.
/// </para>
/// </remarks>
/// <param name="GameIdentifier">Identifier of the room row this host publishes.</param>
/// <param name="Name">Display name of that room.</param>
/// <param name="BriefingTimeMinutes">Minutes the room's briefing lasts.</param>
/// <param name="Rotation">
/// The room's rounds, entry zero first. It is never empty: a host that states no
/// rotation is served its own one configured round, so every round a host opens
/// has a rule, a map and a length.
/// </param>
public sealed record MatchSettings(
    int GameIdentifier,
    string Name,
    int BriefingTimeMinutes,
    IReadOnlyList<MatchRotationEntry> Rotation)
{
    /// <summary>
    /// The entry a round plays: the rotation is walked in order, so the first
    /// round plays entry zero and a rotation that runs out starts again.
    /// </summary>
    /// <remarks>
    /// The walk is the round number rather than the room row's <c>current_game</c>
    /// because that column is the host's own selection in the client's room
    /// screen; the dedicated host never moves it, and a rotation that never
    /// advanced would play one rule for the life of the container. No capture
    /// joins a room early enough to see which of the two the client follows
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §6), so this follows the
    /// rotation the room published.
    /// </remarks>
    /// <param name="roundNumber">Round counting from one, as the room counts them.</param>
    /// <returns>The round's rule, map and length.</returns>
    public MatchRotationEntry Round(int roundNumber) =>
        Rotation[(Math.Max(roundNumber, 1) - 1) % Rotation.Count];
}
