namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// One objective point of a stage, in the compact form the record carries it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The record is the stage's point divided by ten, with its height lifted.</b>
/// Measured against four captured records, each matched back to the effect it
/// names: <c>x</c> and <c>z</c> are the stage's own world coordinate ÷ 10, and
/// <c>y</c> is the stage's <c>y</c> + 900. Blood Bath's
/// <c>PRP_RUGBY_TGT_01 (9000, 0.83, −7062.5)</c> is the captured
/// <c>(900, 900, −706)</c>; its <c>PRP_RUGBY_TGT_02 (−7062.5, −6.26, 7750)</c> is
/// <c>(−706, 893, 775)</c>; Tomb of Tubes' <c>PRP_TGT_BOMB (−9000, 1000, −29000)</c>
/// is <c>(−900, 1900, −2900)</c>; and Blood Bath's <c>PRP_SCAP_TGT_00
/// (1000, −6000, 875)</c> is <c>(100, −5100, 87)</c>. The four are
/// <c>docs/analysis/custom-points.md</c> §2.
/// </para>
/// <para>
/// The division is what keeps a point in a signed sixteen: a stage such as Jade
/// Junction states <c>z = −245 477</c>, which no sixteen-bit field holds. The
/// lift is a constant of the wire, not of the stage — the stages measured put
/// their points at <c>y</c> −6.26 to −6000 and every record is that value plus
/// 900 — so it is written here once and not carried per point.
/// </para>
/// </remarks>
/// <param name="X">The stage's <c>x</c> in tenths.</param>
/// <param name="Y">The stage's <c>y</c> plus 900, in stage units.</param>
/// <param name="Z">The stage's <c>z</c> in tenths.</param>
public readonly record struct MapCustomPoint(short X, short Y, short Z);

/// <summary>
/// A round's points together with the task they are named under: what the run and
/// the briefing write from.
/// </summary>
/// <param name="Points">The round's points, in send order; empty for a rule with none.</param>
/// <param name="Task">The task byte, or <see cref="CustomPointUtils.NoTask"/> when there are none.</param>
public readonly record struct CustomPointSet(IReadOnlyList<MapCustomPoint> Points, byte Task)
{
    /// <summary>The number of points the round places.</summary>
    public int Count => Points.Count;

    /// <summary>The set a round's own rule and map give it.</summary>
    /// <param name="round">The round's rotation entry.</param>
    /// <returns>The round's points and task.</returns>
    public static CustomPointSet For(MatchRotationEntry round) =>
        new(CustomPointUtils.For(round.Map, round.Rule), CustomPointUtils.TaskOf(round.Rule));
}

/// <summary>
/// The objective points a round is given, by its rule: which of a stage's own
/// markers the room occupies, and the task byte the record names them under.
/// </summary>
/// <remarks>
/// <para>
/// <b>A rule without objectives is sent none, and that is the whole of the
/// gate.</b> A Team Deathmatch round has no target to place, and a host that sends
/// the recorded pair anyway puts KEROTAN and GA-KO in a mode that has neither:
/// the client reads kind <c>0x03</c> as an objective point and spawns the target
/// it names wherever the body says. The reference only ever writes them for a
/// rule whose script places targets, and the placement is the script's own branch
/// — <c>proc 50</c> of every stage sends Capture to <c>proc 51</c>, Rescue to
/// <c>proc 52</c>, Team Sneaking to <c>proc 53</c>, Bomb to <c>proc 59</c>,
/// Sneaking Capture to <c>proc 61</c> and Race to <c>proc 67</c>, and each of those
/// procs names the props below (<c>docs/analysis/gameplay-data-functions.md</c>
/// §4).
/// </para>
/// <para>
/// <b>The task byte is the record's first payload byte, <c>(task &lt;&lt; 4) |
/// index</c>.</b> Four captures measure it: Blood Bath under Capture carries
/// <c>0x00</c>/<c>0x01</c> with two points, Tomb of Tubes under Bomb
/// <c>0x40</c> with one, Blood Bath under Sneaking Capture <c>0x50</c> with one,
/// and Blood Bath under Race <c>0x60</c>/<c>0x61</c> with two. So the high nibble
/// is the task and the low nibble the point's index within it, and the ladder those
/// four sit on — Capture <c>0</c>, Bomb <c>4</c>, Sneaking Capture <c>5</c>, Race
/// <c>6</c> — is what puts Rescue on <c>1</c> and Team Sneaking on <c>2</c>.
/// **Rescue's and Team Sneaking's are inferred**, not measured: no capture holds a
/// record for either rule, and their points come from the procs that place them.
/// </para>
/// </remarks>
public static class CustomPointUtils
{
    /// <summary>Task byte of a Capture target; Interval places the same three props.</summary>
    private const byte CaptureTask = 0x00;

