namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's weapon-select values: for each identifier of the block and each
/// lobby slot, the byte that player last reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>It holds what the clients said and invents nothing.</b> Every slot starts
/// at <see cref="WeaponSelectionBodiesUtils.UnsetValue"/>, which is the value
/// the reference host publishes for a family nobody has reported, so a block
/// built for a slot that has said nothing is the same six rows the capture
/// shows for its silent slots. A report replaces only its own family's value.
/// </para>
/// <para>
/// <b>One room, one host.</b> The gameplay server serves a single room, so this
/// is a singleton like the room's own roster, and the lock is for the peers'
/// frames being handled side by side rather than for anything shared with
/// another room.
/// </para>
/// </remarks>
public sealed class WeaponSelectionService
{
    private readonly object gate = new();
    private readonly byte[,] values = new byte[WeaponSelectionBodiesUtils.FamilyCount, 256];

    /// <summary>Builds the room's values, every family unset.</summary>
    public WeaponSelectionService()
    {
        for (var family = 0; family < WeaponSelectionBodiesUtils.FamilyCount; family++)
        {
            for (var slot = 0; slot < 256; slot++)
            {
                values[family, slot] = WeaponSelectionBodiesUtils.UnsetValue;
            }
        }
    }

    /// <summary>
    /// Records one player's report of one family.
    /// </summary>
    /// <param name="family">Identifier the report arrived on.</param>
    /// <param name="slot">The reporting player's own lobby slot.</param>
    /// <param name="value">Value the player reported.</param>
    public void Note(ushort family, byte slot, byte value)
    {
        if (!WeaponSelectionBodiesUtils.IsFamily(family))
        {
            return;
        }

        lock (gate)
        {
            values[family - WeaponSelectionBodiesUtils.FirstFamily, slot] = value;
        }
    }

    /// <summary>
    /// The block one slot publishes: every family of the block, the last
    /// identifier first, as the reference host writes it.
    /// </summary>
    /// <param name="slot">The player's own lobby slot.</param>
    public IReadOnlyList<RosterRecord> RowsFor(byte slot)
    {
        var rows = new List<RosterRecord>(WeaponSelectionBodiesUtils.FamilyCount);

        lock (gate)
        {
            // 0x12e7 first, 0x12e2 last: every captured block is written in that
            // order, and the slot's join-state row follows it.
            for (var family = WeaponSelectionBodiesUtils.LastFamily;
                 family >= WeaponSelectionBodiesUtils.FirstFamily;
                 family--)
            {
                var value = values[family - WeaponSelectionBodiesUtils.FirstFamily, slot];
                rows.Add(new RosterRecord(
                    family,
                    WeaponSelectionBodiesUtils.Body(value, slot),
                    0));
            }
        }

        return rows;
    }
}
