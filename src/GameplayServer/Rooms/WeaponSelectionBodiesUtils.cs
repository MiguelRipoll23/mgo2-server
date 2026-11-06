using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The six weapon-select records a room's host publishes for a player,
/// <c>0x12e2</c>–<c>0x12e7</c>, each a three-byte body of that player's chosen
/// value, its lobby slot, and <c>0x18</c>; and the three of them that carry the
/// three weapon families.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape is measured, and measured repeatedly.</b> Every block the
/// reference host wrote for a slot in
/// <c>mgo2-game-joined-when-red-team-then-briefing</c>,
/// <c>mgo2-started-on-game-round1</c> and
/// <c>mgo2-game-started-team-selection-selected-blue-then-game</c> is those six
/// records written together, in the order <c>0x12e7</c> down to <c>0x12e2</c>,
/// and every row of every block is the same three bytes: the value the player
/// reported, the slot the block is for, and <c>0x18</c>. A family the player
/// never reported keeps <c>0xff</c>, which is what all six carry in every
/// capture that shows the block — the reference's own rows for slots
/// <c>01</c>, <c>08</c>, <c>09</c>, <c>0a</c>, <c>0b</c>, <c>10</c> and
/// <c>15</c> are all <c>ff SS 18</c>.
/// </para>
/// <para>
/// <b>The report is the same body without the trailer.</b> A team-selection
/// client writes <c>12e4 ff 11</c> · <c>12e3 ff 11</c> · <c>12e2 ff 11</c> and
/// the host answers <c>12e4 ff 11 18</c> · <c>12e3 ff 11 18</c> ·
/// <c>12e2 ff 11 18</c> in the same pass, with no byte of either body changed
/// but the one that is added. The second byte is the reporting player's own
/// slot in both directions, so the row belongs to that player rather than to a
/// new one.
/// </para>
/// <para>
/// <b>Three of the six are the three weapon families, and a client reports
/// exactly those three.</b> The weapon-select stage's script
/// (<c>n007a/script.gcl</c>, localized by the patcher log's <c>wp_select</c> and
/// <c>LmenuWeapon</c> labels) builds <c>WpMng_SetWeaponList -group 0</c>,
/// <c>-group 1</c> and <c>-group 2</c>, and the stage's own text names the
/// three: "switch between primary, secondary, and support … one weapon from each
/// type" (<c>resources.gcl</c>, line 4649). The groups' contents agree: the
/// replay console's id tables (`.gametools/replay-console/app.js`, keyed by the
/// item names a replay carries) put ten of its sixteen firearm ids in group 0,
/// the two pistols (<c>2</c> MK2, <c>7</c> GSR) and nothing else in group 1, and
/// seven of its nine thrown ids plus two blades in group 2 — so the groups are
/// primary, secondary and support in that order, the families
/// <see cref="WeaponFamily"/> names. Across the ten captures surveyed, only
/// <see cref="PrimaryIdentifier"/>, <see cref="SecondaryIdentifier"/> and
/// <see cref="SupportIdentifier"/> are ever reported by a client — as two-byte
/// bodies — and <c>0x12e5</c>–<c>0x12e7</c> are rows no report ever fills. That
/// the reported three are the weapon families is **[V]**; which family each
/// identifier is rests on the order the client reports them in (<c>12e4</c>,
/// <c>12e3</c>, <c>12e2</c> — the screen's top-down order), so the pairing is
/// **[I]**.
/// </para>
/// <para>
/// <b><c>0x12e1</c> stays outside the block.</b> It shares the prefix and the
/// two-byte report, but the host's own row on it is seven bytes —
/// <c>00 11 05 00 00 00 00</c> for the client that reported <c>08 11</c> — and
/// one observation cannot say whether its first byte is the side, its third a
/// mode, or both something else. Nothing here writes it, and
/// <see cref="GameplayServer.Commands.Peer.Room.WeaponSelectionHandler"/> is
/// not bound to it.
/// </para>
/// </remarks>
public static class WeaponSelectionBodiesUtils
{
    /// <summary>First record identifier of the block, the one written last.</summary>
    public const ushort BlockFirstIdentifier = 0x12e2;

