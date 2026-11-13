using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The room's rotation and its per-rule bank, read from the two settings that
/// state them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A rotation is what a room plays, so it is parsed once and read by every
/// caller.</b> The setting is written the way the client's own room screen writes
/// it — one <c>RULE@Map</c> pair per round, with an optional <c>:DREBIN</c> suffix
/// for a round played for Drebin Points — and this file is where a pair becomes
/// the ids and the option byte a rotation triple carries, through
/// <see cref="MatchRuleUtils"/> and <see cref="MatchMapUtils"/> rather than a
/// second spelling of either table.
/// </para>
/// <para>
/// <b>The bank is where a round's length and kill limit come from.</b> The
/// seventeen words are the order the lobby's game-details reply states them in
/// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2), so a round's rule names its own
/// word: a rotation that mixes rules plays each for its own time, which is what
/// the recorded rooms do. A rule the bank has no pair for — SDM, INTERVAL, SCAP
/// and RACE, whose times live in a bank nothing has read yet — runs the bank's
/// first time and ends on its clock alone.
/// </para>
/// <para>
/// A name that is no rule or no map is refused rather than mapped onto a nearby
/// id, and so is a bank whose words do not fit the reply: a room running a round
/// no client was told about is a room the client is not playing.
/// </para>
/// </remarks>
public static class MatchRotationUtils
{
    /// <summary>Separates one round's pair from the next.</summary>
    private const char RoundSeparator = ',';

    /// <summary>Separates a round's rule name from its map name.</summary>
    private const char PairSeparator = '@';

    /// <summary>Separates a round's map name from its optional option suffix.</summary>
    private const char OptionSeparator = ':';

    /// <summary>Separates two of the bank's words.</summary>
    private const char WordSeparator = ',';

    /// <summary>Words the bank carries, in the reply's own order.</summary>
    public const int RuleTimerCount = 17;

    /// <summary>Index of a rule's time within the bank, or <c>-1</c> when it has none.</summary>
    /// <remarks>
    /// The order is the reply's: SNE, CAP, RES, TDM, DM, BASE, BOMB, TSNE. TDM and
    /// DM each state a pair of times and rounds and a third word of tickets, which
    /// is why the two ticket words sit at <see cref="TdmTicketsIndex"/> and
    /// <see cref="DmTicketsIndex"/> rather than behind a word of their own.
    /// </remarks>
    /// <param name="rule">Rule id from a rotation triple.</param>
    /// <returns>The index of the rule's time, or -1 when the bank states none.</returns>
    public static int TimeIndex(int rule) => rule switch
    {
        MatchRuleUtils.Sneaking => 0,
        MatchRuleUtils.Capture => 2,
        MatchRuleUtils.Rescue => 4,
        MatchRuleUtils.TeamDeathmatch => 6,
        MatchRuleUtils.Deathmatch => 9,
        MatchRuleUtils.Base => 11,
        MatchRuleUtils.Bomb => 13,
        MatchRuleUtils.TeamSneaking => 15,
        _ => -1,
    };

    /// <summary>Index of team deathmatch's ticket count within the bank.</summary>
    public const int TdmTicketsIndex = 8;

    /// <summary>Index of deathmatch's ticket count within the bank.</summary>
    public const int DmTicketsIndex = 10;

