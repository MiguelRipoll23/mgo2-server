namespace Mgo2Server.Shared.Types;

/// <summary>
/// One round's options: the third byte of a rotation triple, which is body
/// <c>0x1a + 3 × round + 2</c> of the room-state payload and the same byte in the
/// room's <c>0x4310</c> block and <c>0x4313</c> reply.
/// </summary>
/// <remarks>
/// <para>
/// <b>The low value is a three-way radio, not a bitfield.</b> The menu table at
/// <c>0xFE7084/88/8C</c> of <c>MGO2.ELF</c> labels the three states
/// <c>obj_4_select_others</c>, <c>icon_drebin_point</c> and <c>icon_HSonly</c>,
/// and the client reads the byte at <c>0x6A9948</c> to draw a Drebin round pink
/// and a Headshots-Only round light blue (<c>docs/GATES.md</c> §2). The radio's
/// values are <c>0</c>, <c>2</c> and <c>4</c>; a value carrying <c>0x02</c> and
/// <c>0x04</c> together has never been measured.
/// </para>
/// <para>
/// <b>The toggle was measured against a disabled room.</b> The room-state bodies
/// of <c>pcapng1</c> — a game whose weapon-select screen read
/// <c>DP: 1000/8500</c> — carry <c>0x02</c> on all thirteen of its configured
/// triples, and the bodies of <c>game-with-drebin-points-disabled</c> carry
/// <c>0x00</c> on all seven of its own. So the byte is what states the mode to a
/// joiner; it is not a per-round cosmetic the client decides for itself.
/// </para>
/// <para>
/// <b><see cref="Unknown0x20"/> is a second bit and not part of the radio.</b>
/// Two of <c>pcapng1</c>'s triples read <c>0x22</c> — the radio's Drebin value
/// with <c>0x20</c> beside it — so the byte holds one more switch than the menu
/// offers. Nothing in the corpus names it and it is written as the room states it.
/// </para>
/// </remarks>
[Flags]
public enum RoundOption : byte
{
    /// <summary>Neither Drebin Points nor Headshots Only: the round's ordinary rules.</summary>
    Normal = 0x00,

    /// <summary>
    /// The round is played for Drebin Points: the weapon-select screen draws a DP
    /// balance and a purchase cost beside it.
    /// </summary>
    DrebinPoints = 0x02,

    /// <summary>
    /// The round scores headshots only, which the room list draws light blue.
    /// </summary>
    HeadshotsOnly = 0x04,

    /// <summary>
    /// A bit the menu table does not offer and no capture names. It rides beside
    /// the radio (<c>0x22</c> on two of one room's triples) and is preserved as
    /// the room states it rather than mapped to an option.
    /// </summary>
    Unknown0x20 = 0x20,
}
