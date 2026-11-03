using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The contiguous regions of the 161-byte room-state body, written in body order.
/// </summary>
/// <remarks>
/// <para>
/// Every method here appends exactly the bytes of one run of the body and documents
/// its offsets, so a byte seen on the wire can be matched to the writer that produced
/// it. What each region is measured to mean, and what is still open, is in
/// <c>docs/analysis/rotation-payload-corpus.md</c>.
/// </para>
/// <para>
/// <b>The pinned values are the working capture's.</b> Every field this server does
/// not derive — fields 3 to 8, 13 and 17 to 27, the tail entries of field 29, fields
/// 30 to 49 and the closing four — is written with the value
/// <c>.pcapngs/mgo2-started-briefing-then-solo-game.pcapng</c> carries at t+0, so the
/// body is that capture's body field for field apart from the rotation, the current
/// slot and the population, which are the room's own.
/// </para>
/// </remarks>
internal static class RoomStateRegionUtils
{
    /// <summary>Bytes in one rotation entry: map, rule and the third component.</summary>
    private const int RotationTripleLength = 3;

    /// <summary>
    /// Body <c>0x4a</c>: field 3. The captured rooms write <c>0x0d</c>, <c>0x0f</c> or
    /// <c>0x11</c> here and nothing in the corpus says which room gets which.
    /// </summary>
    private const byte Field3 = 0x0f;

    /// <summary>Body <c>0x4b</c>: field 4, the same <c>0x00b4</c> in every captured room.</summary>
    private const ushort Field4 = 0x00b4;

    /// <summary>Body <c>0x4d</c>: field 5, <c>3</c> in twelve captured rooms and <c>2</c> in the two six-slot ones.</summary>
    private const byte Field5 = 0x02;

    /// <summary>Body <c>0x4e</c>: field 6, <c>3</c> in twelve captured rooms and <c>6</c> in the TSNE one.</summary>
    private const byte Field6 = 0x03;

    /// <summary>
    /// Body <c>0x4f</c>–<c>0x52</c>: field 7, a word of flags rather than a number.
    /// </summary>
    /// <remarks>
    /// The client tests three bits of it: <c>0x20</c> at five call sites, <c>0x04</c> at
    /// one, and <c>0x40</c> in a getter that returns it as a boolean
    /// (<c>docs/analysis/rotation-payload-corpus.md</c> §5). Which room condition each
    /// bit carries is still unnamed, so the working capture's word is written.
    /// </remarks>
    private const uint StateFlagsWord = 0x0007622a;

    /// <summary>Body <c>0x53</c>–<c>0x54</c>: field 8, the room's own and unnamed.</summary>
    private const ushort Field8 = 0x00b4;

    /// <summary>
    /// The selector field 13 and field 53 both carry.
    /// </summary>
    /// <remarks>
    /// Native readers use bit 0 of field 53 to choose between two entries in a global
    /// table, and field 13 holds the same byte at the head of the block in every
    /// captured room, so the two offsets are written from one constant rather than
    /// from two literals that can drift apart.
    /// </remarks>
    private const byte SelectorFlag = 0x00;

    /// <summary>
    /// Body <c>0x57</c>–<c>0x64</c>: state fields 13 and 17 to 27, the room's own.
    /// </summary>
    /// <remarks>
    /// The first byte is <see cref="SelectorFlag"/>, which field 53 repeats. The rest
    /// move with the room — <c>0x60</c> is <c>2</c> in most rooms and <c>0</c> in the
    /// all-default ones, <c>0x63</c> is <c>2</c>, <c>3</c>, <c>6</c> or <c>0</c>,
    /// <c>0x64</c> is <c>10</c> or <c>0</c> — and keep the working capture's bytes.
    /// </remarks>
    private static readonly byte[] ReferenceStateFields17To27 =
    [
        0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x28,
        0x00, 0x02, 0x01, 0x00, 0x02, 0x0a,
    ];