    /// <summary>Last record identifier of the block, the one written first.</summary>
    public const ushort BlockLastIdentifier = 0x12e7;

    /// <summary>Number of record identifiers the block carries, the three families among them.</summary>
    public const ushort BlockIdentifierCount = BlockLastIdentifier - BlockFirstIdentifier + 1;

    /// <summary>
    /// Record identifier the primary family's selection travels on. That the
    /// reported three are the three weapon families is **[V]**; which identifier
    /// is which rests on the order the client reports in, so this pairing is
    /// **[I]**.
    /// </summary>
    public const ushort PrimaryIdentifier = 0x12e4;

    /// <summary>Record identifier the secondary family's selection travels on; see <see cref="PrimaryIdentifier"/>.</summary>
    public const ushort SecondaryIdentifier = 0x12e3;

    /// <summary>Record identifier the support family's selection travels on; see <see cref="PrimaryIdentifier"/>.</summary>
    public const ushort SupportIdentifier = 0x12e2;

    /// <summary>Value of a family the player has not reported.</summary>
    public const WeaponIdentifier UnsetValue = WeaponIdentifier.Unreported;

    /// <summary>Length of a published row's body.</summary>
    public const int BodyLength = 3;

    /// <summary>Length of the report a client sends under the same identifier.</summary>
    public const int ReportBodyLength = 2;

    /// <summary>Byte every published row ends on.</summary>
    public const byte Trailer = 0x18;

    /// <summary>Index of the value, in the report and in the row.</summary>
    public const int ValueIndex = 0;

    /// <summary>Index of the reporting player's own slot, in the report and in the row.</summary>
    public const int SlotIndex = 1;

    /// <summary>Whether an identifier is one of the block's six.</summary>
    /// <param name="type">Record identifier as it decoded.</param>
    public static bool IsBlockIdentifier(ushort type) =>
        type >= BlockFirstIdentifier && type <= BlockLastIdentifier;

    /// <summary>Whether a record is a client's report rather than a published row.</summary>
    /// <param name="type">Record identifier as it decoded.</param>
    /// <param name="body">Body as it decoded.</param>
    public static bool IsReport(ushort type, byte[] body) =>
        IsBlockIdentifier(type) && body.Length == ReportBodyLength;

    /// <summary>The record identifier a weapon family's selection travels on.</summary>
    /// <param name="family">The family to name the identifier of.</param>
    public static ushort IdentifierOf(WeaponFamily family) => family switch
    {
        WeaponFamily.Primary => PrimaryIdentifier,
        WeaponFamily.Secondary => SecondaryIdentifier,
        WeaponFamily.Support => SupportIdentifier,
        _ => throw new ArgumentOutOfRangeException(
            nameof(family),
            family,
            "Not one of the three weapon families."),
    };

    /// <summary>
    /// Reads a block identifier as a weapon family. The three rows whose
    /// identifier carries no family — <c>0x12e5</c>–<c>0x12e7</c>, which no
    /// capture has a client report on — answer <see langword="false"/>.
    /// </summary>
    /// <param name="type">Record identifier as it decoded.</param>
    /// <param name="family">The family that identifier belongs to, when it belongs to one.</param>
    public static bool TryGetFamily(ushort type, out WeaponFamily family)
    {
        switch (type)
        {
            case PrimaryIdentifier:
                family = WeaponFamily.Primary;
                return true;
            case SecondaryIdentifier:
                family = WeaponFamily.Secondary;
                return true;
            case SupportIdentifier:
                family = WeaponFamily.Support;
                return true;
            default:
                family = default;
                return false;
        }
    }

    /// <summary>Body of the row a family publishes for a slot.</summary>
    /// <param name="value">The player's value for that family.</param>
    /// <param name="slot">The player's own lobby slot.</param>
    public static byte[] Body(WeaponIdentifier value, byte slot) => [(byte)value, slot, Trailer];
}
