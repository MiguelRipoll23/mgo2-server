using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// Appearance a dedicated host character plays with, applied when the character
/// has no appearance row of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the reference host's own face, decoded from its loadout record.</b>
/// The twenty-three bytes at the tail of the recorded fifty-byte body — the
/// block <c>LoadoutBodiesUtils.ReferenceAppearanceBlock</c> holds — decode to
/// exactly the values below, in the field order
/// <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c> lists. Writing the same values
/// into the row means the host's TCP character records and the appearance block
/// its settings burst carries describe one face rather than two.
/// </para>
/// <para>
/// <b>The row is what keeps a zeroed block off the wire.</b> The character
/// records of a character with no appearance row are written as an empty block:
/// <c>CharacterPayloadBuilder</c> pads twenty-seven zero bytes and
/// <c>CharacterListPayloadBuilder</c> writes each field as <c>?? 0</c>. A client
/// reading that block has no parts to build a body from and voice zero to hand
/// its voice plugin, which is the <c>invalid voice id 0</c> the plugin prints —
/// measured on this host and on no other participant, because every other
/// participant is a player whose character always has a row.
/// </para>
/// <para>
/// The values are stored as the flat indices the schema holds, not as the
/// twenty-three bytes: the wire block is built from the row by
/// <c>LoadoutFramesUtils.TryAppearanceBlock</c>, so the two cannot drift.
/// </para>
/// </remarks>
public static class DedicatedHostAppearanceUtils
{
    /// <summary>Head equipment index the recorded host wears.</summary>
    public const int Head = 30;

    /// <summary>Chest equipment index the recorded host wears.</summary>
    public const int Chest = 69;

    /// <summary>Hand equipment index the recorded host wears.</summary>
    public const int Hands = 47;

    /// <summary>Waist equipment index the recorded host wears.</summary>
    public const int Waist = 87;

    /// <summary>Foot equipment index the recorded host wears.</summary>
    public const int Feet = 57;

    /// <summary>First accessory index the recorded host wears.</summary>
    public const int Accessory1 = 103;

    /// <summary>Second accessory index the recorded host wears.</summary>
    public const int Accessory2 = 104;

    /// <summary>Head colour index the recorded host wears.</summary>
    public const int HeadColor = 14;

    /// <summary>Chest colour index the recorded host wears.</summary>
    public const int ChestColor = 14;

    /// <summary>
    /// Hand colour index. The recorded host carries zero here, and the client has
    /// no hand colour to apply — hands take the chest's — so the byte is unused.
    /// </summary>
    public const int HandsColor = 0;

    /// <summary>Waist colour index the recorded host wears.</summary>
    public const int WaistColor = 14;

    /// <summary>Foot colour index the recorded host wears.</summary>
    public const int FeetColor = 14;

    /// <summary>Gender index the recorded host is built as.</summary>
    public const int Gender = 0;

    /// <summary>Face index the recorded host is built with.</summary>
    public const int Face = 0;

    /// <summary>Upper-body equipment index the recorded host wears.</summary>
    public const int Upper = 11;

    /// <summary>Lower-body equipment index the recorded host wears.</summary>
    public const int Lower = 22;

    /// <summary>Face paint index the recorded host wears; none.</summary>
    public const int FacePaint = 0;

    /// <summary>Upper-body colour index the recorded host wears.</summary>
    public const int UpperColor = 14;

    /// <summary>Lower-body colour index the recorded host wears.</summary>
    public const int LowerColor = 14;

    /// <summary>
    /// Voice index the recorded host speaks with: a real voice, not the zero an
    /// absent row leaves behind.
    /// </summary>
    public const int Voice = 7;

    /// <summary>Voice pitch index the recorded host speaks with.</summary>
    public const int Pitch = 15;

    /// <summary>First accessory colour index the recorded host wears.</summary>
    public const int Accessory1Color = 0;

    /// <summary>Second accessory colour index the recorded host wears.</summary>
    public const int Accessory2Color = 0;

    /// <summary>Writes the recorded host's face into an appearance row.</summary>
    /// <param name="appearance">Row to write; its character identifier is left as given.</param>
    public static void Apply(CharacterAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        appearance.Head = Head;
        appearance.Chest = Chest;
        appearance.Hands = Hands;
        appearance.Waist = Waist;
        appearance.Feet = Feet;
        appearance.Accessory1 = Accessory1;
        appearance.Accessory2 = Accessory2;
        appearance.HeadColor = HeadColor;
        appearance.ChestColor = ChestColor;
        appearance.HandsColor = HandsColor;
        appearance.WaistColor = WaistColor;
        appearance.FeetColor = FeetColor;
        appearance.Gender = Gender;
        appearance.Face = Face;
        appearance.Upper = Upper;
        appearance.Lower = Lower;
        appearance.FacePaint = FacePaint;
        appearance.UpperColor = UpperColor;
        appearance.LowerColor = LowerColor;
        appearance.Voice = Voice;
        appearance.Pitch = Pitch;
        appearance.Accessory1Color = Accessory1Color;
        appearance.Accessory2Color = Accessory2Color;
    }
}
