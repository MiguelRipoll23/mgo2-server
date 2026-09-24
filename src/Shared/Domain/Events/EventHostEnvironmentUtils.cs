using System.Buffers.Binary;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the 204-byte host-environment block shared by ordinary room details
/// and the event records. Writing asserts the size, so a layout regression is a
/// failure at the edge rather than a shifted payload the client parses off
/// whatever its buffer held.
/// <para>
/// The reserved runs and the live-match fields (round, round time, team
/// balance) are not modelled: they are written as zero. An event record is a
/// preset rather than a running match, so there is nothing truthful to put in
/// them, and a zero is the value the client already treats as "not started".
/// </para>
/// </summary>
public static class EventHostEnvironmentUtils
{
    /// <summary>Bytes the block occupies on the wire.</summary>
    public const int WireSize = EventConstants.HostEnvironmentWireSize;

    /// <summary>Writes the block, asserting its exact size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="environment">Environment to write.</param>
    public static void Write(PacketWriter writer, EventHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var rotation in environment.Rotations)
        {
            writer.WriteUInt8(rotation[0]);
            writer.WriteUInt8(rotation[1]);
            writer.WriteUInt8(rotation[2]);
        }

        writer.WriteUInt8(environment.RedTeamSkin);
        writer.WriteUInt8(environment.BlueTeamSkin);
        writer.WriteBytes(environment.WeaponRestrictions);
        writer.WriteUInt8(environment.MaximumPlayers);
        writer.WriteUInt8(environment.CurrentPlayers);
        writer.WriteInt32(environment.BriefingTime);
        writer.WritePadding(10);
        writer.WriteUInt16(0);
        writer.WritePadding(4);
        writer.WriteInt32(0);
        writer.WritePadding(2);
        writer.WriteUInt8(environment.Stance);
        writer.WriteUInt8(environment.LevelLimitTolerance);
        writer.WriteInt32(environment.StandardRateOrLevelLimitBase);

        writer.WriteInt32(environment.SneakingTime);
        writer.WriteInt32(environment.SneakingRounds);
        writer.WriteInt32(environment.CaptureTime);
        writer.WriteInt32(environment.CaptureRounds);
        writer.WriteInt32(environment.RescueTime);
        writer.WriteInt32(environment.RescueRounds);
        writer.WriteInt32(environment.TeamDeathmatchTime);
        writer.WriteInt32(environment.TeamDeathmatchRounds);
        writer.WriteInt32(environment.TeamDeathmatchTickets);
        writer.WriteInt32(environment.DeathmatchTime);
        writer.WriteInt32(environment.DeathmatchTickets);
        writer.WriteInt32(environment.BaseTime);
        writer.WriteInt32(environment.BaseRounds);
        writer.WriteInt32(environment.BombTime);
        writer.WriteInt32(environment.BombRounds);
        writer.WriteInt32(environment.TeamSneakingTime);
        writer.WriteInt32(environment.TeamSneakingRounds);

        writer.WriteUInt8(environment.UniqueRed);
        writer.WriteUInt8(environment.UniqueBlue);
        writer.WriteUInt16(0);
        writer.WriteInt32(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(environment.CommonA);
        writer.WriteUInt8(environment.CommonB);
        writer.WriteUInt8(environment.TeamBalance);
        writer.WriteUInt16(environment.IdleKick);
        writer.WriteUInt16(environment.TeamKillKick);
        writer.WriteInt32(environment.NetworkStatus);
        writer.WriteUInt8(environment.CaptureExtraTime);
        writer.WriteUInt8(environment.SneakingSnakeKills);
        writer.WriteUInt8(environment.StealthDeathmatchTime);
        writer.WriteUInt8(environment.StealthDeathmatchRounds);
        writer.WriteUInt8(environment.IntervalTime);
        writer.WriteUInt8(environment.DeathmatchRounds);
        writer.WriteUInt8(environment.SoloCaptureTime);
        writer.WriteUInt8(environment.SoloCaptureRounds);
        writer.WriteUInt8(environment.RaceTime);
        writer.WriteUInt8(environment.RaceRounds);
        writer.WriteUInt8(environment.Field0C6);
        writer.WriteUInt8(environment.HostOptionsExtraTimeFlags);
        writer.WritePadding(4);
    }

