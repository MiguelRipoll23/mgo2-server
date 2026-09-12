using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Stores the appearance, skills and comment the client submits.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdatePersonalInfoHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var update = ReadUpdate(packet.Payload);

        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            await characterService.UpdateAppearanceAsync(characterIdentifier, appearance =>
            {
                appearance.FacePaint = update.FacePaint;
                appearance.Upper = update.Upper;
                appearance.Lower = update.Lower;
                appearance.UpperColor = update.UpperColor;
                appearance.LowerColor = update.LowerColor;
                appearance.Head = update.Head;
                appearance.HeadColor = update.HeadColor;
                appearance.Chest = update.Chest;
                appearance.ChestColor = update.ChestColor;
                appearance.Waist = update.Waist;
                appearance.WaistColor = update.WaistColor;
                appearance.Hands = update.Hands;
                appearance.HandsColor = update.HandsColor;
                appearance.Feet = update.Feet;
                appearance.FeetColor = update.FeetColor;
                appearance.Accessory1 = update.Accessory1;
                appearance.Accessory1Color = update.Accessory1Color;
                appearance.Accessory2 = update.Accessory2;
                appearance.Accessory2Color = update.Accessory2Color;
            }, cancellationToken);

            // The equipped skills are echoed back and must be stored too, or
            // they vanish on the next connect burst.
            await characterService.UpdateEquippedSkillsAsync(characterIdentifier, skills =>
            {
                skills.Skill1 = update.Skill1;
                skills.Skill2 = update.Skill2;
                skills.Skill3 = update.Skill3;
                skills.Skill4 = update.Skill4;
                skills.Level1 = update.Level1;
                skills.Level2 = update.Level2;
                skills.Level3 = update.Level3;
                skills.Level4 = update.Level4;
            }, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.UpdatePersonalInfoResult,
            WriteUpdateResponse(update),
            cancellationToken);
    }

    private static PersonalInfoUpdate ReadUpdate(byte[] payload)
    {
        var reader = new PacketReader(payload);
        var update = new PersonalInfoUpdate
        {
            Upper = reader.ReadUInt8(),
            Lower = reader.ReadUInt8(),
            FacePaint = reader.ReadUInt8(),
            UpperColor = reader.ReadUInt8(),
            LowerColor = reader.ReadUInt8(),
            Head = reader.ReadUInt8(),
            Chest = reader.ReadUInt8(),
            Hands = reader.ReadUInt8(),
            Waist = reader.ReadUInt8(),
            Feet = reader.ReadUInt8(),
            Accessory1 = reader.ReadUInt8(),
            Accessory2 = reader.ReadUInt8(),
            HeadColor = reader.ReadUInt8(),
            ChestColor = reader.ReadUInt8(),
            HandsColor = reader.ReadUInt8(),
            WaistColor = reader.ReadUInt8(),
            FeetColor = reader.ReadUInt8(),
            Accessory1Color = reader.ReadUInt8(),
            Accessory2Color = reader.ReadUInt8(),
            Skill1 = reader.ReadInt8(),
            Skill2 = reader.ReadInt8(),
            Skill3 = reader.ReadInt8(),
            Skill4 = reader.ReadInt8(),
        };

        reader.Skip(1);
        update.Level1 = reader.ReadInt8();
        update.Level2 = reader.ReadInt8();
        update.Level3 = reader.ReadInt8();
        update.Level4 = reader.ReadInt8();
        reader.Skip(2);
        update.Comment = reader.ReadFixedString(128);
        return update;
    }

    private static byte[] WriteUpdateResponse(PersonalInfoUpdate update)
    {
        var writer = new PacketWriter();
        writer.WritePadding(4);
        writer.WriteUInt8(update.Upper);
        writer.WriteUInt8(update.Lower);
        writer.WriteUInt8(update.FacePaint);
        writer.WriteUInt8(update.UpperColor);
        writer.WriteUInt8(update.LowerColor);
        writer.WriteUInt8(update.Head);
        writer.WriteUInt8(update.Chest);
        writer.WriteUInt8(update.Hands);
        writer.WriteUInt8(update.Waist);
        writer.WriteUInt8(update.Feet);
        writer.WriteUInt8(update.Accessory1);
        writer.WriteUInt8(update.Accessory2);
        writer.WriteUInt8(update.HeadColor);
        writer.WriteUInt8(update.ChestColor);
        writer.WriteUInt8(update.HandsColor);
        writer.WriteUInt8(update.WaistColor);
        writer.WriteUInt8(update.FeetColor);
        writer.WriteUInt8(update.Accessory1Color);
        writer.WriteUInt8(update.Accessory2Color);
        writer.WriteInt8(update.Skill1);
        writer.WriteInt8(update.Skill2);
        writer.WriteInt8(update.Skill3);
        writer.WriteInt8(update.Skill4);
        writer.WritePadding(1);
        writer.WriteInt8(update.Level1);
        writer.WriteInt8(update.Level2);
        writer.WriteInt8(update.Level3);
        writer.WriteInt8(update.Level4);
        writer.WritePadding(1);

        for (var slot = 0; slot < 4; slot++)
        {
            writer.WriteUInt32((uint)CharacterSkillCatalogue.MaximumSkillExperience);
        }

        writer.WritePadding(5);
        writer.WriteFixedString(update.Comment, 128);
        // Face-paint colour unlock bitmask: all thirty-two colours unlocked.
        writer.WriteUInt32(0xffffffff);
        return writer.Build();
    }
}

