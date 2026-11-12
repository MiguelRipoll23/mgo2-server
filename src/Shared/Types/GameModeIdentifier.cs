namespace Mgo2Server.Shared.Types;

/// <summary>
/// A room's rule, the console's <c>MODE_NAMES</c>. The ids are the wire's, and the
/// list is sparse by design: only the rules a room can be published under have a name.
/// </summary>
public enum GameModeIdentifier : byte
{
    DM = 0,
    TDM = 1,
    RES = 2,
    CAP = 3,
    SNE = 4,
    BASE = 5,
    BOMB = 6,
    TSNE = 7,
    SDM = 12,
    INTERVAL = 13,
    SCAP = 15,
    RACE = 16,
}
