using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// Resolves what this host's room is running, from this instance's own settings.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room is asked for here rather than by each caller.</b> "Which room is
/// mine" has one answer and it lives on <see cref="MatchService"/>: a host that has
/// not published its room yet has no round to open and no rotation to write into a
/// joiner's settings burst, and every writer of the channel asks this before it
/// writes. Nothing is read from the database to answer it — the settings are this
/// instance's own (<see cref="MatchSettingsUtils"/>) — so the gate is the published
/// room and not a query, and a caller pays no round trip to learn what it already
/// knows.
/// </para>
/// <para>
/// A room that cannot be resolved is answered with <c>null</c> and never with a
/// default: a host that opened a round on a guess would play a rule or a map the
/// client was not told about, which is worse than a round that does not open.
/// </para>
/// </remarks>
/// <param name="match">Publisher of this host's room, which names the room to serve.</param>
/// <param name="roundState">The room's round, which names the map the room stands on.</param>
/// <param name="roster">The room's roster, which says whether the room holds one player.</param>
/// <param name="options">Options of this instance, which state what the room runs.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchSettingsService(
    MatchService match,
    MatchRecords.MatchRoundStateService roundState,
    Rooms.RoomRosterService roster,
    IOptions<ServerOptions> options,
    ILogger<MatchSettingsService> logger)
{
    private readonly ServerOptions options = options.Value;

    /// <summary>
    /// The rotation this instance's room plays, resolved once per process.
    /// </summary>
    private readonly IReadOnlyList<MatchRotationEntry> rotation = MatchSettingsUtils.Rotation(options.Value);

    private readonly IReadOnlyList<int> ruleTimers = MatchSettingsUtils.RuleTimers(options.Value);

    /// <summary>The room's live SNE dog-tag count, which a round can raise.</summary>
    private byte sneakingDogTags =
        (byte)Math.Clamp(options.Value.GameplayServerSneakingDogTags, 0, byte.MaxValue);

    /// <summary>The room's live checkpoint count, which a round can raise.</summary>
    private byte checkpoints =
        (byte)Math.Clamp(options.Value.GameplayServerCheckpoints, 0, byte.MaxValue);

    /// <summary>
    /// The generation a room that has opened no round writes at body <c>0x9d</c>:
    /// <c>1</c>.
    /// </summary>
    public const byte SettingsGenerationSeed = 1;

    /// <summary>
    /// The settings generation this room writes at body <c>0x9d</c>: the seed plus the
    /// rounds the room has opened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The seed is one and the step is the round.</b> A room that has opened no round
    /// — the briefing of a fresh room — writes <see cref="SettingsGenerationSeed"/>, and
    /// each round it opens takes the next value. The increment point is
    /// <see cref="MatchRecords.MatchRoundStateService.Open"/>, where
    /// <see cref="MatchRecords.MatchRoundStateService.RoundNumber"/> moves; an emptied
    /// room resets that count to zero and so returns here to the seed.
    /// </para>
    /// <para>
    /// <b>It is a byte and it wraps.</b> Round <c>255</c> writes <c>0</c> and the next
    /// writes <c>1</c> again. The client adopts the value and bumps its own copy on each
    /// applied <c>0xa1</c> record (<c>0x26dd60</c>), and the role-pair draw at
    /// <c>0x77b628</c> indexes with it, so what matters is that a new round is a new
    /// value, not that the value never repeats.
    /// </para>
    /// </remarks>
    public byte SettingsGeneration => unchecked((byte)(roundState.RoundNumber + SettingsGenerationSeed));

    /// <summary>The round number, carried beside the phase as the room's own count.</summary>
    public byte CurrentRoundNumber => unchecked((byte)roundState.RoundNumber);

    /// <summary>
    /// The state the room states about itself at body offset <c>0x09</c> and in
    /// state <c>0x00</c>'s field 0: <see cref="RoomState.Briefing"/> while the room
    /// is briefed, and the round's own start once one plays —
    /// <see cref="RoomState.RoundStartSolo"/> for the one player and no bots the
    /// roster says the room holds, <see cref="RoomState.RoundStart"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <b>It is a state and not the round counter.</b> Every briefing in
    /// `docs/protocol/MATCH_FLOW.md` §1 states <c>2</c> and a room whose round is
    /// playing states its start; a joiner sent the first round's number instead is
    /// told a phase no capture holds: its client stops waiting to be briefed,
    /// builds the battle HUD, and never writes the <c>1d</c> rows a briefing is
    /// answered with. The values, and the flow they are written in, are
    /// <see cref="RoomState"/>'s.
    /// </remarks>
    public Rooms.RoomState CurrentRoomState => roundState.State(roster.IsSolo);

    /// <summary>
    /// The rule the room is on: the one the round playing uses, or the one the
    /// briefing precedes when no round is open.
    /// </summary>
    public int CurrentRule => rotation[ActiveRotationIndex].Rule;

    /// <summary>The active rotation slot carried by the room's state transfer.</summary>
    public int CurrentRotationIndex => ActiveRotationIndex;

    /// <summary>
    /// The entry the room is on now: the playing round's while one is open and
    /// the upcoming round's while a briefing runs.
    /// </summary>
    /// <remarks>
    /// <see cref="MatchRecords.MatchRoundStateService.RoundNumber"/> counts the
    /// rounds opened, counting the playing one, so the current entry is one less
    /// than that count. A stage end is still the played round's entry while its
    /// screens run — the next briefing has not begun and its map is not staged
    /// yet — and a briefing with no screens names the next entry, which is the
    /// count itself. Reading the count in both phases put the settings burst and
    /// the room's spawn records on different maps.
    /// </remarks>
    private int ActiveRotationIndex =>
        rotation.Count == 0
            ? 0
            : roundState.IsOpen || roundState.IsStageEnding
                ? (Math.Max(roundState.RoundNumber, 1) - 1) % rotation.Count
                : roundState.RoundNumber % rotation.Count;

    /// <summary>The room's configured seventeen-word rule timer bank.</summary>
    public IReadOnlyList<int> RuleTimers => ruleTimers;

    /// <summary>
    /// The room's current SNE dog-tag count, body <c>0x8e</c>: the number Snake must
    /// collect. It starts at <see cref="ServerOptions.GameplayServerSneakingDogTags"/>;
    /// a gameplay change raises it with <see cref="IncreaseSneakingDogTags"/> and the
    /// next body built carries the new value.
    /// </summary>
    public byte SneakingDogTags => Volatile.Read(ref sneakingDogTags);

    /// <summary>Sets the room's SNE dog-tag count.</summary>
    /// <param name="count">The new count, written to body <c>0x8e</c>.</param>
    public void SetSneakingDogTags(byte count) => Volatile.Write(ref sneakingDogTags, count);

    /// <summary>Raises the room's SNE dog-tag count, clamped to the body's byte.</summary>
    /// <param name="amount">How many tags to add.</param>
    public void IncreaseSneakingDogTags(byte amount = 1) =>
        SetSneakingDogTags((byte)Math.Clamp(SneakingDogTags + amount, 0, byte.MaxValue));

    /// <summary>
    /// The room's current checkpoint count, body <c>0x95</c>: the number a checkpoint
    /// rule requires. It starts at <see cref="ServerOptions.GameplayServerCheckpoints"/>;
    /// a gameplay change raises it with <see cref="IncreaseCheckpoints"/> and the next
    /// body built carries the new value.
    /// </summary>
    public byte Checkpoints => Volatile.Read(ref checkpoints);

    /// <summary>Sets the room's checkpoint count.</summary>
    /// <param name="count">The new count, written to body <c>0x95</c>.</param>
    public void SetCheckpoints(byte count) => Volatile.Write(ref checkpoints, count);

    /// <summary>Raises the room's checkpoint count, clamped to the body's byte.</summary>
    /// <param name="amount">How many checkpoints to add.</param>
    public void IncreaseCheckpoints(byte amount = 1) =>
        SetCheckpoints((byte)Math.Clamp(Checkpoints + amount, 0, byte.MaxValue));

    /// <summary>
    /// Whether the room assigns the unique characters — Snake and Metal Gear Mk.II —
    /// at the start of a Sneaking or Team Sneaking round.
    /// </summary>
    public bool UniqueCharacters => options.GameplayServerUniqueCharacters;

    /// <summary>
    /// Whether the room puts joiners on a side itself. Clear is the room whose
    /// client shows the Red / Blue / Auto selection screen, and whose players'
    /// choices the lobby stores.
    /// </summary>
    public bool AutoAssignTeams => options.GameplayServerAutoAssignTeams;

    /// <summary>
    /// The room's level restriction: the band the client tests a joining player's level
    /// against, and whether it is in force.
    /// </summary>
    /// <remarks>
    /// Both bytes are written whether or not the limit is in force — the client keeps
    /// the chosen band while the option is off — and only the enable bit at
    /// <c>0x400000</c> says whether a joining client should apply it. The band is
    /// clamped to a byte because that is the wire's width, not because a level that
    /// large is playable.
    /// </remarks>
    public Rooms.LevelLimitBand LevelLimit => new(
        (byte)Math.Clamp(options.GameplayServerLevelLimitTolerance, 0, byte.MaxValue),
        (byte)Math.Clamp(options.GameplayServerLevelLimitBase, 0, byte.MaxValue),
        options.GameplayServerLevelLimitEnabled);

    /// <summary>
    /// The map the room's own records are written on: the playing round's while one
    /// is open and the upcoming round's while a briefing runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A round's records take the map of the round itself
    /// (<see cref="MatchRotationEntry.Map"/> from <see cref="MatchSettings.Round"/>);
    /// this answers for the room's own stage, which is what the briefing and the
    /// lobby-slot pair are drawn on before a round exists.
    /// </para>
    /// <para>
    /// <b>It is the round in front of the room and not entry zero.</b> A room staged
    /// mid-rotation briefs on the map it is about to play — the recording whose
    /// current round is <c>CAP@Blood Bath</c> writes its briefing on Blood Bath — so
    /// a stage read off entry zero would draw the briefing on a map the round that
    /// follows it is not played on. It is validated at startup
    /// (<see cref="MatchSettingsUtils.Validate"/>), so it is known before the room
    /// is published and does not wait on <see cref="FindPublished"/>.
    /// </para>
    /// </remarks>
    public int Map => rotation[ActiveRotationIndex].Map;

    /// <summary>
    /// The settings of the room this host publishes, which is the room every writer
    /// of this host's channel is answering for.
    /// </summary>
    /// <returns>The room's settings, or <c>null</c> while it has none published.</returns>
    /// <summary>
    /// Whether the round a number names is the last its game plays, read off the
    /// rotation this instance plays.
    /// </summary>
    /// <remarks>
    /// It resolves no room: the rotation is this instance's own and is known before
    /// the room is published, and a round end that waited on the row would leave a
    /// game's boundary undecided while the lobby is unreachable. The rule is
    /// <see cref="MatchSettings.IsLastRoundOfGame"/>'s, over the same list, and its
    /// last entry ends its game instead of wrapping onto the first.
    /// </remarks>
    /// <param name="roundNumber">Round counting from one, as the room counts them.</param>
    /// <returns><c>true</c> when the round is its game's last.</returns>
    public bool IsLastRoundOfGame(int roundNumber)
    {
        if (rotation.Count == 0)
        {
            return true;
        }

        var index = (Math.Max(roundNumber, 1) - 1) % rotation.Count;
        if (index == rotation.Count - 1)
        {
            return true;
        }

        var current = rotation[index];
        var next = rotation[index + 1];

        return current.Rule != next.Rule
            || current.Map != next.Map
            || current.RoundOptions != next.RoundOptions;
    }

    /// <summary>
    /// Writes the rotation entry the briefing after a stage end plays into the
    /// room's row, so a joiner stages the map and rule that briefing is on.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public Task PublishNextGameAsync(CancellationToken cancellationToken = default) =>
        match.PublishNextGameAsync(cancellationToken);

    public MatchSettings? FindPublished()
    {
        var matchIdentifier = match.MatchIdentifier;
        if (matchIdentifier == 0)
        {
            logger.LogWarning(
                "This host has not published its room yet, so what its room runs is not known");
            return null;
        }

        var settings = new MatchSettings(
            matchIdentifier,
            options.GameplayServerRoomName,
            options.GameplayServerBriefingMinutes,
            rotation,
            options.GameplayServerAutoAssignTeams,
            SneakingDogTags,
            Checkpoints,
            options.GameplayServerUniqueCharacters);
        logger.LogDebug(
            "Room {GameIdentifier} ({GameName}) runs {RoundCount} round(s) on maps {Maps} with a "
            + "{BriefingTime}-minute briefing, from this instance's own settings",
            settings.GameIdentifier,
            settings.Name,
            settings.Rotation.Count,
            string.Join(", ", rotation.Select(entry => MatchMapUtils.Name(entry.Map) ?? $"map {entry.Map}")),
            settings.BriefingTimeMinutes);
        return settings;
    }
}