    /// <summary>Reads a block.</summary>
    /// <param name="block">Bytes to read; must hold a whole block.</param>
    /// <returns>The decoded environment.</returns>
    /// <exception cref="ArgumentException">Thrown when the block is the wrong size.</exception>
    public static EventHostEnvironment Read(ReadOnlySpan<byte> block)
    {
        if (block.Length != WireSize)
        {
            throw new ArgumentException(
                $"A host-environment block must be exactly {WireSize} bytes (got {block.Length}).",
                nameof(block));
        }

        var environment = new EventHostEnvironment();
        var offset = 0;

        for (var slot = 0; slot < EventHostEnvironment.RotationSlots; slot++)
        {
            environment.Rotations[slot][0] = block[offset++];
            environment.Rotations[slot][1] = block[offset++];
            environment.Rotations[slot][2] = block[offset++];
        }

        environment.RedTeamSkin = block[offset++];
        environment.BlueTeamSkin = block[offset++];
        block.Slice(offset, 16).CopyTo(environment.WeaponRestrictions);
        offset += 16;
        environment.MaximumPlayers = block[offset++];
        environment.CurrentPlayers = block[offset++];
        environment.BriefingTime = ReadInt32(block, offset);
        offset += 4;

        // Reserved run, the live round number, another reserved run, the round
        // timer and a last reserved run: skipped, so the read stays faithful to
        // the writer's zeroes rather than inventing values for fields this codec
        // does not model.
        offset += 10;  // Reserved run at 0x30.
        offset += 2;   // Live round number.
        offset += 4;   // Reserved run at 0x36.
        offset += 4;   // Live round timer.
        offset += 2;   // Reserved run at 0x3c.

        environment.Stance = block[offset++];
        environment.LevelLimitTolerance = block[offset++];
        environment.StandardRateOrLevelLimitBase = ReadInt32(block, offset);
        offset += 4;

        environment.SneakingTime = ReadInt32(block, offset); offset += 4;
        environment.SneakingRounds = ReadInt32(block, offset); offset += 4;
        environment.CaptureTime = ReadInt32(block, offset); offset += 4;
        environment.CaptureRounds = ReadInt32(block, offset); offset += 4;
        environment.RescueTime = ReadInt32(block, offset); offset += 4;
        environment.RescueRounds = ReadInt32(block, offset); offset += 4;
        environment.TeamDeathmatchTime = ReadInt32(block, offset); offset += 4;
        environment.TeamDeathmatchRounds = ReadInt32(block, offset); offset += 4;
        environment.TeamDeathmatchTickets = ReadInt32(block, offset); offset += 4;
        environment.DeathmatchTime = ReadInt32(block, offset); offset += 4;
        environment.DeathmatchTickets = ReadInt32(block, offset); offset += 4;
        environment.BaseTime = ReadInt32(block, offset); offset += 4;
        environment.BaseRounds = ReadInt32(block, offset); offset += 4;
        environment.BombTime = ReadInt32(block, offset); offset += 4;
        environment.BombRounds = ReadInt32(block, offset); offset += 4;
        environment.TeamSneakingTime = ReadInt32(block, offset); offset += 4;
        environment.TeamSneakingRounds = ReadInt32(block, offset); offset += 4;

        environment.UniqueRed = block[offset++];
        environment.UniqueBlue = block[offset++];
        offset += 2;  // Continuous wins.
        offset += 4;  // Continuous winner.
        offset += 1;  // Legacy game-phase flags.
        environment.CommonA = block[offset++];
        environment.CommonB = block[offset++];
        environment.TeamBalance = block[offset++];
        environment.IdleKick = ReadUInt16(block, offset); offset += 2;
        environment.TeamKillKick = ReadUInt16(block, offset); offset += 2;
        environment.NetworkStatus = ReadInt32(block, offset); offset += 4;
        environment.CaptureExtraTime = block[offset++];
        environment.SneakingSnakeKills = block[offset++];
        environment.StealthDeathmatchTime = block[offset++];
        environment.StealthDeathmatchRounds = block[offset++];
        environment.IntervalTime = block[offset++];
        environment.DeathmatchRounds = block[offset++];
        environment.SoloCaptureTime = block[offset++];
        environment.SoloCaptureRounds = block[offset++];
        environment.RaceTime = block[offset++];
        environment.RaceRounds = block[offset++];
        environment.Field0C6 = block[offset++];
        environment.HostOptionsExtraTimeFlags = block[offset];
        return environment;
    }

    private static int ReadInt32(ReadOnlySpan<byte> block, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(block[offset..]);

    private static int ReadUInt16(ReadOnlySpan<byte> block, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(block[offset..]);
}
