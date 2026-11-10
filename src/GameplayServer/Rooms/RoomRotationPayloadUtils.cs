using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The 161-byte room-state body carried by a <c>0x1001</c> settings record.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is measured.</b> Field 9 (<c>0x55</c>) is the current rotation slot and
/// field 10 (<c>0x56</c>) the room population, host included; field 29 is the
/// room's rule times in seconds. The rest are written from the working capture and
/// named by offset, because no reader in the client names them
/// (<c>docs/analysis/rotation-payload-corpus.md</c>).
/// </para>
/// <para>
/// <b>What the client confirms.</b> The rotation the two readers select from is a
/// 48-byte block of sixteen triples plus a one-byte slot index: <c>system.dll</c>'s
/// getters at <c>0x8eb0c0</c> and <c>0x8eb148</c> fetch 48 bytes at object offset
/// <c>0x15</c> and one byte at <c>0x52</c> through the same accessor
/// (<c>0x27e480</c>), then return triple <c>[slot][1]</c> and <c>[slot][2]</c> —
/// the round's rule and its third component. So a triple is three bytes and the
/// slot index is one, which is what this writer emits.
/// </para>
/// <para>
/// This file owns the entry points and the validation; the regions themselves are
/// written by <see cref="RoomStateRegionUtils"/>, one method per contiguous run of
/// the body and each documented with its own offsets.
/// </para>
/// </remarks>
internal static class RoomRotationPayloadUtils
{
    public const int PayloadLength = 161;

    /// <summary>Slots in the rotation field, of which fifteen play and one is zeroed.</summary>
    internal const int RotationEntryCount = 16;

    private const byte ReferenceRoundNumber = 2;
    private const int ReferenceRotationIndex = 4;
    private const byte ReferencePlayerCount = 2;

    private static readonly MatchRotationEntry[] ReferenceRotation =
    [
        new(Rule: 5, Map: 12, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 6, Map: 4, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 3, Map: 5, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 16, Map: 2, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 3, Map: 7, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 1, Map: 3, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 2, Map: 14, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 1, Map: 1, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
        new(Rule: 5, Map: 6, RoundOptions: RoundOption.Normal, TimeLimitMinutes: 5),
    ];

    public static byte[] BuildReferenceBody() =>
        BuildCore(
            ReferenceRotation,
            ReferenceRoundNumber,
            ReferenceRotationIndex,
            ReferencePlayerCount,
            ruleTimers: null);

    /// <summary>
    /// The body for a room this host serves: its rotation, its round phase, the
    /// slot it is playing and its population.
    /// </summary>
    /// <remarks>
    /// <b>None of the three state fields is a constant of the format.</b> The
    /// client reads the slot at <c>0x55</c> and names its round by that triple's
    /// map byte — the getter at <c>system.dll 0x8eb0c0</c> returns
    /// <c>rotation_data[slot * 3]</c> — and the walk at <c>0x27bb10</c> wraps the
    /// slot to zero when that byte is zero. So a body pinned to slot <c>0</c> tells
    /// a room on its fifth entry that it is playing the first, and one pinned to a
    /// population of two tells a room of nine it holds two.
    /// </remarks>
    /// <param name="rotation">The room's rotation, entry zero first.</param>
    /// <param name="roundNumber">The room's round-phase byte.</param>
    /// <param name="currentRotationIndex">Slot of the round the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    /// <returns>The body.</returns>
    public static byte[] BuildReferenceBody(
        IReadOnlyList<MatchRotationEntry> rotation,
        byte roundNumber,
        int currentRotationIndex,
        byte playerCount) =>
        BuildCore(rotation, roundNumber, currentRotationIndex, playerCount, ruleTimers: null);

    public static byte[] Build(
        IReadOnlyList<MatchRotationEntry> rotation,
        byte roundNumber,
        int currentRotationIndex,
        byte playerCount,
        IReadOnlyList<int> ruleTimers)
    {
        ArgumentNullException.ThrowIfNull(ruleTimers);
        return BuildCore(rotation, roundNumber, currentRotationIndex, playerCount, ruleTimers);
    }

    private static byte[] BuildCore(
        IReadOnlyList<MatchRotationEntry> rotation,
        byte roundNumber,
        int currentRotationIndex,
        byte playerCount,
        IReadOnlyList<int>? ruleTimers)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        if (rotation.Count == 0)
        {
            throw new ArgumentException("A room's rotation must contain at least one entry.", nameof(rotation));
        }

        // The slot has to name one of the triples this body writes. The client
        // resolves the round through that triple's map byte and falls back to slot
        // zero when the byte is zero (system.dll 0x27bb10), so a slot past the
        // rotation would be a round no client can name.
        currentRotationIndex %= rotation.Count;

        if (currentRotationIndex is < 0 or >= RotationEntryCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentRotationIndex),
                currentRotationIndex,
                $"The current rotation index must fit the {RotationEntryCount}-slot field.");
        }

        if (ruleTimers is not null && ruleTimers.Count != MatchRotationUtils.RuleTimerCount)
        {
            throw new ArgumentException(
                $"The room's timer bank must contain {MatchRotationUtils.RuleTimerCount} values.",
                nameof(ruleTimers));
        }

        List<byte> body = new(PayloadLength);

        RoomStateRegionUtils.WritePresenceMap(body);
        RoomStateRegionUtils.WriteRoundPhase(body, roundNumber);
        RoomStateRegionUtils.WriteRotationTriples(body, rotation);
        RoomStateRegionUtils.WriteRoomStateFields(body);
        RoomStateRegionUtils.WriteCurrentSlotAndPopulation(body, currentRotationIndex, playerCount);
        RoomStateRegionUtils.WriteStateFields17To27(body);
        RoomStateRegionUtils.WriteRuleTimerVector(body, ruleTimers);
        RoomStateRegionUtils.WriteSlotReferencesAndFlags(body);
        RoomStateRegionUtils.WriteTailSelector(body);

        if (body.Count != PayloadLength)
        {
            throw new InvalidOperationException(
                $"The state-field writer produced {body.Count} bytes instead of {PayloadLength}.");
        }

        return body.ToArray();
    }
}
