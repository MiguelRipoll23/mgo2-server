namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Everything a bot knows about the match it is in, at the instant it decides.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a snapshot and not a handle on the room.</b> A decision reads one
/// consistent picture — the round, the rule, the map, the bot's own side and the
/// players around it — and no part of the decision system reaches back into the
/// roster or the match rows while it decides. A room that changes underneath a
/// tick is therefore a tick taken on the room before the change, which is what
/// the next pass corrects.
/// </para>
/// <para>
/// The rule and the map are the round's own, from the room's rotation
/// (<c>MatchSettings.Round</c>), so a bot changes mode with the room and never on
/// a clock of its own.
/// </para>
/// </remarks>
/// <param name="MatchIdentifier">The room this bot is in.</param>
/// <param name="RoundNumber">The round the room is on, counting from one.</param>
/// <param name="Rule">The round's rule, in the replay decoder's numbering.</param>
/// <param name="Map">The round's map, in the same numbering.</param>
/// <param name="Participant">The bot the decision is taken for.</param>
/// <param name="Role">The role it plays this round.</param>
/// <param name="Perception">What it can see, or what it cannot.</param>
/// <param name="Objective">The mode's objective for this round.</param>
public sealed record BotContext(
    int MatchIdentifier,
    int RoundNumber,
    int Rule,
    int Map,
    BotParticipant Participant,
    BotRole Role,
    BotPerceptionSnapshot Perception,
    IBotObjective Objective)
{
    /// <summary>Whether the bot knows which side it is on.</summary>
    public bool HasSide => Participant.Team is not null;

    /// <summary>Whether anyone hostile to the bot has been seen.</summary>
    public bool HasEnemies => Perception.Enemies.Count > 0;
}
