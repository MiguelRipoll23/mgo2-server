namespace Mgo2Server.Shared.Types;

/// <summary>
/// A player's skill, as a loadout reports it: the console's <c>SKILL_NAMES</c>, indexed
/// from one, so a reported id is this value and not an offset into the list.
/// </summary>
public enum SkillIdentifier : byte
{
    HandMas = 1,
    SbmcMas = 2,
    AssltMas = 3,
    ShotMas = 4,
    SnipperMas = 5,
    Zoom = 6,
    Lockon = 7,
    Throw = 8,
    Trap = 9,
    Cqc = 10,
    Knife = 11,
    Fastmov = 12,
    Enemy = 13,
    Aware = 14,
    Alert = 15,
    Scanning = 16,
    Officer = 17,
    Recover = 18,
    Cbox = 19,
    CqcEx = 20,
    ShieldMas = 21,
    ScanningEx = 22,
    SopJamming = 23,
    Toughness = 24,
    Charm = 25,
}