    /// <summary>
    /// Body <c>0x65</c>–<c>0x86</c>: state field 29, seventeen rule times in seconds.
    /// </summary>
    /// <remarks>
    /// Elements 0 to 7 are the eight standard rules out of the room's timer bank;
    /// elements 8 to 11 are the room family's own four words (here
    /// <c>0xd003</c>, <c>0xaca0</c>, <c>0x2400</c> and <c>0x0022</c>) whose meaning is
    /// open; elements 12 to 16 are the times of rules 12 to 16 by rule id (SDM,
    /// INTERVAL, ANEST, SCAP, RACE), which the lobby states in a reply this server
    /// never receives. The working capture's vector is below, and a live room's first
    /// eight elements come from its bank instead.
    /// </remarks>
    private static readonly ushort[] ReferenceTimerVector =
    [
        300, 420, 180, 300, 540, 300, 300, 240,
        53251, 44192, 9216, 34,
        180, 5940, 0, 300, 300,
    ];

    /// <summary>Body <c>0x87</c>–<c>0x9b</c>: state fields 30 to 49.</summary>
    /// <remarks>
    /// The last two bytes are slot references with <c>0x7f</c> as their unassigned
    /// value; the other nineteen move with the room.
    /// </remarks>
    private static readonly byte[] ReferenceStateFields30To49 =
    [
        0x02, 0x02, 0x04, 0x02, 0x02, 0x02, 0x04, 0x00, 0x02, 0x04, 0x02,
        0x02, 0x28, 0x63, 0x00, 0x03, 0x00, 0x01, 0x01, 0x7f, 0x7f,
    ];

    /// <summary>Body <c>0x9c</c>: field 50, the unassigned slot reference.</summary>
    private const byte UnassignedSlotReference = 0xff;

    /// <summary>Body <c>0x9d</c>: field 51, the working capture's byte; its meaning is open.</summary>
    private const byte Field51 = 0x7e;

    /// <summary>Body <c>0x9e</c>–<c>0x9f</c>: field 52, the working capture's word; its meaning is open.</summary>
    private const ushort Field52 = 0x0ee5;

    /// <summary>
    /// The timer-bank position each of the eight standard rules owns.
    /// </summary>
    /// <remarks>
    /// The bank is in the lobby reply's order, not rule-id order, so the eight
    /// positions are <see cref="MatchRotationUtils.TimeIndex"/> of the rules this
    /// server accepts.
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
    /// <param name="body">The body being built.</param>
    internal static void WritePresenceMap(List<byte> body)
    {
        body.Add(PlayerSettingsBurstService.SettingsPayloadType);
        body.Add(PlayerSettingsBurstService.RoomRotationSlot);
        body.Add(0xff);
        body.Add(0x27);
        body.Add(0xfe);
        body.Add(0xee);
        body.Add(0xff);
        body.Add(0xff);
        body.Add(0x3f);
    }

    /// <summary>Body <c>0x09</c>–<c>0x19</c>: the round phase, then sixteen reserved bytes.</summary>
    /// <remarks>
    /// The phase is the room's own. The client compares it with <c>0</c>, <c>3</c> and
    /// <c>7</c>; the corpus only ever carries <c>2</c> to <c>5</c>.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="roundNumber">The room's round-phase value.</param>
    internal static void WriteRoundPhase(List<byte> body, byte roundNumber)
    {
        body.Add(roundNumber);
        WriteZeroBytes(body, 16);
    }

    /// <summary>
    /// Body <c>0x1a</c>–<c>0x49</c>: sixteen rotation triples of map, rule and flags.
    /// </summary>
    /// <remarks>
    /// The client reads this block as sixteen entries of three bytes and selects one
    /// with the slot index at <c>0x55</c>; an unused slot is three zero bytes.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="rotation">The room's rotation, entry zero first.</param>
    internal static void WriteRotationTriples(List<byte> body, IReadOnlyList<MatchRotationEntry> rotation)
    {
        for (var slot = 0; slot < RoomRotationPayloadUtils.RotationEntryCount; slot++)
        {
            if (slot >= rotation.Count)
            {
                WriteZeroBytes(body, RotationTripleLength);
                continue;
            }

            var entry = rotation[slot];
            body.Add(Component(nameof(entry.Map), entry.Map));
            body.Add(Component(nameof(entry.Rule), entry.Rule));
            body.Add(Component(nameof(entry.Flags), entry.Flags));
        }
    }