/// <summary>Fields of one personal-information write-back.</summary>
internal sealed class PersonalInfoUpdate
{
    /// <summary>Upper-body equipment index.</summary>
    public int Upper { get; set; }

    /// <summary>Lower-body equipment index.</summary>
    public int Lower { get; set; }

    /// <summary>Face-paint index.</summary>
    public int FacePaint { get; set; }

    /// <summary>Upper-body colour index.</summary>
    public int UpperColor { get; set; }

    /// <summary>Lower-body colour index.</summary>
    public int LowerColor { get; set; }

    /// <summary>Head equipment index.</summary>
    public int Head { get; set; }

    /// <summary>Chest equipment index.</summary>
    public int Chest { get; set; }

    /// <summary>Hand equipment index.</summary>
    public int Hands { get; set; }

    /// <summary>Waist equipment index.</summary>
    public int Waist { get; set; }

    /// <summary>Foot equipment index.</summary>
    public int Feet { get; set; }

    /// <summary>First accessory index.</summary>
    public int Accessory1 { get; set; }

    /// <summary>Second accessory index.</summary>
    public int Accessory2 { get; set; }

    /// <summary>Head equipment colour index.</summary>
    public int HeadColor { get; set; }

    /// <summary>Chest equipment colour index.</summary>
    public int ChestColor { get; set; }

    /// <summary>Hand equipment colour index.</summary>
    public int HandsColor { get; set; }

    /// <summary>Waist equipment colour index.</summary>
    public int WaistColor { get; set; }

    /// <summary>Foot equipment colour index.</summary>
    public int FeetColor { get; set; }

    /// <summary>First accessory colour index.</summary>
    public int Accessory1Color { get; set; }

    /// <summary>Second accessory colour index.</summary>
    public int Accessory2Color { get; set; }

    /// <summary>First equipped skill.</summary>
    public int Skill1 { get; set; }

    /// <summary>Second equipped skill.</summary>
    public int Skill2 { get; set; }

    /// <summary>Third equipped skill.</summary>
    public int Skill3 { get; set; }

    /// <summary>Fourth equipped skill.</summary>
    public int Skill4 { get; set; }

    /// <summary>Level of the first equipped skill.</summary>
    public int Level1 { get; set; }

    /// <summary>Level of the second equipped skill.</summary>
    public int Level2 { get; set; }

    /// <summary>Level of the third equipped skill.</summary>
    public int Level3 { get; set; }

    /// <summary>Level of the fourth equipped skill.</summary>
    public int Level4 { get; set; }

    /// <summary>Comment of the character.</summary>
    public string Comment { get; set; } = string.Empty;
}
