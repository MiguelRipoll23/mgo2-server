using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The live counters the client's score table reads, one field for each column
/// this host can observe.
/// </summary>
/// <remarks>
/// <b>The names are the client's table's, not a second score model.</b> The
/// client's <c>ComputeScore</c> (<c>0x6FA408</c>) walks a 37-column row of
/// <c>s8</c> coefficients over these counters; the rows are filled from the stage
/// script's own <c>-score</c> directives and are identical across stages
/// (<c>docs/PROTOCOL.md</c>, "SETTLED 2026-07-27"). A counter the host does not
/// have contributes nothing, which is what the table states for a player with
/// none of it.
/// </remarks>
/// <param name="Kills">Kills credited to the player: column 0.</param>
/// <param name="Deaths">Deaths recorded for the player: column 1.</param>
/// <param name="TeamWins">Rounds the player's side has won: column 5.</param>
/// <param name="KnockoutsDealt">Knock-outs the player landed: column 2.</param>
/// <param name="KnockoutsReceived">Knock-outs the player took: column 3.</param>
/// <param name="Headshots">Headshot kills: column 4.</param>
/// <param name="Combo">The player's combo total: column 8.</param>
/// <param name="BodySearches">Body searches that yielded an item: column 28.</param>
/// <param name="DogTags">Dog tags collected: column 29.</param>
/// <param name="HoldUps">CQC hold-ups performed: column 35.</param>
/// <param name="Checkpoints">
/// Checkpoints passed in a checkpoint rule. The documented rows do not carry a
/// column for it — SCAP's full 37-column row is not among the seven the stage
/// scripts state — so the counter is kept and contributes nothing yet.
/// </param>
public readonly record struct MatchScoreCounters(
    int Kills = 0,
    int Deaths = 0,
    int TeamWins = 0,
    int KnockoutsDealt = 0,
    int KnockoutsReceived = 0,
    int Headshots = 0,
    int Combo = 0,
    int BodySearches = 0,
    int DogTags = 0,
    int HoldUps = 0,
    int Checkpoints = 0);

/// <summary>
/// The client's 37-column coefficient table, for the rules the stage scripts
/// state it for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rows are the stage script's own, except where a captured card
/// corrects them.</b> They are the <c>-rule N -score &lt;37 ints&gt;</c> rows of
/// <c>proc23</c> in <c>n002a</c>/<c>n003a</c>/<c>n004a</c>, read off the disc,
/// and the table is not static in the ELF: its base is filled at runtime by the
/// GCX native command at <c>0x6FA1B8</c>. The client's <c>ComputeScore</c>
/// multiplies each column by the counter it maps to and clamps the total to
/// <c>[0, 65535]</c> (<c>docs/PROTOCOL.md</c>;
/// <c>docs/proto/inbound/mgo2_cmd_4390_c2s.ksy</c>). CAP's columns 0 and 4 are
/// the measured card's rather than the script's: <c>replay_396276_13.dat</c>
/// (match 72518) pays 3 per kill, headshot kills included, where the script
/// states 5 and 3.
/// </para>
/// <para>
/// <b>Only the columns this host can observe are read.</b> The rest of the row
/// is kept in full so the module is the table rather than a second reading of
/// it, but a column with no counter contributes nothing. The mapped counters
/// are kills, deaths, team wins, knock-outs dealt and received, headshots,
/// combo, checkpoints, body searches, dog tags and hold-ups. Team kills, wakes,
/// assists, the other per-mode objective counters and the off-wire <c>OTHER</c>
/// column are not counted by this host yet.
/// </para>
/// </remarks>
public static class MatchScoreCounterUtils
{
    /// <summary>Column of the kills count.</summary>
    private const int KillsColumn = 0;

    /// <summary>Column of the deaths count.</summary>
    private const int DeathsColumn = 1;

    /// <summary>Column of the team-win flag: n15, worth 5 in CAP, RES, SNE, BASE and TSNE.</summary>
    private const int TeamWinsColumn = 5;

    /// <summary>Column of the knock-outs dealt.</summary>
    private const int KnockoutsDealtColumn = 2;

    /// <summary>Column of the knock-outs received.</summary>
    private const int KnockoutsReceivedColumn = 3;

    /// <summary>Column of the headshot count.</summary>
    private const int HeadshotsColumn = 4;

    /// <summary>Column of the combo total.</summary>
    private const int ComboColumn = 8;

