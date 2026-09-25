using System.Globalization;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// Operator configuration of the event subsystem: what the Survival and
/// Tournament information screens advertise, and the settings an assigned game
/// starts from.
/// <para>
/// None of it is protocol. The client is told a title, a description, a daily
/// window, a reward table and a handful of rule bytes; every value here is the
/// operator's choice, so it is configuration rather than a literal. The one
/// thing the client fixes is the shape: ten win-reward words, one participation
/// word, and a selector that says which event the record describes.
/// </para>
/// </summary>
public sealed class EventOptions
{
    /// <summary>Number of win-reward slots the information record carries.</summary>
    public const int WinRewardSlots = 10;

    /// <summary>Title shown on the event information screen.</summary>
    public string Title { get; set; } = "SURVIVAL & TOURNAMENT";

    /// <summary>Description shown on the event information screen.</summary>
    public string Description { get; set; } =
        "Form a team, then wait until an opponent is found. This event is under development.";

    /// <summary>
    /// Zone the daily window is expressed in. Named explicitly because a window
    /// with no zone, in a container that runs UTC, is a bug discovered live at
    /// the wrong hour.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Minute of the day the window opens, counted from local midnight. The
    /// record carries whole epoch seconds, derived from this so a deployment
    /// states the window it means rather than a timestamp.
    /// </summary>
    public int StartMinute { get; set; }

    /// <summary>Minute of the day the window closes, counted from local midnight.</summary>
    public int EndMinute { get; set; } = 1440;

    /// <summary>Reward paid to a team for entering, win or lose.</summary>
    public int ParticipationReward { get; set; }

    /// <summary>
    /// Rewards paid to a team for its first through tenth consecutive wins, as a
    /// comma-separated list. Missing entries are zero, so a deployment states
    /// only the streak it pays for.
    /// </summary>
    public string WinRewards { get; set; } = "100,200,300,400,500";

    /// <summary>
    /// Rule byte the client copies into its information common-rule field. Bits
    /// 0..6 expose the seven rule rows of the retail screen.
    /// </summary>
    public int InformationRuleFlags { get; set; } = 0x7f;

    /// <summary>Number of consecutive-win rows the information screen draws.</summary>
    public int DisplayedWinCount { get; set; } = 5;

    /// <summary>
    /// Maximum number of teams a Tournament bracket may hold. The default is the
    /// largest field the client can draw, because the validation below refuses
    /// anything above it: a larger default would stop the server at startup with
    /// a capacity nobody asked for.
    /// </summary>
    public int TournamentCapacity { get; set; } = Domain.Events.EventConstants.BracketMaximumEntrants;

    /// <summary>Prize labels shown by the Tournament screen, comma-separated.</summary>
    public string PrizeLabels { get; set; } = string.Empty;

    /// <summary>
    /// Lowest character level a Tournament reservation accepts, or zero for no
    /// lower bound. Applied when the reservation is taken, because that is the
    /// first moment the server sees the character rather than the team.
    /// </summary>
    public int TournamentMinimumLevel { get; set; }

    /// <summary>Highest character level a Tournament reservation accepts, or zero for no upper bound.</summary>
    public int TournamentMaximumLevel { get; set; }

    /// <summary>
    /// How long the players' reports are given to settle before the outcome is
    /// decided from them. The delay exists because one kind of event host also
    /// reports the match terminal itself, and deciding from the statistics any
    /// sooner would race that report.
    /// </summary>
    public int ReportGraceMilliseconds { get; set; } = 1000;

    /// <summary>
    /// How often the lobby looks for matches whose reports have settled. It is
    /// the partner of the grace above: the grace says when a match may be
    /// decided, and this says how soon after that anyone looks.
    /// </summary>
    public int OutcomeSweepMilliseconds { get; set; } = 1000;

