namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One bot's body in the round: where it stands, where it looks, what it is
/// doing about whom, and how much health it has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>The coordinates are the stage's own units, not the wire's tenths.</b> A bot
/// is placed from <see cref="MapSpawnUtils"/> and walked in the same units the
/// stage's placements are stated in; the record builders divide by ten on the way
/// out, which is the scale the recorded position and aim records carry.
/// </para>
/// <para>
/// <b>Every field here is one the working-server capture shows the host writing
/// for a bot.</b> Health falls in the vitals record, the heading and the three
/// coordinates are the position record, the aim point is the aim record, the
/// target slot and the action are the state record, and a hit is the damage
/// record. Nothing is kept that no record carries.
/// </para>
/// </remarks>
public sealed class BotBodyState
{
    /// <summary>Slot the bot holds in the room's player table.</summary>
    public required byte SessionSlot { get; init; }

    /// <summary>Stage <c>x</c>, in stage units.</summary>
    public float X { get; set; }

    /// <summary>Stage height, in stage units — the stand height of the floor it is on.</summary>
    public float Y { get; set; }

    /// <summary>Stage <c>z</c>, in stage units.</summary>
    public float Z { get; set; }

    /// <summary>Facing, as the recorded heading word.</summary>
    public ushort Yaw { get; set; }

    /// <summary>Health the vitals record reports; zero is a corpse.</summary>
    public short Health { get; set; } = BotBodyState.MaximumVitals;

    /// <summary>Stamina the vitals record reports.</summary>
    public short Stamina { get; set; } = BotBodyState.MaximumVitals;

    /// <summary>Where the bot is looking, in stage units.</summary>
    public float AimX { get; set; }

    /// <summary>Height it is looking at, in stage units.</summary>
    public float AimY { get; set; }

    /// <summary>Where it is looking, in stage units.</summary>
    public float AimZ { get; set; }

    /// <summary>Slot it is engaging, or <see cref="NoTarget"/>.</summary>
    public byte TargetSlot { get; set; } = BotBodyState.NoTarget;

    /// <summary>Slot no player holds, which the state record writes beside no target.</summary>
    public const byte NoTarget = 0xff;

    /// <summary>
    /// The vitals both bytes open at, measured over the working-server capture:
    /// <c>fa fa</c> on every living actor.
    /// </summary>
    public const short MaximumVitals = 250;

    /// <summary>Whether the body is walking this tick, which the position head shows.</summary>
    public bool Moving { get; set; }

    /// <summary>Route it is following, if any.</summary>
    public IReadOnlyList<BotPosition> Route { get; set; } = [];

    /// <summary>Waypoint on that route it is walking towards.</summary>
    public int RouteIndex { get; set; }

    /// <summary>Destination the route was planned for, so a new one replaces it.</summary>
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>When the route may be planned again, in monotonic milliseconds.</summary>
    public long NextRouteMilliseconds { get; set; }

    /// <summary>When it may fire again, in monotonic milliseconds.</summary>
    public long NextShotMilliseconds { get; set; }

    /// <summary>When it stands up again after a death, in monotonic milliseconds.</summary>
    public long RespawnMilliseconds { get; set; }

    /// <summary>Last time the body was advanced, in monotonic milliseconds.</summary>
    public long LastAdvanceMilliseconds { get; set; }

    /// <summary>Whether it is dead, which is health at zero.</summary>
    public bool IsDead => Health <= 0;
}
