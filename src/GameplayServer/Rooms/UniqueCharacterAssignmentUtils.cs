using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The two player slots a round assigns its unique characters to, or <c>0x7f</c>
/// for a slot that holds none.
/// </summary>
/// <param name="Red">Slot of the first unique character.</param>
/// <param name="Blue">Slot of the second unique character.</param>
public readonly record struct UniqueCharacterSlots(byte Red, byte Blue)
{
    /// <summary>The pair a round that assigns none writes: <c>7f 7f</c>.</summary>
    public static UniqueCharacterSlots Unassigned { get; } = new(
        RoomStateBodyConstants.UnassignedUniqueCharacterSlot,
        RoomStateBodyConstants.UnassignedUniqueCharacterSlot);
}

/// <summary>
/// Assigns the two unique-character slots with the client's own selection.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client's picker is <c>P2P_AssignRoleSlots</c> at <c>0x77b628</c>.</b> The
/// start-round sequence calls it once per round (<c>0x7828e4</c>), and it advances a
/// 32-bit LCG with <c>state = state * 0x5d588b65 + 1</c>, draws an index with
/// <c>(draw + state + settings+0xa1) % count</c>, takes two values and redraws the
/// second while it repeats the first. The two values are written to object
/// <c>0x9e</c>, body <c>0x9a</c>/<c>0x9b</c>.
/// </para>
/// <para>
/// <b>The candidates are the room's player slots.</b> The client's candidate list is
/// filled by its own roster; a server has no such list, so it takes the slots the
/// room's loadouts publish. The rule gate is the capture's: the pair appears only in
/// Sneaking and Team Sneaking, so every other rule writes <c>7f 7f</c>.
/// </para>
/// <para>
/// <b>The draw is the client's clock, not a random value of its own.</b>
/// <c>_opd_FUN_0006f010</c> converts the frame counter at 1/59.94 and adds the base
/// time, so it is the current time in seconds; the picker adds it to the LCG step and
/// the settings generation. The LCG state is the client's global at <c>0x012296d0</c>,
/// which the ELF seeds with <c>1</c>; this server keeps a process-wide state from that
/// seed and advances it once per draw. The client shares that state with every other
/// <c>rand()</c> call, so its value at a round depends on calls this server never
/// makes; the seed, the step and the index are the game's, the sequence is the
/// server's own.
/// </para>
/// </remarks>
public static class UniqueCharacterAssignmentUtils
{
    private const uint LcgMultiplier = 0x5d588b65;
    private const uint LcgIncrement = 1;

    // The client's state at 0x012296d0 holds 1 in the ELF, so the server starts from
    // the seed the game's own rand() state begins with.
    private const uint LcgSeed = 1;

    private static readonly object StateLock = new();
    private static uint lcgState = LcgSeed;

    /// <summary>
    /// The pair a round writes: two distinct player slots when the room enables the
    /// setting on a Sneaking or Team Sneaking round, and <c>7f 7f</c> otherwise.
    /// </summary>
    /// <param name="loadouts">The room's players, whose slots are the candidates.</param>
    /// <param name="rotation">The room's rotation, or <c>null</c> when it is unknown.</param>
    /// <param name="currentRotationIndex">Slot of the round being briefed or played.</param>
    /// <param name="enabled">Whether the room's unique-character setting is on.</param>
    /// <param name="settingsGeneration">The room's settings generation at body <c>0x9d</c>.</param>
    /// <returns>The pair to write.</returns>
    public static UniqueCharacterSlots Assign(
        IReadOnlyList<LobbyLoadout>? loadouts,
        IReadOnlyList<MatchRotationEntry>? rotation,
        int currentRotationIndex,
        bool enabled,
        byte settingsGeneration)
    {
        if (!enabled || loadouts is not { Count: > 0 } || rotation is not { Count: > 0 })
        {
            return UniqueCharacterSlots.Unassigned;
        }

        var slot = ((currentRotationIndex % rotation.Count) + rotation.Count) % rotation.Count;
        if (rotation[slot].Rule is not (MatchRuleUtils.Sneaking or MatchRuleUtils.TeamSneaking))
        {
            return UniqueCharacterSlots.Unassigned;
        }

        var candidates = loadouts
            .Select(loadout => loadout.Slot)
            .Distinct()
            .OrderBy(playerSlot => playerSlot)
            .ToArray();

        if (candidates.Length == 0)
        {
            return UniqueCharacterSlots.Unassigned;
        }

        var first = Draw(candidates, settingsGeneration);

        if (candidates.Length == 1)
        {
            return new UniqueCharacterSlots(
                first,
                RoomStateBodyConstants.UnassignedUniqueCharacterSlot);
        }

        byte second;
        do
        {
            second = Draw(candidates, settingsGeneration);
        }
        while (second == first);

        return new UniqueCharacterSlots(first, second);
    }

    private static byte Draw(byte[] candidates, byte settingsGeneration)
    {
        uint state;
        lock (StateLock)
        {
            lcgState = unchecked((lcgState * LcgMultiplier) + LcgIncrement);
            state = lcgState;
        }

        // `0x6f010` is the client's session clock: the frame counter at 1/59.94 plus
        // the base time, which is the current time in seconds. The picker adds it to
        // the LCG step and the settings generation.
        var draw = unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var index = (int)((draw + state + settingsGeneration) % (uint)candidates.Length);
        return candidates[index];
    }
}