    /// <summary>
    /// The ten win rewards, in streak order, with any slot the configuration
    /// omitted filled with zero.
    /// </summary>
    /// <returns>The fixed-length reward table.</returns>
    public int[] WinRewardTable()
    {
        var table = new int[WinRewardSlots];
        if (string.IsNullOrWhiteSpace(WinRewards))
        {
            return table;
        }

        var parts = WinRewards.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < Math.Min(parts.Length, WinRewardSlots); index++)
        {
            table[index] = int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0, value)
                : 0;
        }

        return table;
    }

    /// <summary>
    /// The configured zone, or UTC when the name does not resolve. Parsed here
    /// rather than at first use so a bad name cannot silently shift a schedule.
    /// </summary>
    /// <returns>The resolved zone.</returns>
    public TimeZoneInfo ResolveZone() =>
        string.IsNullOrWhiteSpace(TimeZone)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

    /// <summary>Rejects a configuration that cannot be honoured.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the settings contradict each other.</exception>
    public void Validate()
    {
        if (StartMinute < 0 || StartMinute > 1439)
        {
            throw new InvalidOperationException(
                $"EVENT_SCHEDULE_START_MINUTE must be 0..1439 (got {StartMinute}).");
        }

        if (EndMinute < 1 || EndMinute > 1440)
        {
            throw new InvalidOperationException(
                $"EVENT_SCHEDULE_END_MINUTE must be 1..1440 (got {EndMinute}).");
        }

        if (InformationRuleFlags < 0 || InformationRuleFlags > 0xff)
        {
            throw new InvalidOperationException(
                $"EVENT_INFORMATION_RULE_FLAGS must be 0..255 (got {InformationRuleFlags}).");
        }

        if (DisplayedWinCount < 0 || DisplayedWinCount > WinRewardSlots)
        {
            throw new InvalidOperationException(
                $"EVENT_DISPLAYED_WIN_COUNT must be 0..{WinRewardSlots} (got {DisplayedWinCount}).");
        }

        if (ParticipationReward < 0)
        {
            throw new InvalidOperationException(
                $"EVENT_PARTICIPATION_REWARD must not be negative (got {ParticipationReward}).");
        }

        if (TournamentCapacity < 2 || TournamentCapacity > Domain.Events.EventConstants.BracketMaximumEntrants)
        {
            // The ceiling is the client's bracket, not a server limit: a larger
            // field would be seeded into rows the screen cannot draw.
            throw new InvalidOperationException(
                $"TOURNAMENT_CAPACITY must be 2..{Domain.Events.EventConstants.BracketMaximumEntrants} "
                + $"(got {TournamentCapacity}).");
        }

        if (TournamentMinimumLevel < 0 || TournamentMaximumLevel < 0)
        {
            throw new InvalidOperationException(
                "TOURNAMENT_MINIMUM_LEVEL and TOURNAMENT_MAXIMUM_LEVEL must not be negative.");
        }

        if (ReportGraceMilliseconds < 0 || ReportGraceMilliseconds > 60_000)
        {
            throw new InvalidOperationException(
                $"EVENT_REPORT_GRACE_MILLISECONDS must be 0..60000 (got {ReportGraceMilliseconds}).");
        }

        if (OutcomeSweepMilliseconds < 100 || OutcomeSweepMilliseconds > 60_000)
        {
            throw new InvalidOperationException(
                $"EVENT_OUTCOME_SWEEP_MILLISECONDS must be 100..60000 (got {OutcomeSweepMilliseconds}).");
        }

        if (TournamentMaximumLevel != 0 && TournamentMaximumLevel < TournamentMinimumLevel)
        {
            throw new InvalidOperationException(
                $"TOURNAMENT_MAXIMUM_LEVEL ({TournamentMaximumLevel}) is below "
                + $"TOURNAMENT_MINIMUM_LEVEL ({TournamentMinimumLevel}); no character could enter.");
        }

        // Parsed here rather than at first use: a malformed zone would otherwise
        // shift the advertised window without any error being reported.
        ResolveZone();
        WinRewardTable();
    }
}
