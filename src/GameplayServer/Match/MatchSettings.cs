using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// One round of a room's rotation: the rule it plays, the map it plays on, the
/// triple's options byte, and the round's own clock and kill limit.
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
/// <param name="RoundOptions">
/// Options byte of the triple: the round's mode, which
/// <see cref="RoundOption"/> names (Drebin Points, Headshots Only, or neither).
/// </param>
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
    RoundOption RoundOptions,
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
    /// The walk is the round number rather than the room row's <c>current_game</c>:
    /// that column is what a joining client stages, and <see cref="MatchService"/>
    /// keeps it on the very entry this walk is about to play, so the two name one
    /// round instead of one of them being the rule. It is still the round number that
    /// is read here, because a column can be written by anything between two rounds
    /// and a round that played a rule its own clock was not set for is a round the
    /// client is not in (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §6).
    /// </remarks>
    /// <param name="roundNumber">Round counting from one, as the room counts them.</param>
    /// <returns>The round's rule, map and length.</returns>
    public MatchRotationEntry Round(int roundNumber) =>
        Rotation[(Math.Max(roundNumber, 1) - 1) % Rotation.Count];

    /// <summary>
    /// Whether the round a number names is the last its game plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A game is a run of identical entries.</b> The rotation is a per-round
    /// list, so a game played for several rounds appears as that many consecutive
    /// triples of one rule on one map: `mgo2-started-on-game-round1` states slots
    /// 8, 9 and 10 as `[6,1,0]` and its own round count as three
    /// (`docs/protocol/MATCH_FLOW.md` §2). The entry after this round naming
    /// another rule, map or flags is where the game ends, and that is where the
    /// room returns to a briefing — not at every round, which is what the capture
    /// shows: two whole five-minute rounds run back to back at t+601.4 and
    /// t+1238.2 with no briefing between them (§5).
    /// </para>
    /// <para>
    /// A rotation of distinct entries has every round as its game's last, which is
    /// the shape a room plays a rule once in: one briefing per round, unchanged.
    /// </para>
    /// <para>
    /// <b>The walk wraps; a game does not.</b> The last entry the rotation states
    /// ends its game even when it repeats the first one byte for byte, because the
    /// rounds after it are the next game and not this one: a rotation of one entry
    /// is a game of one round, and a rotation whose entries are all identical is a
    /// game of as many rounds as it states. Comparing with
    /// <see cref="Round"/>'s wrapped successor would read either shape as a game
    /// with no end.
    /// </para>
    /// </remarks>
    /// <param name="roundNumber">Round counting from one, as the room counts them.</param>
    /// <returns><c>true</c> when the round is its game's last.</returns>
    public bool IsLastRoundOfGame(int roundNumber)
    {
        if (Rotation.Count == 0)
        {
            return true;
        }

        var index = (Math.Max(roundNumber, 1) - 1) % Rotation.Count;
        if (index == Rotation.Count - 1)
        {
            return true;
        }

        var current = Rotation[index];
        var next = Rotation[index + 1];

        return current.Rule != next.Rule
            || current.Map != next.Map
            || current.RoundOptions != next.RoundOptions;
    }
}