    /// <summary>
    /// The rotation a setting states, with each round's length and kill limit
    /// resolved from the bank.
    /// </summary>
    /// <param name="rotation">
    /// The rounds, as <c>RULE@Map</c> pairs separated by commas, each optionally
    /// suffixed <c>:DREBIN</c>.
    /// </param>
    /// <param name="ruleTimers">The seventeen per-rule words, resolved already.</param>
    /// <returns>The rotation, entry zero first.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the setting names no round, when a name is no rule or no map,
    /// when an option is not <c>DREBIN</c>, or when a round would play for no time
    /// at all.
    /// </exception>
    public static IReadOnlyList<MatchRotationEntry> Parse(
        string? rotation,
        IReadOnlyList<int> ruleTimers)
    {
        ArgumentNullException.ThrowIfNull(ruleTimers);

        var entries = new List<MatchRotationEntry>();
        foreach (var round in (rotation ?? string.Empty).Split(
            RoundSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = round.LastIndexOf(PairSeparator);
            if (separator <= 0 || separator == round.Length - 1)
            {
                throw new InvalidOperationException(
                    $"'{round}' is not a round of a rotation; each one is a rule and a map joined by "
                    + $"'{PairSeparator}', as in TDM@{MatchMapUtils.Name(MatchMapUtils.Parse("Blood Bath"))}.");
            }

            // The option is a suffix after the map, so the last ':' is the one that
            // separates it. A map name has no ':' of its own.
            var optionSeparator = round.LastIndexOf(OptionSeparator);
            var option = RoundOption.Normal;
            var mapEnd = round.Length;
            if (optionSeparator > separator)
            {
                option = ParseRoundOption(round[(optionSeparator + 1)..], round);
                mapEnd = optionSeparator;
            }

            var rule = MatchRuleUtils.Parse(round[..separator]);
            var map = MatchMapUtils.Parse(round[(separator + 1)..mapEnd]);
            var minutes = TimeLimitMinutes(ruleTimers, rule);

            if (minutes <= 0)
            {
                throw new InvalidOperationException(
                    $"The round {MatchRuleUtils.Name(rule)}@{MatchMapUtils.Name(map)} would play for no "
                    + "time at all; GAMEPLAY_SERVER_RULE_TIMERS must state a positive time for its rule.");
            }

            entries.Add(new MatchRotationEntry(rule, map, option, minutes, Tickets(ruleTimers, rule)));
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SERVER_ROTATION names no round; a room with no rotation has nothing to play.");
        }

        return entries;
    }

    /// <summary>
    /// The round option a suffix states. Only <c>DREBIN</c> is a setting this
    /// server can write: it is the measured Drebin Points value of the triple's
    /// third byte, and the only option the room's configuration names.
    /// </summary>
    /// <param name="option">Text after the map's colon.</param>
    /// <param name="round">The whole round, for the refusal to name.</param>
    /// <returns>The option the suffix states.</returns>
    /// <exception cref="InvalidOperationException">The suffix is not a known option.</exception>
    private static RoundOption ParseRoundOption(string option, string round) =>
        option.Trim().ToUpperInvariant() switch
        {
            "DREBIN" => RoundOption.DrebinPoints,
            _ => throw new InvalidOperationException(
                $"'{option}' in '{round}' is not a round option; the only one is DREBIN, written "
                + "after the map as in TDM@Blood Bath:DREBIN."),
        };

    /// <summary>
    /// The seventeen per-rule words a setting states.
    /// </summary>
    /// <param name="timers">The words, comma separated, in the reply's order.</param>
    /// <returns>The seventeen words.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a word is not a number, or when the bank states fewer or more
    /// words than the reply reads.
    /// </exception>
    public static int[] ParseRuleTimers(string? timers)
    {
        var words = (timers ?? string.Empty).Split(
            WordSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length != RuleTimerCount)
        {
            throw new InvalidOperationException(
                $"GAMEPLAY_SERVER_RULE_TIMERS states {words.Length} word(s) where the reply reads "
                + $"{RuleTimerCount}: SNE time and rounds, CAP, RES, TDM with its tickets, DM with its "
                + "tickets, BASE, BOMB and TSNE.");
        }

        var values = new int[RuleTimerCount];
        for (var index = 0; index < words.Length; index++)
        {
            if (!int.TryParse(words[index], out values[index]) || values[index] < 0)
            {
                throw new InvalidOperationException(
                    $"GAMEPLAY_SERVER_RULE_TIMERS word {index + 1} is '{words[index]}', which is not a "
                    + "count of minutes, rounds or kills.");
            }
        }

        return values;
    }

    /// <summary>The minutes a rule's rounds last, from the bank.</summary>
    /// <param name="ruleTimers">The seventeen words.</param>
    /// <param name="rule">Rule id from a rotation triple.</param>
    /// <returns>The rule's own time, or the bank's first when it states none for it.</returns>
    public static int TimeLimitMinutes(IReadOnlyList<int> ruleTimers, int rule)
    {
        ArgumentNullException.ThrowIfNull(ruleTimers);

        var index = TimeIndex(rule);
        return index >= 0 && index < ruleTimers.Count
            ? ruleTimers[index]
            : ruleTimers.Count > 0 ? ruleTimers[0] : 0;
    }

    /// <summary>
    /// The kill limit a rule's rounds end on, or <c>null</c> for a round that ends
    /// on its clock alone.
    /// </summary>
    /// <remarks>
    /// Only the two deathmatches state one: nothing on the gameplay channel counts
    /// kills for a host, so a limit is reached only by the score this host keeps
    /// itself (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §3). A word of zero is
    /// read as no limit rather than as a round that ends at the first kill.
    /// </remarks>
    /// <param name="ruleTimers">The seventeen words.</param>
    /// <param name="rule">Rule id from a rotation triple.</param>
    /// <returns>The rule's ticket count, or <c>null</c> when it states none.</returns>
    public static int? Tickets(IReadOnlyList<int> ruleTimers, int rule)
    {
        ArgumentNullException.ThrowIfNull(ruleTimers);

        var index = rule switch
        {
            MatchRuleUtils.TeamDeathmatch => TdmTicketsIndex,
            MatchRuleUtils.Deathmatch => DmTicketsIndex,
            _ => -1,
        };

        return index >= 0 && index < ruleTimers.Count && ruleTimers[index] > 0
            ? ruleTimers[index]
            : null;
    }
}
