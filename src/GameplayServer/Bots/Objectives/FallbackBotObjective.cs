namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The objective a round gets when its rule is one this server has no behaviour
/// for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It says so rather than pretending to be a deathmatch.</b> A rotation can
/// name rules the bot system has no objective for — Stealth Deathmatch, Interval,
/// Sneaking Capture and Race are in the decoder's table and in no objective here —
/// and a fallback that engaged enemies would be playing a mode it does not know.
/// The bot walks and watches instead, which is a behaviour the mode cannot score
/// wrongly, and the reason carries the rule's own name so the gap is visible in a
/// log rather than only in this file.
/// </para>
/// </remarks>
public sealed class FallbackBotObjective : IBotObjective
{
    /// <summary>Rule value for a round whose rule no objective covers.</summary>
    public const int UnsupportedRule = -1;

    /// <inheritdoc />
    public int Rule => UnsupportedRule;

    /// <inheritdoc />
    public string Name => "Unsupported mode";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } = [BotObjectiveKind.Patrol];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.Patrol,
                BotAction.Patrol,
                0.5,
                $"No objective covers rule {context.Rule}; the bot walks and watches."),
        ];
    }
}
