namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The slot a rule's time occupies in the room-state body's seventeen-word vector at
/// <c>0x65</c>–<c>0x86</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The value is the rule id, which is also the vector word.</b> Words 0–7 are the
/// eight standard rules in id order — Deathmatch, Team Deathmatch, Rescue, Capture,
/// Sneaking, Base, Bomb and Team Sneaking — and words 12–16 are the five rules the
/// lobby names by id: Stealth Deathmatch (SDM), Interval, Anesthesia (ANEST), Sneaking
/// Capture (SCAP) and Race.
/// </para>
/// <para>
/// Words 8–11 belong to rule ids 8–11, which no shipped rule uses; the corpus leaves
/// them unmapped and a live room writes zero there.
/// </para>
/// </remarks>
internal enum RuleTimerSlot
{
    /// <summary>Deathmatch, rule id 0.</summary>
    Deathmatch = 0,

    /// <summary>Team Deathmatch, rule id 1.</summary>
    TeamDeathmatch = 1,

    /// <summary>Rescue, rule id 2.</summary>
    Rescue = 2,

    /// <summary>Capture, rule id 3.</summary>
    Capture = 3,

    /// <summary>Sneaking, rule id 4.</summary>
    Sneaking = 4,

    /// <summary>Base, rule id 5.</summary>
    Base = 5,

    /// <summary>Bomb, rule id 6.</summary>
    Bomb = 6,

    /// <summary>Team Sneaking, rule id 7.</summary>
    TeamSneaking = 7,

    /// <summary>Rule id 8's slot; no shipped rule uses it.</summary>
    Rule8 = 8,

    /// <summary>Rule id 9's slot; no shipped rule uses it.</summary>
    Rule9 = 9,

    /// <summary>Rule id 10's slot; no shipped rule uses it.</summary>
    Rule10 = 10,

    /// <summary>Rule id 11's slot; no shipped rule uses it.</summary>
    Rule11 = 11,

    /// <summary>Stealth Deathmatch (SDM), rule id 12.</summary>
    StealthDeathmatch = 12,

    /// <summary>Interval, rule id 13.</summary>
    Interval = 13,

    /// <summary>Anesthesia (ANEST), rule id 14.</summary>
    Anesthesia = 14,

    /// <summary>Sneaking Capture (SCAP), rule id 15.</summary>
    SneakingCapture = 15,

    /// <summary>Race, rule id 16.</summary>
    Race = 16,
}