    /// <summary>Body <c>0x4a</c>–<c>0x54</c>: state fields 3 to 8, the room's own and unnamed.</summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteRoomStateFields(List<byte> body)
    {
        body.Add(Field3);
        WriteUInt16LittleEndian(body, Field4);
        body.Add(Field5);
        body.Add(Field6);
        WriteUInt32LittleEndian(body, StateFlagsWord);
        WriteUInt16LittleEndian(body, Field8);
    }

    /// <summary>Body <c>0x55</c>–<c>0x56</c>: the slot the client reads, and the population.</summary>
    /// <remarks>
    /// The client's own object keeps these as one byte each: the slot index at
    /// <c>0x52</c> and the rotation block at <c>0x15</c>. Getter
    /// <c>system.dll:0x8eb0c0</c> returns the selected triple's rule,
    /// <c>0x8eb148</c> its third component, and <c>0x27bb10</c> wraps the slot to zero
    /// past index 14.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="currentRotationIndex">The slot the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    internal static void WriteCurrentSlotAndPopulation(List<byte> body, int currentRotationIndex, byte playerCount)
    {
        body.Add((byte)currentRotationIndex);
        body.Add(playerCount);
    }

    /// <summary>Body <c>0x57</c>–<c>0x64</c>: state fields 13 and 17 to 27, the room's own.</summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteStateFields17To27(List<byte> body)
    {
        body.Add(SelectorFlag);
        foreach (var value in ReferenceStateFields17To27)
        {
            body.Add(value);
        }
    }

    /// <summary>Body <c>0x65</c>–<c>0x86</c>: state field 29, seventeen rule times in seconds.</summary>
    /// <param name="body">The body being built.</param>
    /// <param name="ruleTimers">The room's bank, or <c>null</c> for the reference body.</param>
    internal static void WriteRuleTimerVector(List<byte> body, IReadOnlyList<int>? ruleTimers)
    {
        if (ruleTimers is null)
        {
            foreach (var seconds in ReferenceTimerVector)
            {
                WriteUInt16LittleEndian(body, seconds);
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

            WriteUInt16LittleEndian(body, minutes * 60);
        }

        // Elements 8-11 are the room family's own four words; elements 12-16 are the
        // times of rules 12-16, which the lobby states in a reply this server never
        // receives, so a live room writes zero and the reference 300 respectively.
        for (var index = 0; index < 4; index++)
        {
            WriteUInt16LittleEndian(body, 0);
        }

        for (var index = 0; index < 5; index++)
        {
            WriteUInt16LittleEndian(body, 300);
        }
    }

    /// <summary>Body <c>0x87</c>–<c>0x9b</c>: state fields 30 to 49, incl. two slot references.</summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteSlotReferencesAndFlags(List<byte> body)
    {
        foreach (var value in ReferenceStateFields30To49)
        {
            body.Add(value);
        }
    }

    /// <summary>
    /// Body <c>0x9c</c>–<c>0xa0</c>: the unassigned slot reference (field 50), the
    /// opaque field 51 and 52 pair, and field 53, which repeats field 13's selector.
    /// </summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteTailSelector(List<byte> body)
    {
        body.Add(UnassignedSlotReference);
        body.Add(Field51);
        WriteUInt16LittleEndian(body, Field52);

        // Native readers use bit 0 to select between two entries in a global table,
        // and field 13 repeats this byte at the head of the block.
        body.Add(SelectorFlag);
    }

    private static void WriteZeroBytes(List<byte> destination, int count)
    {
        for (var index = 0; index < count; index++)
        {
            destination.Add(0);
        }
    }

    private static void WriteUInt16LittleEndian(List<byte> destination, int value)
    {
        if (value is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The state field is an unsigned 16-bit value.");
        }

        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
    }

    private static void WriteUInt32LittleEndian(List<byte> destination, uint value)
    {
        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
        destination.Add((byte)(value >> 16));
        destination.Add((byte)(value >> 24));
    }

    private static byte Component(string field, int value) =>
        value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new ArgumentOutOfRangeException(field, value, "A rotation component is one byte.");
}
