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
    /// Body <c>0x4a</c>: field 3, the room's <c>Max Number of Characters</c>.
    /// </summary>
    /// <remarks>
    /// The client's own state builder loads the game-settings block's <c>+66</c> — the
    /// byte <c>0x4313</c> calls <c>max_players</c> — and writes it to this slot, which
    /// is what settles the older <c>Level-Limit</c> reading: the level limit is a
    /// different field of the same block (<c>+95</c>/<c>+96</c>) and is never read on
    /// this path. It is the room's and not its host's, which is why
    /// <c>129.153.74.111:5720</c> writes <c>0x11</c> for its sixteen-player room and
    /// <c>0x0f</c> for the six-player one
    /// (<c>docs/analysis/rotation-payload-corpus.md</c>).
    /// </remarks>
    private const byte MaximumCharacters = 0x0f;

    /// <summary>
    /// Body <c>0x4b</c>: field 4, <c>0x00b4</c> in every captured room because the
    /// client writes it itself — <c>li r0,0xb4</c> at <c>0x9e9500</c> and then
    /// <c>sth</c> into its object at <c>0x46</c>. It has provenance and no reading: the
    /// value is the client's, so it is not a setting this server can derive, and what
    /// the client means by 180 is not established.
    /// </summary>
    private const ushort ClientFixedValue = 0x00b4;

    /// <summary>
    /// Body <c>0x4d</c>: field 5, <c>0x02</c> in twenty-one of the twenty-two captured
    /// bodies.
    /// </summary>
    /// <remarks>
    /// The exception is a room held at its briefing, which writes <c>0x14</c>
    /// (<c>20</c>) while the same host's other bodies write <c>0x02</c>, so the byte
    /// is the room's state rather than one of its settings. The <c>3</c>/<c>2</c>/
    /// <c>6</c> this comment used to give belongs to field 6 at <c>0x4e</c>.
    /// </remarks>
    private const byte Field5 = 0x02;

    /// <summary>
    /// Body <c>0x4e</c>: field 6, the room's <c>Number of Rounds</c> — <c>3</c> in
    /// nineteen captured bodies, <c>2</c> in the two six-slot rooms of one host, and
    /// <c>6</c> in the TSNE room.
    /// </summary>
    /// <remarks>
    /// Read, not measured: the screen's own label is <c>Number of Rounds</c> at
    /// <c>scenerio.gcx:0x0ccf8f</c>, and a room holds one value across all of its
    /// bodies. Nothing on the gameplay wire counts rounds against it.
    /// </remarks>
    private const byte NumberOfRounds = 0x03;

    /// <summary>
    /// Body <c>0x4f</c>–<c>0x52</c>: field 7, a word of flags rather than a number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client tests three bits of it: <c>0x20</c> at five call sites, <c>0x04</c> at
    /// one, and <c>0x40</c> in a getter that returns it as a boolean
    /// (<c>docs/analysis/rotation-payload-corpus.md</c> §5). The two bits with a reading
    /// are members of <see cref="RoomCommonSettingsFlag"/> — <c>0x02</c> Drebin Points,
    /// which the captured word sets, and <c>0x40</c> asking a joiner which team to join,
    /// which it leaves clear.
    /// </para>
    /// <para>
    /// <b>It is the client's own common-settings word, written and not only read.</b> The
    /// gameplay client hosts matches, and its state builder assembles this word from the
    /// room record's <c>+0x3a0</c> bitfield with <c>ori</c>s of bits 9, 12, 13, 14, 15,
    /// 16 and 17; the Create Game summary binds exactly those positions to
    /// <c>Auto Assign Teams</c>, <c>Level Limit</c> (enable), <c>Allow Quick-Join</c>,
    /// <c>Voice Chat</c>, <c>Team Kill Kick</c>, <c>Idle Kick</c> and one unrendered row,
    /// and they are the members of <see cref="RoomCommonSettingsFlag"/>. The
    /// level-limit *base* the enable gates is not on this wire at all — it is a different
    /// field of the settings block (<c>+96</c>) that this builder never reads. Bits 0-7
    /// vary per room and no instruction in the image tests them, so they stay part of the
    /// pinned word.
    /// </para>
    /// </remarks>
    private const RoomCommonSettingsFlag CommonSettingsSwitches = (RoomCommonSettingsFlag)0x0007622a;

    /// <summary>
    /// Body <c>0x53</c>–<c>0x54</c>: field 8, a whole number of minutes held as seconds.
    /// </summary>
    /// <remarks>
    /// Every captured room writes one of <c>0</c>, <c>180</c>, <c>240</c> or <c>300</c>
    /// and holds it across all of that room's own bodies
    /// (<see cref="RoomMinuteLimit"/>), so it is a limit the room set on something the
    /// wire does not name. The screen's minutes are <c>Round Time</c> and
    /// <c>Briefing Time</c>, and the gameplay bytes cannot separate the two. Field 4 is
    /// the only other offset here that never leaves a minute boundary.
    /// </remarks>
    private const RoomMinuteLimit MinuteLimitSeconds = RoomMinuteLimit.ThreeMinutes;

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

    /// <summary>Body <c>0x9c</c>: field 50, the unassigned slot reference.</summary>
    private const byte UnassignedSlotReference = 0xff;

    /// <summary>
    /// Body <c>0x9d</c>: the tail byte that belongs to the capture rather than the room.
    /// </summary>
    /// <remarks>
    /// It moves between bodies of one room — <c>64.181.216.250:5720</c> writes
    /// <c>140</c>, <c>232</c> and <c>233</c> — where the word beside it does not, so the
    /// pair is two fields. It is not a checksum of the body: a byte sum, a negated sum,
    /// a <c>u16</c> sum, a byte XOR and a CRC16 over every prefix ending at <c>0x9d</c>
    /// or later match it in none of the twenty-two bodies, so the working capture's
    /// byte is what is written.
    /// </remarks>
    private const byte CaptureTailByte = 0x7e;

    /// <summary>
    /// Body <c>0x9e</c>–<c>0x9f</c>: the tail word that belongs to the room.
    /// </summary>
    /// <remarks>
    /// It holds across a room's own bodies — <c>277</c> for all three of
    /// <c>143.47.227.126:5725</c>, <c>296</c> for the three of
    /// <c>64.181.216.250:5720</c>, <c>3813</c> for the five of
    /// <c>99.66.131.177:5731</c> — and which counter it names is **[U]**.
    /// </remarks>
    private const ushort RoomTailWord = 0x0ee5;

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
                WriteZeroBytes(body, RotationTripleLength);
                continue;
            }

            var entry = rotation[slot];
            body.Add(Component(nameof(entry.Map), entry.Map));
            body.Add(Component(nameof(entry.Rule), entry.Rule));
            body.Add(Component(nameof(entry.RoundOptions), (int)entry.RoundOptions));
        }
    }

    /// <summary>
    /// Body <c>0x4a</c>–<c>0x54</c>: the room's character cap, two client constants, its
    /// round count, its switch word and a minute limit.
    /// </summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteRoomStateFields(List<byte> body)
    {
        body.Add(MaximumCharacters);
        WriteUInt16LittleEndian(body, ClientFixedValue);
        body.Add(Field5);
        body.Add(NumberOfRounds);
        WriteUInt32LittleEndian(body, (uint)CommonSettingsSwitches);
        WriteUInt16LittleEndian(body, (ushort)MinuteLimitSeconds);
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
        foreach (var value in RoomStateReferenceBodyUtils.StateFields17To27)
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
            foreach (var seconds in RoomStateReferenceBodyUtils.TimerVector)
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
        foreach (var value in RoomStateReferenceBodyUtils.StateFields30To49)
        {
            body.Add(value);
        }
    }

    /// <summary>
    /// Body <c>0x9c</c>–<c>0xa0</c>: the unassigned slot reference (field 50), the
    /// capture's tail byte, the room's tail word, and field 53, which repeats field
    /// 13's selector.
    /// </summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteTailSelector(List<byte> body)
    {
        body.Add(UnassignedSlotReference);
        body.Add(CaptureTailByte);
        WriteUInt16LittleEndian(body, RoomTailWord);

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
