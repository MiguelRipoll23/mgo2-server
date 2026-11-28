namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The round boundary of the bots' simulation: what a round that opens leaves the
/// bodies and the deaths of the round that ended in.
/// </summary>
public sealed partial class BotSimulationService
{
    /// <summary>
    /// Returns the room's bots to their spawns with their vitals whole, because a
    /// round is opening.
    /// </summary>
    /// <remarks>
    /// A body left where the last round ended starts the next one mid-map, and the
    /// patrol index it had walked to is not the round's. Dropping the bodies is what
    /// places every bot at its side's spawn again, which is where a body is created
    /// (<see cref="BotLocomotionService.BodyOf"/>); the deaths still owed the closed
    /// round are dropped with them so none is announced into the next.
    /// </remarks>
    public void BeginRound()
    {
        kills.Clear();
        locomotion.Reset();
    }
}
