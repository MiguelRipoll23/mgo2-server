using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Sends the gear catalogue, echoed when an outfit is committed.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CommitOutfitHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is null)
        {
            // The reply is a table, not a result code: staying silent stalls
            // the screen, but a session with no character cannot reach it.
            return;
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.CommitOutfitResult,
            GearCatalogue.Payload,
            cancellationToken);
    }
}

/// <summary>Sends the gear catalogue to a client that is opening the wardrobe.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGearHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGear,
            GearCatalogue.Payload,
            cancellationToken);
}

/// <summary>Sends the skill catalogue: every defined skill at its maximum level.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetSkillsHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSkills,
            CharacterPayloadBuilder.BuildSkillsPayload(),
            cancellationToken);
}

/// <summary>Sends the saved skill sets of the session character.</summary>
/// <param name="characterService">Service that owns the saved sets.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetSkillSetsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var sets = session.CharacterIdentifier is { } characterIdentifier
            ? await characterService.GetSkillSetsAsync(characterIdentifier, cancellationToken)
            : [];

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSkillSets,
            CharacterPayloadBuilder.BuildSkillSetsPayload(sets),
            cancellationToken);
    }
}

/// <summary>
/// Stores the saved skill sets. The reply lands on the request's own
/// identifier, which is what the client's waiter reads.
/// </summary>
/// <param name="characterService">Service that owns the saved sets.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateSkillSetsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>u32 modes + five skill bytes + five level bytes + the name.</summary>
    private const int RecordSize = 4 + 5 + 5 + CharacterPayloadBuilder.SetNameLength;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length > 0)
        {
            var reader = new PacketReader(packet.Payload);
            var sets = new List<CharacterSkillSet>();
            var index = 0;

            while (reader.Remaining >= RecordSize)
            {
                var modes = (int)reader.ReadUInt32();
                var skills = new int[5];
                for (var slot = 0; slot < 5; slot++)
                {
                    skills[slot] = reader.ReadUInt8();
                }

                var levels = new int[5];
                for (var slot = 0; slot < 5; slot++)
                {
                    levels[slot] = reader.ReadUInt8();
                }

                var name = reader.ReadFixedString(CharacterPayloadBuilder.SetNameLength);

                // The wire carries five skill and level slots; storage models
                // four. The fifth slot is consumed either way.
                sets.Add(new CharacterSkillSet
                {
                    Index = index,
                    Name = name,
                    Modes = modes,
                    Skill1 = skills[0],
                    Skill2 = skills[1],
                    Skill3 = skills[2],
                    Skill4 = skills[3],
                    Level1 = levels[0],
                    Level2 = levels[1],
                    Level3 = levels[2],
                    Level4 = levels[3],
                });

                index++;
            }

            await characterService.UpdateSkillSetsAsync(characterIdentifier, sets, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.UpdateSkillSets,
            new byte[4],
            cancellationToken);
    }
}

/// <summary>Sends the saved gear sets of the session character.</summary>
/// <param name="characterService">Service that owns the saved sets.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGearSetsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var sets = session.CharacterIdentifier is { } characterIdentifier
            ? await characterService.GetGearSetsAsync(characterIdentifier, cancellationToken)
            : [];

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGearSets,
            CharacterPayloadBuilder.BuildGearSetsPayload(sets),
            cancellationToken);
    }
}

/// <summary>Stores the saved gear sets. The reply lands on the request's own identifier.</summary>
/// <param name="characterService">Service that owns the saved sets.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateGearSetsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>u32 stages + 22 appearance bytes + the name.</summary>
    private const int RecordSize = 4 + 22 + CharacterPayloadBuilder.SetNameLength;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length > 0)
        {
            var reader = new PacketReader(packet.Payload);
            var sets = new List<CharacterGearSet>();
            var index = 0;

            while (reader.Remaining >= RecordSize)
            {
                var stages = (int)reader.ReadUInt32();
                var appearance = reader.ReadBytes(22);
                var name = reader.ReadFixedString(CharacterPayloadBuilder.SetNameLength);

                sets.Add(new CharacterGearSet
                {
                    Index = index,
                    Name = name,
                    Stages = stages,
                    Face = appearance[0],
                    Head = appearance[1],
                    Upper = appearance[2],
                    Lower = appearance[3],
                    Chest = appearance[4],
                    Waist = appearance[5],
                    Hands = appearance[6],
                    Feet = appearance[7],
                    Accessory1 = appearance[8],
                    Accessory2 = appearance[9],
                    HeadColor = appearance[10],
                    UpperColor = appearance[11],
                    LowerColor = appearance[12],
                    ChestColor = appearance[13],
                    WaistColor = appearance[14],
                    HandsColor = appearance[15],
                    FeetColor = appearance[16],
                    Accessory1Color = appearance[17],
                    Accessory2Color = appearance[18],
                    FacePaint = appearance[19],
                });

                index++;
            }

            await characterService.UpdateGearSetsAsync(characterIdentifier, sets, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.UpdateGearSets,
            new byte[4],
            cancellationToken);
    }
}
