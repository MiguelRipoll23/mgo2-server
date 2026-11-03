namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The values body field 8 was captured carrying: whole minutes held as seconds.
/// </summary>
/// <remarks>
/// The client's Create Game screen offers two settings in minutes that this could be —
/// <c>Round Time</c> and <c>Briefing Time</c> — and a room keeps its own value across
/// every one of its bodies, which no gameplay byte separates further
/// (<c>docs/analysis/rotation-payload-corpus.md</c> §8). Four values were seen and no
/// fifth, so the members are those four rather than a wider range.
/// </remarks>
internal enum RoomMinuteLimit : ushort
{
    /// <summary><c>0</c>: the rooms that leave this setting at zero.</summary>
    ZeroMinutes = 0,

    /// <summary><c>180</c>: three minutes.</summary>
    ThreeMinutes = 180,

    /// <summary><c>240</c>: four minutes.</summary>
    FourMinutes = 240,

    /// <summary><c>300</c>: five minutes.</summary>
    FiveMinutes = 300,
}
