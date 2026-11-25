using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The framed regions of the 161-byte room-state body: its header and presence map, its
/// phase, its rotation block, its current slot and population, and its rule times.
/// </summary>
/// <remarks>
/// <para>
/// The body's single-valued fields are written by <see cref="RoomStateFieldUtils"/>; the
/// values they carry when the server has no room fact are in
/// <see cref="RoomStateBodyConstants"/>. What each region is measured to mean, and
/// what is still open, is in <c>docs/analysis/rotation-payload-corpus.md</c>.
/// </para>
/// <para>
/// Every method here appends exactly the bytes of one run of the body and documents its
/// offsets, so a byte seen on the wire can be matched to the writer that produced it.
/// </para>
/// </remarks>
internal static class RoomStateRegionUtils
{
    /// <summary>Bytes in one rotation entry: map, rule and the third component.</summary>
    private const int RotationTripleLength = 3;

    /// <summary>
    /// The timer-bank position each of the eight standard rules owns.
    /// </summary>
    /// <remarks>
    /// The bank is in the lobby reply's order, not rule-id order, so the eight positions
    /// are <see cref="MatchRotationUtils.TimeIndex"/> of the rules this server accepts.
    /// </remarks>
    private static readonly int[] StandardRuleTimerBankIndexes =
    [
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Deathmatch),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.TeamDeathmatch),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Rescue),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Capture),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Sneaking),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Base),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Bomb),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.TeamSneaking),
    ];

    /// <summary>Body <c>0x00</c>–<c>0x08</c>: the record's own header and presence map.</summary>
    /// <remarks>
    /// The map's seven bytes are the client's own field-presence bitmap, LSB first over
    /// the 55 fields of the state-<c>0x00</c> table. The set bits are the fields at
    /// object <c>0x00</c>–<c>0x07</c>; <c>0x08</c>, <c>0x09</c>, <c>0x0a</c> and
    /// <c>0x0d</c>; <c>0x11</c>–<c>0x17</c>; <c>0x19</c>–<c>0x1b</c>, <c>0x1d</c>–
    /// <c>0x1f</c>; <c>0x20</c>–<c>0x27</c>; <c>0x28</c>–<c>0x2f</c>; and
    /// <c>0x30</c>–<c>0x35</c>. The clear bits are the seven fields the body omits —
    /// <c>0x54</c>, <c>0x56</c>, <c>0x59</c>, the 8-byte <c>0x5a</c>, the stride-2
    /// <c>0x5a</c>, <c>0x63</c> and <c>0xa5</c> — which is why the object-to-body offset
    /// moves between the regions.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    internal static void WritePresenceMap(List<byte> body)
    {
        body.Add(PlayerSettingsBurstService.SettingsPayloadType);
        body.Add(PlayerSettingsBurstService.RoomRotationSlot);
        body.Add(0xff); // fields 0x00-0x07
        body.Add(0x27); // 0x08, 0x09, 0x0a, 0x0d
        body.Add(0xfe); // 0x11-0x17
        body.Add(0xee); // 0x19-0x1b, 0x1d-0x1f
        body.Add(0xff); // 0x20-0x27
        body.Add(0xff); // 0x28-0x2f
        body.Add(0x3f); // 0x30-0x35
    }

    /// <summary>Body <c>0x09</c>–<c>0x19</c>: the room state, then sixteen reserved bytes.</summary>
    /// <remarks>
    /// The state is the room's own and is the same value state <c>0x00</c>'s field 0
    /// carries on its own (<see cref="RoomState"/>). The client compares it with
    /// <c>0</c>, <c>3</c> and <c>7</c>; the captures carry <c>2</c> to <c>5</c>.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="state">The room state.</param>
    internal static void WriteRoundPhase(List<byte> body, RoomState state)
    {
        body.Add((byte)state);
        StateBodyWriterUtils.WriteZeroBytes(body, 16);
    }

    /// <summary>
    /// Body <c>0x1a</c>–<c>0x49</c>: sixteen rotation triples of map, rule and options.
    /// </summary>
    /// <remarks>
    /// The client reads this block as sixteen entries of three bytes and selects one
    /// with the slot index at <c>0x55</c>; an unused slot is three zero bytes. The
    /// third byte is the round's <see cref="Mgo2Server.Shared.Types.RoundOption"/> —
    /// <c>0x02</c> on a Drebin Points round and <c>0x00</c> on an ordinary one, which
    /// is the difference a Drebin-disabled capture shows against an enabled one.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="rotation">The room's rotation, entry zero first.</param>
    internal static void WriteRotationTriples(List<byte> body, IReadOnlyList<MatchRotationEntry> rotation)
    {
        for (var slot = 0; slot < RoomRotationPayloadUtils.RotationEntryCount; slot++)
        {
            if (slot >= rotation.Count)
            {
                StateBodyWriterUtils.WriteZeroBytes(body, RotationTripleLength);
                continue;
            }

            var entry = rotation[slot];
            body.Add(Component(nameof(entry.Map), entry.Map));
            body.Add(Component(nameof(entry.Rule), entry.Rule));
            body.Add(Component(nameof(entry.RoundOptions), (int)entry.RoundOptions));
        }
    }

    /// <summary>Body <c>0x55</c>–<c>0x56</c>: the slot the client reads, and the population.</summary>
    /// <remarks>
    /// The client's own object keeps these as one byte each: the slot index at
    /// <c>0x52</c> and the rotation block at <c>0x15</c>. Getter
    /// <c>system.dll:0x8eb0c0</c> returns the selected triple's map — it indexes
    /// <c>slot * 3</c> — <c>0x8eb148</c> its rule (<c>slot * 3 + 1</c>), and
    /// <c>0x27bb10</c> wraps the slot to zero past index 14.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="currentRotationIndex">The slot the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    internal static void WriteCurrentSlotAndPopulation(List<byte> body, int currentRotationIndex, byte playerCount)
    {
        body.Add((byte)currentRotationIndex);
        body.Add(playerCount);
    }

    /// <summary>Body <c>0x65</c>–<c>0x86</c>: seventeen rule times in seconds.</summary>
    /// <param name="body">The body being built.</param>
    /// <param name="ruleTimers">The room's bank, or <c>null</c> for the reference body.</param>
    internal static void WriteRuleTimerVector(List<byte> body, IReadOnlyList<int>? ruleTimers)
    {
        if (ruleTimers is null)
        {
            foreach (var slot in Enum.GetValues<RuleTimerSlot>())
            {
                StateBodyWriterUtils.WriteUInt16LittleEndian(
                    body,
                    RoomStateBodyConstants.RuleTimerSeconds[slot]);
            }

            return;
        }

        foreach (var timerIndex in StandardRuleTimerBankIndexes)
        {
            var minutes = ruleTimers[timerIndex];
            if (minutes < 0 || minutes > ushort.MaxValue / 60)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ruleTimers),
                    minutes,
                    "A state-field rule timer must fit in an unsigned 16-bit count of seconds.");
            }

            StateBodyWriterUtils.WriteUInt16LittleEndian(body, minutes * 60);
        }

        // Elements 8-11 are the slots for rule ids 8-11, which no shipped rule uses and
        // which are no room fact, so a live room writes zero. Elements 12-16 are rules
        // 12-16's times from a reply this server never receives, so a live room writes the
        // reference 300 rather than inventing a source.
        for (var index = 0; index < 4; index++)
        {
            StateBodyWriterUtils.WriteUInt16LittleEndian(body, 0);
        }

        for (var index = 0; index < 5; index++)
        {
            StateBodyWriterUtils.WriteUInt16LittleEndian(body, 300);
        }
    }

    private static byte Component(string field, int value) =>
        value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new ArgumentOutOfRangeException(field, value, "A rotation component is one byte.");
}
