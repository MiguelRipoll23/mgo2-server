namespace Mgo2Server.Shared.Types;

/// <summary>
/// A stage, the console's <c>MAPS</c> keys: the id a room's rotation carries and the name
/// a player knows the stage by.
/// </summary>
/// <remarks>
/// The stage name the disc ships each map under is <c>MatchMapUtils</c>' column, beside
/// these names; <see cref="Unnamed"/> is the table's own placeholder row, which exists and
/// names nothing.
/// </remarks>
public enum MapIdentifier : byte
{
    ForestFirefight = 1,
    AmbushAlley = 2,
    UrbanUltimatum = 3,
    GronznyjGrad = 4,
    Coppertown = 5,
    HazardHouse = 6,
    BloodBath = 7,
    TombOfTubes = 8,
    RavagedRiverfront = 9,
    SiloSunset = 10,
    IceboundInferno = 11,
    Midtown = 12,
    WinterWarehouse = 13,
    VirtuousVista = 14,
    OuterOutlet = 15,
    Unknown = 16,
    DesertDuel = 17,
    LethalLeviathan = 18,
    PerilousPalace = 19,
    QuaintQuarrel = 20,
    JadeJunction = 21,
    EasternEurope = 22,
}
