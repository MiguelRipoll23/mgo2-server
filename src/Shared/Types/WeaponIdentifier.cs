namespace Mgo2Server.Shared.Types;

/// <summary>
/// The ELF's weapon master table: 141 rows, and a row's position is its id, which is
/// the byte the wire carries wherever a weapon is named.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read out of the image, not out of a capture.</b> The table sits at vaddr
/// <c>0x1035834</c> with a stride of <c>0x2c</c>, and its first column is each row's
/// display name (<c>docs/WEAPONS.md</c> carries the category bitmask, the non-lethal
/// flag and the live anchors as well). The ids are the confirmed layer: a round's tally
/// list is indexed by them, <see cref="StKnife"/> is where a knife kill lands and
/// <see cref="MosinN"/> where a Mosin faint does, and the replay console's item table
/// names <c>0x14</c> the P90, the one row a capture has also been read on
/// (<see cref="WorldObjectItem.P90"/>). The names are the table's own strings and sit
/// one tier lower: it lists a duplicate SBS (36 and 74) and repeats PUNCH, CLAYMORE,
/// DOLL, GROUND FIRE and seven SURPRESSOR rows. Where a string repeats, the later
/// member carries its id and the table's string in a comment, so every member stays
/// distinct and traceable to its row.
/// </para>
/// <para>
/// <b>The one member that is not a row.</b> <see cref="Unreported"/> is <c>0xff</c>, the
/// value a weapon-select family carries for a player who reported nothing. No table row
/// has that id, and the reference host publishes it in every row nobody filled.
/// </para>
/// </remarks>
public enum WeaponIdentifier : byte
{
    /// <summary>A weapon-select family nobody has reported; not a table row.</summary>
    Unreported = 0xFF,

    None = 0,
    StKnife = 1,
    Ruger = 2,
    Operator = 3,
    Socom = 4,
    Pmm = 5,
    Fiveseven = 6,
    SigGsr = 7,
    DEagle = 8,
    DEagleL = 9,
    Thor = 10,
    M1911A1 = 11,
    RaceGun = 12,
    GunDelSol = 13,
    Pss = 14,
    Glock = 15,
    T17 = 16,
    MP7 = 17,
    MP5 = 18,
    Ingram = 19,
    P90 = 20,
    Bizon = 21,
    Patriot = 22,
    Skorpion = 23,
    M4 = 24,
    AK102 = 25,
    G3A3 = 26,
    AN94 = 27,
    Fal = 28,
    Tane = 29,
    MK17 = 30,
    XM8 = 31,
    Lmg = 32,
    HK21E = 33,
    Pk = 34,
    M60E4 = 35,
    Sbs = 36,
    M870 = 37,
    Saiga = 38,
    Vss = 39,
    M82A2 = 40,
    DSR1 = 41,
    M14EBR = 42,
    MosinN = 43,
    Dragnov = 44,
    Railgun = 45,
    Mgl140 = 46,
    XM25 = 47,
    Stinger = 48,
    Javelin = 49,
    RPG7 = 50,
    M72A3 = 51,
    Grenade = 52,
    WpG = 53,
    StunG = 54,
    ChaffG = 55,
    SmokeG = 56,
    SmokeGR = 57,
    SmokeGG = 58,
    SmokeGY = 59,
    SmokeGP = 60,
    PetroBomb = 61,
    Magazine = 62,
    EmpGrenade = 63,
    Claymore = 64,
    SleepClaymore = 65,
    C4 = 66,
    SleepC4 = 67,
    BaseBuster = 68,
    Book = 69,
    AffectBook = 70,
    Doll = 71,
    Doll72 = 72,  // table "DOLL"
    Shield = 73,
    Sbs74 = 74,  // table "SBS"
    XM320 = 75,
    GP30 = 76,
    Surpressor = 77,
    Surpressor78 = 78,  // table "SURPRESSOR"
    Surpressor79 = 79,  // table "SURPRESSOR"
    Surpressor80 = 80,  // table "SURPRESSOR"
    Surpressor81 = 81,  // table "SURPRESSOR"
    Surpressor82 = 82,  // table "SURPRESSOR"
    Surpressor83 = 83,  // table "SURPRESSOR"
    Scope = 84,
    Dotsight = 85,
    MP7Dotsight = 86,
    Light = 87,
    Lasersight = 88,
    Light89 = 89,  // table "LIGHT"
    GripA = 90,
    GripB = 91,
    RexVulcan = 92,
    RexMissile = 93,
    RexLaser = 94,
    Nighthawk = 95,
    AkKnife = 96,
    ThrowKnife = 97,
    ThrowMachete = 98,
    WpParacord = 99,
    XM8Short = 100,
    XM8Long = 101,
    XM8Jona = 102,
    Mortarbomb = 103,
    M2 = 104,
    Core = 105,
    Claymore106 = 106,  // table "CLAYMORE"
    Kick = 107,
    Punch = 108,
    Punch109 = 109,  // table "PUNCH"
    Stomp = 110,
    Rolling = 111,
    Cqc = 112,
    RollingDrum = 113,
    MK2Spark = 114,
    DoorAttack = 115,
    Drumcan = 116,
    GroundFire = 117,
    GroundFire118 = 118,  // table "GROUND FIRE"
    AnimalStrong = 119,
    C4Trigger = 120,
    SleepC4Trigger = 121,
    Injection = 122,
    CriticalKnife = 123,
    ExplosionSmall = 124,
    ExplosionMiddle = 125,
    ExplosionLarge = 126,
    Tornado = 127,
    Gekkovulcan = 128,
    Gekkomissile = 129,
    GkKick = 130,
    GkStomp = 131,
    RexKick = 132,
    RexBody = 133,
    RayVulcan = 134,
    RayMissile = 135,
    RayCutter = 136,
    RayKick = 137,
    RayBody = 138,
    Binocular = 139,
    Envg = 140,
}
