namespace Mgo2Server.Shared.Types;

/// <summary>
/// The kind byte at offset <c>0x03</c> of a <c>0x1001</c> / <c>0x9001</c> record: which of
/// the client's stage object nodes consumes the record's payload.
/// </summary>
/// <remarks>
/// <para>
/// The client's handler for the family (<c>0x282D80</c> in <c>MGO2.ELF</c>) reads the byte
/// and uses it to index a node table on the stage object, handing each record's payload to
/// that node's first vtable slot — so the kind is the record's <em>type</em>, not a
/// sub-type of a roster entry. A roster entry is kind <see cref="RosterEntry"/> and the
/// four shapes beside it are the stage's own records. The mapping is
/// <c>docs/analysis/roster-record-kinds.md</c> §§3–4 and the lengths each kind was measured
/// at are <c>docs/protocol/UDP_COMMANDS.md</c>, "Kinds <c>0x01</c>–<c>0x04</c>".
/// </para>
/// <para>
/// <b>Three of the five say where a player stands.</b>
/// <see cref="RespawnPoint"/> is one spawn point per player slot, with the slot, the
/// player's place and a sixteen-bit direction beside the three coordinates;
/// <see cref="CustomPoint"/> is one custom point and its action, the point written twice;
/// and <see cref="Box"/> is a region — a header and a count of eight-byte corners, which a
/// host also writes with a count of nought to close it. Only
/// <see cref="QuadAndItems"/> is unread; it shares <see cref="Box"/>'s tail two bytes
/// further down and none of its four items is a corner.
/// </para>
/// </remarks>
public enum RoomRecordKind : byte
{
    /// <summary>A roster entry, or the close that ends the run: the player's own record.</summary>
    RosterEntry = 0x00,

    /// <summary>
    /// Four indented entries behind a quad of four signed sixteens, in the shape
    /// <see cref="Box"/>'s tail takes two bytes further down. What it states is
    /// **[U]**; the frame is measured.
    /// </summary>
    QuadAndItems = 0x01,

    /// <summary>A region: an eight-byte header, a count, and that many eight-byte corners.</summary>
    Box = 0x02,

    /// <summary>A custom point and its action: a tag, one point written twice, a sixteen-bit tail.</summary>
    CustomPoint = 0x03,

    /// <summary>
    /// One respawn point per player slot. The client's setter is
    /// <c>PL_NetSetRespawn</c>, which stores the point and its direction beside each other
    /// on the player, and the supervisor fills it from <c>SV_GetInitialPosDir</c>.
    /// </summary>
    RespawnPoint = 0x04,
}