    /// <summary>Column of the body searches.</summary>
    private const int BodySearchesColumn = 28;

    /// <summary>Column of the objective deliveries: B46 capture_put_count, which CAP pays at 1.</summary>
    private const int CheckpointsColumn = 27;

    /// <summary>Column of the dog tags collected.</summary>
    private const int DogTagsColumn = 29;

    /// <summary>Column of the CQC hold-ups.</summary>
    private const int HoldUpsColumn = 35;

    // The seven rows the stage scripts state, 37 s8 columns each. Their order is
    // the table's own and is not rearranged: a reader checking one against the
    // disc should be able to read it left to right.
    private static readonly int[] Deathmatch =
    [
        3, -2, 3, -2, 2, 0, 0, 0, 0, 5, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] TeamDeathmatch =
    [
        3, -2, 2, -1, 2, 0, 0, 5, 2, 5, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] Rescue =
    [
        7, 0, 7, 0, 3, 5, -5, 0, 0, 0, 5, 0, 0, 0, 0, 0, 3, 3, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3,
    ];

    // The CAP row's kill and headshot cells are the captured scoreboard's, not
    // the stage script's: `replay_396276_13.dat` (match 72518) pays 3 for every
    // kill, headshot kills included, and 5 for a round win. The script states 5
    // and 3 in columns 0 and 4; the measured card is what the wire score is
    // read against, and `docs/protocol/RULE_WIRE_DATA.md` records the change.
    private static readonly int[] Capture =
    [
        3, 0, 5, 0, 0, 5, 0, 3, 5, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3,
    ];

    private static readonly int[] Sneaking =
    [
        3, -2, 2, -1, 2, 5, 0, 5, 2, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 5, 5, 3, 0, 6, 4, 2, 0,
    ];

    private static readonly int[] Base =
    [
        3, 0, 3, 0, 0, 5, -5, 0, 3, 0, 3, 0, 0, 1, 5, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] TeamSneaking =
    [
        5, 0, 5, 0, 0, 5, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 5, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5,
    ];

    /// <summary>Whether the rule has a row in the client's table.</summary>
    /// <param name="rule">Rule id from the round's rotation.</param>
    /// <returns><c>true</c> when the table states the rule's row.</returns>
    public static bool HasRow(int rule) => RowOf(rule) is not null;

    /// <summary>
    /// Computes a player's score from the counters this host holds, in the
    /// client's own table.
    /// </summary>
    /// <param name="rule">Rule id from the round's rotation.</param>
    /// <param name="counters">The player's live counters.</param>
    /// <returns>The clamped total, or zero when the table has no row for the rule.</returns>
    public static int Score(int rule, MatchScoreCounters counters)
    {
        if (RowOf(rule) is not { } row)
        {
            return 0;
        }

        var total =
            (long)row[KillsColumn] * counters.Kills
            +            (long)row[DeathsColumn] * counters.Deaths
            + (long)row[TeamWinsColumn] * counters.TeamWins
            + (long)row[KnockoutsDealtColumn] * counters.KnockoutsDealt
            + (long)row[KnockoutsReceivedColumn] * counters.KnockoutsReceived
            + (long)row[HeadshotsColumn] * counters.Headshots
            + (long)row[ComboColumn] * counters.Combo
            + (long)row[CheckpointsColumn] * counters.Checkpoints
            + (long)row[BodySearchesColumn] * counters.BodySearches
            + (long)row[DogTagsColumn] * counters.DogTags
            + (long)row[HoldUpsColumn] * counters.HoldUps;

        return (int)Math.Clamp(total, 0, MatchScoreRulesUtils.ScoreCeiling);
    }

    /// <summary>
    /// The row a rule plays, or <c>null</c> when the stage scripts state none.
    /// </summary>
    /// <param name="rule">Rule id from the round's rotation.</param>
    /// <returns>The rule's 37 coefficients, or <c>null</c>.</returns>
    private static int[]? RowOf(int rule) => rule switch
    {
        MatchRuleUtils.Deathmatch => Deathmatch,
        MatchRuleUtils.TeamDeathmatch => TeamDeathmatch,
        MatchRuleUtils.Rescue => Rescue,
        MatchRuleUtils.Capture => Capture,
        MatchRuleUtils.Sneaking => Sneaking,
        MatchRuleUtils.Base => Base,
        MatchRuleUtils.TeamSneaking => TeamSneaking,
        _ => null,
    };
}
