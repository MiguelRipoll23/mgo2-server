namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The simple states a bot's body is in, as the finite state machine drives them.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are body states and not decisions.</b> What a bot wants to do is a
/// <see cref="BotAction"/> chosen by the decision system; this is what its
/// character is doing while that action is carried out, which is why the two are
/// separate: a bot advancing and a bot retreating are both <see cref="Moving"/>,
/// and a bot that has chosen to engage is <see cref="Aiming"/> before it is
/// <see cref="Firing"/>.
/// </para>
/// <para>
/// No transition here writes anything to the wire. The operations stream's aim,
/// fire and reload records are the client's own and their shapes have not been
/// measured for this host, so a state is where a bot's body would be and not a
/// record this server sends.
/// </para>
/// </remarks>
public enum BotState
{
    /// <summary>Nothing to do: the bot is waiting for a decision to change.</summary>
    Waiting,

    /// <summary>The bot is walking to a destination.</summary>
    Moving,

    /// <summary>The bot is bringing its weapon onto a target.</summary>
    Aiming,

    /// <summary>The bot is shooting.</summary>
    Firing,

    /// <summary>The bot is reloading.</summary>
    Reloading,

    /// <summary>The bot is dead and waiting for its next round.</summary>
    Dead,
}
