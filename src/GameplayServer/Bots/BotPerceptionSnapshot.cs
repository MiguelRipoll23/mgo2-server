namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One player a bot can see: the name, the slot and the side it is on.
/// </summary>
/// <remarks>
/// <b>It carries no position and no health, because this host does not read them
/// for a player.</b> The roster says who is in the room and the lobby-slot arrays
/// say which side, and those are the two facts a bot is given. A field added
/// here later must come from a source this server can read, not from a guess at
/// where a player is standing.
/// </remarks>
/// <param name="Name">Character name the player is known by.</param>
/// <param name="SessionSlot">Key in the room's 24-entry player table.</param>
/// <param name="Team">Side the room puts the player on, or <c>null</c> when none.</param>
/// <param name="IsBot">Whether the player is a bot of this host.</param>
public sealed record BotPerceptionEntry(string Name, byte SessionSlot, short? Team, bool IsBot);

/// <summary>
/// What a bot can perceive at one instant, and what it explicitly cannot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The empty lists are a reading and not an empty room.</b> A bot on no side
/// sees no teammate and no enemy, because this host cannot tell a player on the
/// bot's side from one against it without a side to compare with. The
/// <see cref="PositionsKnown"/> flag is the same kind of statement: player
/// positions are not read on this path, so no distance, line of sight or
/// engagement range exists for a decision to use, and a decision that asked for
/// one would be asking for data the host does not have.
/// </para>
/// <para>
/// It is a record so a decision is taken on one consistent view of the room.
/// </para>
/// </remarks>
/// <param name="Teammates">Players the bot's own side holds, not the bot itself.</param>
/// <param name="Enemies">Players on the other side.</param>
/// <param name="OwnHealth">The bot's health, or <c>null</c> while the host holds none.</param>
/// <param name="PositionsKnown">Whether any player's position is known.</param>
public sealed record BotPerceptionSnapshot(
    IReadOnlyList<BotPerceptionEntry> Teammates,
    IReadOnlyList<BotPerceptionEntry> Enemies,
    byte? OwnHealth,
    bool PositionsKnown)
{
    /// <summary>A snapshot with no sight at all: the state a bot on no side has.</summary>
    public static BotPerceptionSnapshot None { get; } = new([], [], null, false);
}
