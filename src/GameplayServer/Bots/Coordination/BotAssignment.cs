namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What one bot of a team was assigned: its role, the objective it works on and
/// the side it works it from.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a record of a decision and not a message.</b> Nothing sends an
/// assignment: MGO2 has no order channel a host writes, and a bot is not a client
/// that could read one. The assignment is what the coordination service hands the
/// bots' decisions so that two of them do not walk to the same objective while a
/// third stands still, and it is logged so the distribution can be read back.
/// </para>
/// </remarks>
/// <param name="BotName">The bot the assignment is for.</param>
/// <param name="Role">The role it plays.</param>
/// <param name="Objective">The objective it works on.</param>
/// <param name="Team">The side it works from, or <c>null</c> while none is resolved.</param>
public sealed record BotAssignment(
    string BotName,
    BotRole Role,
    BotObjectiveKind Objective,
    short? Team);
