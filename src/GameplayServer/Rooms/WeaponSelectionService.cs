using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's weapon choices: for each of the three weapon families and each
/// lobby slot, the weapon that player last reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>A player has three choices, and the room holds those three.</b> The block
/// a host publishes is six records, but only three of them are families
/// (<see cref="WeaponFamily"/>); the other three carry no choice a client can
/// make and are published unset for every slot.
/// </para>
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
    private const int SlotCount = 256;

    private static readonly int FamilyCount = Enum.GetValues<WeaponFamily>().Length;

    private readonly object gate = new();
    private readonly WeaponIdentifier[,] values = new WeaponIdentifier[FamilyCount, SlotCount];

    /// <summary>Builds the room's choices, every family unset for every slot.</summary>
    public WeaponSelectionService()
    {
        for (var family = 0; family < FamilyCount; family++)
        {
            for (var slot = 0; slot < SlotCount; slot++)
            {
                values[family, slot] = WeaponSelectionBodiesUtils.UnsetValue;
            }
        }
    }

    /// <summary>Records one player's choice of one weapon family.</summary>
    /// <param name="family">The family the player chose a weapon from.</param>
    /// <param name="slot">The reporting player's own lobby slot.</param>
    /// <param name="value">Weapon the player reported.</param>
    public void Note(WeaponFamily family, byte slot, WeaponIdentifier value)
    {
        lock (gate)
        {
            values[(int)family, slot] = value;
        }
    }

    /// <summary>
    /// The block one slot publishes: every record identifier of the block, the
    /// last one first, as the reference host writes it. Each of the three family
    /// rows carries that slot's choice; the three rows without a family carry
    /// the unset value.
    /// </summary>
    /// <param name="slot">The player's own lobby slot.</param>
    public IReadOnlyList<RosterRecord> RowsFor(byte slot)
    {
        var rows = new List<RosterRecord>(WeaponSelectionBodiesUtils.BlockIdentifierCount);

        lock (gate)
        {
            // 0x12e7 first, 0x12e2 last: every captured block is written in that
            // order, and the slot's join-state row follows it.
            for (var identifier = WeaponSelectionBodiesUtils.BlockLastIdentifier;
                 identifier >= WeaponSelectionBodiesUtils.BlockFirstIdentifier;
                 identifier--)
            {
                var value = WeaponSelectionBodiesUtils.TryGetFamily(identifier, out var family)
                    ? values[(int)family, slot]
                    : WeaponSelectionBodiesUtils.UnsetValue;

                rows.Add(new RosterRecord(
                    identifier,
                    WeaponSelectionBodiesUtils.Body(value, slot),
                    0));
            }
        }

        return rows;
    }
}
