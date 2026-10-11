namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// A read of the room's round state, taken so a peer's writer can decide what to
/// write without holding the room's lock while it sends.
/// </summary>
/// <param name="Version">
/// Bumped every time a round opens. A writer compares it with the version it
/// last saw to tell a new round from the one it is already playing.
/// </param>
/// <param name="Number">Round the room is playing, counted from one; zero before the first opens.</param>
/// <param name="Generation">Identifier set the current round's stream uses.</param>
/// <param name="MaximumRounds">Rounds the room's rules allow, or zero for no limit.</param>
public sealed record MatchRoundSnapshot(
    long Version,
    int Number,
    MatchRecordGenerationUtils Generation,
    int MaximumRounds)
{
    /// <summary>Whether a round has been opened and its stream can be written.</summary>
    public bool IsOpen => Number > 0;
}
