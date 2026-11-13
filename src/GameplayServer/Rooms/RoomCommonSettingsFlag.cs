namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The switches of the room-settings word in the gameplay settings body, one bit per
/// room option.
/// </summary>
/// <remarks>
/// <para>
/// Measured from the single-variable captures in <c>.pcapngs/settings</c>, each diffed
/// against a room that changed no switch (the word <c>0x00204022</c> at body
/// <c>0x4f</c>–<c>0x52</c>): flipping one Create Game option moves exactly one bit.
/// The exception is Friendly Fire, whose capture moves <c>0x01</c> and <c>0x10</c>
/// together; no capture separates the pair.
/// </para>
/// <para>
/// <b>It is a bitmask.</b> The single-variable captures isolate each bit, and the
/// rooms outside that set show several at once: <c>0x00274022</c> carries Auto
/// Assign Teams, Teams Switch Positions and Lock On together, and
/// <c>0x00274033</c> adds the Friendly Fire pair. So each member is an independent
/// enable, not one value of a one-of-N field.
/// </para>
/// <para>
/// <b>This is not the <c>0x4310</c> common-settings word.</b> The host-settings block
/// carries the same options at its own positions — Friendly Fire is <c>0x142</c> bit 3
/// there — so a value read here cannot be copied into the block, or the other way
/// round.
/// </para>
/// <para>
/// <b>Two of the requested settings are not in this word.</b> Drebin Points is the
/// rotation triple's third byte (<see cref="Mgo2Server.Shared.Types.RoundOption"/>):
/// the Drebin capture sets it to <c>0x02</c> and leaves this word on the baseline.
/// Team switching is not carried by the body at all; the capture that enabled it
/// changed no byte.
/// </para>
/// </remarks>
[Flags]
internal enum RoomCommonSettingsFlag : uint
{
    /// <summary>
    /// Bits <c>0x01</c> and <c>0x10</c> together. The Friendly Fire capture moved both
    /// and no capture moves one alone, so the pair is the confirmed unit; a later
    /// capture that separates them splits this member rather than this guess.
    /// </summary>
    FriendlyFire = 0x0000_0011,

    /// <summary>Bit <c>0x10000</c>: <c>Auto Assign Teams</c>.</summary>
    AutoAssignTeams = 0x0001_0000,

    /// <summary>
    /// Bit <c>0x20000</c>: <c>Teams Switch Positions</c> — the sides swap spawn-point
    /// side between rounds.
    /// </summary>
    TeamsSwitchPositions = 0x0002_0000,

    /// <summary>Bit <c>0x40000</c>: <c>Lock On (AUTO AIM)</c>.</summary>
    LockOnAutoAim = 0x0004_0000,

    /// <summary>
    /// Bit <c>0x400000</c>: the level band at body <c>0x63</c>/<c>0x64</c> is in force.
    /// The band's own values are written whether or not this bit is set, which is why a
    /// disabled limit still carries a base and a tolerance.
    /// </summary>
    LevelLimitEnabled = 0x0040_0000,
}
