using Mgo2Server.Shared.Domain.Automatch;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// Policy for automatching: whether it runs, when it is open, how often the
/// matchmaker is ticked and how the queue is grouped.
/// <para>
/// None of this is protocol. The client sends a rule preference and then waits,
/// and every other decision — whether the feature is open at all, who is matched
/// with whom, how many players a group needs before it forms — is the operator's.
/// The one thing the game does fix is the vocabulary of refusal: it ships a
/// sentence for "currently not open" separate from "currently not available",
/// which is why a schedule is modelled here rather than a simple on/off.
/// </para>
/// </summary>
public sealed class AutomatchOptions
{
    /// <summary>Whether a search may be started at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours automatching is open, as <c>HH:mm-HH:mm</c> pairs separated by
    /// commas. Empty means open all day.
    /// <para>
    /// That reading is the opposite of the reference server's, where an empty
    /// schedule means never: it is the default here so that a deployment which
    /// never sets the variable keeps matching, rather than silently stopping the
    /// day this setting appears.
    /// </para>
    /// </summary>
    public string Windows { get; set; } = string.Empty;

    /// <summary>
    /// Zone the windows are expressed in. Named explicitly because a window with
    /// no zone, in a container that runs UTC, is a bug discovered live at the
    /// wrong hour.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Interval between two passes of the matchmaker.</summary>
    public int TickSeconds { get; set; } = 5;

    /// <summary>Players a group needs once the requirement has fully decayed.</summary>
    public int MinimumPlayers { get; set; } = 2;

    /// <summary>Players a group needs when it forms immediately.</summary>
    public int MinimumPlayersAtStart { get; set; } = 12;

    /// <summary>Seconds between one player coming off the requirement.</summary>
    public int MinimumPlayersStepSeconds { get; set; } = 30;

    /// <summary>Level half-width at the start of a search.</summary>
    public int BandAtStart { get; set; } = 1;

    /// <summary>Seconds between one level of widening.</summary>
    public int BandStepSeconds { get; set; } = 30;

    /// <summary>Maximum level half-width.</summary>
    public int BandMaximum { get; set; } = 22;

    /// <summary>Wait after which a searcher accepts any mode.</summary>
    public int ModeRelaxSeconds { get; set; } = 90;

    /// <summary>Interval the matchmaker is ticked at.</summary>
    public TimeSpan Tick => TimeSpan.FromSeconds(Math.Max(1, TickSeconds));

    /// <summary>Builds the matching policy from these settings.</summary>
    public AutomatchPolicy ToPolicy() => new(
        minimumPlayers: Math.Max(1, MinimumPlayers),
        minimumPlayersAtStart: Math.Max(1, MinimumPlayersAtStart),
        minimumPlayersStepMilliseconds: Math.Max(0, MinimumPlayersStepSeconds) * 1000,
        bandAtStart: Math.Max(0, BandAtStart),
        bandStepMilliseconds: Math.Max(0, BandStepSeconds) * 1000,
        bandMaximum: Math.Max(0, BandMaximum),
        modeRelaxMilliseconds: Math.Max(0, ModeRelaxSeconds) * 1000);

    /// <summary>Rejects a configuration that cannot be honoured.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the settings contradict each other.</exception>
    public void Validate()
    {
        if (BandAtStart > BandMaximum)
        {
            throw new InvalidOperationException(
                $"AUTOMATCH_BAND_START must not exceed AUTOMATCH_BAND_MAX ({BandAtStart} > {BandMaximum}).");
        }

        if (MinimumPlayersAtStart < MinimumPlayers)
        {
            throw new InvalidOperationException(
                $"AUTOMATCH_MIN_PLAYERS_START must not be below AUTOMATCH_MIN_PLAYERS " +
                $"({MinimumPlayersAtStart} < {MinimumPlayers}); a group that forms immediately can never " +
                "need fewer players than one that has waited.");
        }

        // Parsed here rather than at first use: a malformed schedule would
        // otherwise read as "closed", which is indistinguishable from a
        // deliberate window and is discovered by players rather than by us.
        AutomatchWindowUtils.Parse(Windows);
        AutomatchWindowUtils.ResolveZone(TimeZone);
    }
}
