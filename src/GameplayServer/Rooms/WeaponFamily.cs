namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The three weapon families a player chooses one weapon from — primary,
/// secondary and support — in the order the weapon-select screen presents them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three of the block's six records are families.</b> A room's host publishes
/// six weapon-select records for a player, <c>0x12e2</c>–<c>0x12e7</c>, and every
/// capture shows a client reporting one weapon on three of them. The weapon-select
/// stage's script builds three groups and its own text names them "primary,
/// secondary, and support … one weapon from each type", so the three reporting
/// identifiers are the three families
/// (<see cref="WeaponSelectionBodiesUtils.TryGetFamily"/>). The other three rows
/// are never reported and stay <c>0xff</c>.
/// </para>
/// <para>
/// <b>The family is the choice; the identifier is only where it travels.</b> A
/// family is stored as one value per lobby slot, and it is the identifier of the
/// family that a report arrives on and that the row is published under.
/// </para>
/// </remarks>
public enum WeaponFamily
{
    /// <summary>The primary weapon: the firearm a player is issued with.</summary>
    Primary,

    /// <summary>The secondary weapon: the sidearm beside the primary.</summary>
    Secondary,

    /// <summary>The support weapon: what is carried beside the two firearms, thrown, blade and heavy.</summary>
    Support,
}