    /// <summary>Task byte of a Rescue target, inferred from the measured ladder.</summary>
    private const byte RescueTask = 0x01;

    /// <summary>Task byte of a Team Sneaking target, inferred from the measured ladder.</summary>
    private const byte TeamSneakingTask = 0x02;

    /// <summary>Task byte of a Bomb site, measured on <c>mgo2-game1</c>.</summary>
    private const byte BombTask = 0x04;

    /// <summary>Task byte of a Sneaking Capture target, measured on <c>mgo2-game2</c>.</summary>
    private const byte SneakingCaptureTask = 0x05;

    /// <summary>Task byte of a Race target, measured on <c>mgo2-game-failed-memoryviolation</c>.</summary>
    private const byte RaceTask = 0x06;

    /// <summary>The value <see cref="TaskOf"/> answers for a rule with no objectives.</summary>
    public const byte NoTask = 0xff;

    /// <summary>
    /// The points a round on a map is given, in send order: one per target the
    /// rule's own script proc places.
    /// </summary>
    /// <remarks>
    /// An empty list is a real answer — the rule has no objectives, or the map has
    /// no measured entry (the rotation's unnamed placeholder) — and the caller
    /// writes no record for it. The list is a map's own arrays and not a copy, so a
    /// caller must not modify it.
    /// </remarks>
    /// <param name="map">Map id from the round's rotation entry.</param>
    /// <param name="rule">Rule id from the round's rotation entry.</param>
    /// <returns>The round's points, or an empty list.</returns>
    public static IReadOnlyList<MapCustomPoint> For(int map, int rule)
    {
        if (!CustomPointTableUtils.TryFor(map, out var sets))
        {
            return [];
        }

        return rule switch
        {
            MatchRuleUtils.Capture or MatchRuleUtils.Interval => sets.Capture,
            MatchRuleUtils.Rescue => sets.Rescue,
            MatchRuleUtils.TeamSneaking => sets.TeamSneaking,
            MatchRuleUtils.Bomb => sets.Bomb,
            MatchRuleUtils.SneakingCapture => sets.SneakingCapture,
            MatchRuleUtils.Race => sets.Race,
            _ => [],
        };
    }

    /// <summary>
    /// The task byte a rule's points are named under, or <see cref="NoTask"/> when
    /// the rule has none.
    /// </summary>
    /// <param name="rule">Rule id from the round's rotation entry.</param>
    /// <returns>The task byte.</returns>
    public static byte TaskOf(int rule) => rule switch
    {
        MatchRuleUtils.Capture or MatchRuleUtils.Interval => CaptureTask,
        MatchRuleUtils.Rescue => RescueTask,
        MatchRuleUtils.TeamSneaking => TeamSneakingTask,
        MatchRuleUtils.Bomb => BombTask,
        MatchRuleUtils.SneakingCapture => SneakingCaptureTask,
        MatchRuleUtils.Race => RaceTask,
        _ => NoTask,
    };
}
