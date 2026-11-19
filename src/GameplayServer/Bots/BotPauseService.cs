namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Whether this gameplay room's bots are holding their fire.
/// </summary>
/// <remarks>
/// <para>
/// <b>It gates the trigger and nothing else.</b> A paused bot still perceives,
/// chooses a target, aims and walks, and a player's hit still lands on it. The
/// only thing the pause removes is the shot itself, which is what a player
/// asking the room to pause wants: the bots keep their places and their
/// reactions, and no damage comes back from them.
/// </para>
/// <para>
/// <b>The room is this process's room.</b> A gameplay server hosts one room, so
/// one flag is the room's whole state. The lobby still names the room it is
/// asking about, and <see cref="Mgo2Server.GameplayServer.Commands.Grpc.BotPauseGrpcService"/>
/// checks that name before setting this, so a command for another room is refused rather
/// than applied here.
/// </para>
/// </remarks>
public sealed class BotPauseService
{
    private int paused;

    /// <summary>Whether every bot in the room is holding its fire.</summary>
    public bool IsPaused => Volatile.Read(ref paused) != 0;

    /// <summary>
    /// Sets whether the bots hold their fire.
    /// </summary>
    /// <param name="value">Whether shooting is paused.</param>
    /// <returns><c>true</c> when the value changed.</returns>
    public bool SetPaused(bool value)
    {
        var next = value ? 1 : 0;
        return Interlocked.Exchange(ref paused, next) != next;
    }
}
