namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The value the room-state body carries for each field the server has no room fact
/// for, one named constant per field.
/// </summary>
/// <remarks>
/// <para>
/// They are the working capture's,
/// <c>.pcapngs/mgo2-started-briefing-then-solo-game.pcapng</c>, so the body is that
/// capture's body field for field apart from the rotation, the current slot, the
/// population and the team tickets, which are the room's own.
/// </para>
/// <para>
/// <b>Every field has its own name, including the ones nobody has identified.</b> The
/// client's state-<c>0x00</c> table at <c>0x1229b80</c> declares each field by object
/// key, so an unidentified field is named by the key the table gives it —    /// <see cref="SneakingRounds"/> is the field at object <c>0x8a</c> — and each can be
/// renamed on its own the day a reader names it. The table's shape, the object-to-body
/// packing and what is measured about each field are in
/// <c>docs/analysis/rotation-payload-corpus.md</c> §5a.
/// </para>
/// <para>
/// <b>The type is the value's, not the wire's.</b> A room setting the server holds
/// as a count or a level — the character cap, the team-kill kick, the level band,
/// the ticket limit — is an <c>int</c>; a rule-timer word, which the game keeps in
/// its 32-bit unsigned bank, is a <c>uint</c>; a field the table declares two bytes
/// wide is a <c>ushort</c>; a flag, a stance, a slot index, a byte-wide state
/// field or a raw/unread byte stays a <c>byte</c>. The writers narrow to the wire
/// width where they append.
/// </para>
/// </remarks>
internal static class RoomStateBodyConstants
{
    /// <summary>
    /// Body <c>0x4a</c>: the floor under the room's <c>Max Number of Characters</c>.
    /// </summary>
    /// <remarks>
    /// Written as the constant <c>15</c> it told a joining client a fifteen-player
    /// room of seventeen bots was full, and the join stopped dead at the settings burst
    /// (<c>myserver.pcapng</c>, 2026-11-12, t+14.77). So the writer takes the
    /// population as its floor and this is what it writes beneath that — the value the
    /// capture's six-player room writes and the largest figure no captured room
    /// contradicts.
    /// </remarks>
    internal const int MinimumMaximumCharacters = 15;

    /// <summary>
    /// Body <c>0x4b</c>–<c>0x4c</c>: <c>0x00b4</c>, which the client writes itself.
    /// </summary>
    /// <remarks>
    /// <c>li r0,0xb4</c> at <c>0x9e9500</c> and then <c>sth</c> into its object at
    /// <c>0x46</c>. It has provenance and no reading: the value is the client's, so it
    /// is not a setting this server can derive, and what the client means by 180 is not
    /// established.
    /// </remarks>
    internal const ushort ClientFixedValue = 0x00b4;

    /// <summary>
    /// Body <c>0x4e</c>: the team-kill kick — <c>3</c> in nineteen captured bodies,
    /// <c>2</c> in the two six-slot rooms of one host, <c>6</c> in the TSNE room.
    /// </summary>
    /// <remarks>
    /// The client's builder fills it from the game-details record's <c>+0x3a6</c>
    /// halfword, low byte kept; that record holds the <c>0x4313</c> block at <c>+752</c>,
    /// so the source is block <c>0xB6</c>, <c>team_kill_kick</c>. Read as
    /// <c>Number of Rounds</c> from the screen label at <c>scenerio.gcx:0x0ccf8f</c>
    /// until then; corrected 2026-11-25.
    /// </remarks>
    internal const int TeamKillKick = 3;

    /// <summary>
    /// Body <c>0x4f</c>–<c>0x52</c>: the room's settings switches, as a bitmask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bits are the ones the single-variable captures in <c>.pcapngs/settings</c>
    /// move, each against the same baseline word: Auto Assign Teams <c>0x10000</c>,
    /// Teams Switch Positions <c>0x20000</c>, Lock On (AUTO AIM) <c>0x40000</c> and
    /// Level Limit <c>0x400000</c>. The Friendly Fire capture moves <c>0x01</c> and
    /// <c>0x10</c> together and no capture separates them
    /// (<see cref="RoomCommonSettingsFlag"/>).
    /// </para>
    /// <para>
    /// <b>It is a set of enables, not a one-of-N selection.</b> The single-variable
    /// captures isolate each switch, while the rooms outside that directory set several
    /// at once — <c>0x00274022</c> carries Auto Assign Teams, Teams Switch Positions and
    /// Lock On together, and <c>0x00274033</c> adds the Friendly Fire pair.
    /// </para>
    /// <para>
    /// <b>Drebin Points is not this word.</b> The Drebin capture leaves the field on the
    /// baseline and writes <c>0x02</c> into the rotation triple's third byte, which is
    /// <see cref="Mgo2Server.Shared.Types.RoundOption.DrebinPoints"/>. Team switching is
    /// not carried by this body at all.
    /// </para>
    /// <para>
    /// <b>The pinned value is the room's other switches.</b> <c>0x0006622a</c> sets Teams
    /// Switch Positions and Lock On; the remaining <c>0x0000622a</c> are settings this
    /// build does not name. Its level-limit enable is clear while the band keeps its
    /// <c>2 ± 10</c> values, which is the room's limit switched off with the chosen
    /// numbers retained. Auto Assign Teams is not pinned here: it is
    /// <see cref="ServerOptions.GameplayServerAutoAssignTeams"/> and the writer adds it.
    /// </para>
    /// </remarks>
    internal const RoomCommonSettingsFlag RoomSettingsSwitches =
        RoomCommonSettingsFlag.TeamsSwitchPositions
        | RoomCommonSettingsFlag.LockOnAutoAim
        | (RoomCommonSettingsFlag)0x0000_622a;

    /// <summary>
    /// Body <c>0x53</c>–<c>0x54</c>: a whole-minute limit, held as seconds.
    /// </summary>
    /// <remarks>
    /// Every captured room writes one of <c>0</c>, <c>180</c>, <c>240</c> or <c>300</c>
    /// and holds it across all of that room's own bodies (<see cref="RoomMinuteLimit"/>).
    /// <b>It is not the briefing time.</b> The builder fills block <c>+0x44</c>
    /// (<c>briefing_time</c>) into object <c>0x64</c>, which the presence map puts at body
    /// <c>0x61</c>–<c>0x62</c>; this offset is a different minute limit, whose source is
    /// still open. The round's own times are the timer bank at <c>0x65</c>.
    /// </remarks>
    internal const RoomMinuteLimit MinuteLimit = RoomMinuteLimit.ThreeMinutes;

    /// <summary>
    /// Body <c>0x57</c> and <c>0xa0</c>: the unique-character setting both carry,
    /// <c>0</c> off and <c>1</c> on.
    /// </summary>
    /// <remarks>
    /// The pair at <c>0x9a</c>/<c>0x9b</c> is its assignment: the client's start-round
    /// picker <c>P2P_AssignRoleSlots</c> (<c>0x77b628</c>) advances an LCG, takes two
    /// distinct player slots and writes them to object <c>0x9e</c>, and the body states
    /// the setting at both ends because the client reads one copy with the room and the
    /// other with the state.
    /// </remarks>
    internal const byte UniqueCharactersDisabled = 0x00;
    internal const byte UniqueCharactersEnabled = 0x01;

    /// <summary>
    /// Body <c>0x58</c>: object <c>0x5a</c>, the head of the ticket block.
    /// </summary>
    /// <remarks>
    /// The table declares this key three times — 8 bytes, 4 bytes at stride 2, and 1 byte —
    /// and the presence map selects the 1-byte view, which is what the body carries. The
    /// two shipped getters read the 8-byte view and return its bytes 1 and 5
    /// (<c>0x8eb078</c> and <c>0x8eb030</c>), which is why the key is described as a block
    /// rather than a single value.
    /// </remarks>
    internal const byte TicketBlockField5A = 0x00;

    /// <summary>Body <c>0x59</c>: object <c>0x5b</c>, the second byte of the ticket block.</summary>
    internal const byte TicketBlockField5B = 0x00;

    /// <summary>Body <c>0x5c</c>: object <c>0x5e</c>, between the two ticket words.</summary>
    internal const byte TicketBlockField5E = 0x00;

    /// <summary>Body <c>0x5d</c>: object <c>0x5f</c>, between the two ticket words.</summary>
    internal const byte TicketBlockField5F = 0x00;

    /// <summary>
    /// Body <c>0x60</c>: object <c>0x62</c>, the host's stance — block <c>+0x5E</c>,
    /// <c>host_stance</c> (<c>0</c>–<c>9</c>).
    /// </summary>
    internal const byte HostStance = 0x02;

    /// <summary>
    /// Body <c>0x61</c>–<c>0x62</c>: object <c>0x64</c>, the briefing time in minutes —
    /// block <c>+0x44</c>, <c>briefing_time</c>.
    /// </summary>
    /// <remarks>
    /// The builder stores the block word's low half at object <c>0x64</c>, which the
    /// presence map puts at these two bytes; the working capture's <c>01 00</c> is a
    /// one-minute briefing. The state table declares the field two bytes wide
    /// (<c>li r4,0x64; li r5,2</c> at <c>0x9e92f0</c>), so it is one sixteen-bit value.
    /// There is no object <c>0x65</c> record: <c>0x65</c> is this field's high byte, and
    /// the level band's tolerance begins one byte later at <c>0x63</c>.
    /// </remarks>
    internal const ushort BriefingTime = 0x0001;

    /// <summary>Body <c>0x63</c>: the level band's ± half-width, in levels.</summary>
    /// <remarks>
    /// Measured by the capture that switches the limit on and fills the Create Game box
    /// with level 8 ± 2: the byte reads <c>0x02</c> there and <c>0x00</c> in every room
    /// that leaves the option off. The working server's body carries <c>0x02</c> with the
    /// enable bit clear, its selected band retained while the limit is off.
    /// </remarks>
    internal const int LevelLimitTolerance = 2;

    /// <summary>Body <c>0x64</c>: the level band's centre, in levels.</summary>
    /// <remarks>
    /// <c>0x08</c> in the 8 ± 2 capture, <c>0x00</c> in a room that leaves the option
    /// off, and <c>0x0a</c> in the working server's body — the band it had chosen while
    /// the enable bit was clear.
    /// </remarks>
    internal const int LevelLimitBase = 10;

    /// <summary>
    /// The ticket count a side holds in the room body, which a death counts down from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is not the rule's kill limit.</b> The bank's ticket word is where a ticketed
    /// round ends (fifty in this room's configuration); what the client's panel counts
    /// down is the pair the body states, which is forty for each side here, and the
    /// round's starting count beside it agrees. A host that counted from the bank's fifty
    /// wrote a count higher than the panel was holding and the panel never moved.
    /// </para>
    /// <para>
    /// The pair is written at <c>0x5a</c> and <c>0x5e</c> — first team, then second, in
    /// the wire's order. The room's rows number blue <c>0</c>, the raw team byte numbers
    /// red <c>0</c>, and the captures do not tie the pair to a coloured side.
    /// </para>
    /// </remarks>
    internal const int TicketLimit = 40;

    /// <summary>
    /// Body <c>0x65</c>–<c>0x86</c>: the seventeen rule times in seconds, keyed by the
    /// vector word they occupy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Elements 0 to 7 are the eight standard rules out of the room's timer bank;
    /// elements 8 to 11 are the bank's slots for rule ids 8 to 11, which no shipped rule
    /// uses, so they carry no room fact: they are the only four of the seventeen that are
    /// not whole minutes, they are identical in every room that sets them while their
    /// neighbours move per room, and the two known variants differ in one bit
    /// (<c>0x2400</c> against <c>0x2800</c>); elements 12 to 16 are the times of rules 12
    /// to 16 by rule id (SDM, INTERVAL, ANEST, SCAP, RACE), which the lobby states in a
    /// reply this server never receives.
    /// </para>
    /// <para>
    /// <b>The key is the vector word, which is the rule id.</b> The first eight are in
    /// rule-id order, so <see cref="RuleTimerSlot.TeamSneaking"/> is word 7 and
    /// <see cref="RuleTimerSlot.StealthDeathmatch"/> is word 12. A live room's first
    /// eight elements come from its bank instead; this map is the working capture's.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyDictionary<RuleTimerSlot, int> RuleTimerSeconds =
        new Dictionary<RuleTimerSlot, int>
        {
            [RuleTimerSlot.Deathmatch] = 300,
            [RuleTimerSlot.TeamDeathmatch] = 420,
            [RuleTimerSlot.Rescue] = 180,
            [RuleTimerSlot.Capture] = 300,
            [RuleTimerSlot.Sneaking] = 540,
            [RuleTimerSlot.Base] = 300,
            [RuleTimerSlot.Bomb] = 300,
            [RuleTimerSlot.TeamSneaking] = 240,
            [RuleTimerSlot.Rule8] = 53251,
            [RuleTimerSlot.Rule9] = 44192,
            [RuleTimerSlot.Rule10] = 9216,
            [RuleTimerSlot.Rule11] = 34,
            [RuleTimerSlot.StealthDeathmatch] = 180,
            [RuleTimerSlot.Interval] = 5940,
            [RuleTimerSlot.Anesthesia] = 0,
            [RuleTimerSlot.SneakingCapture] = 300,
            [RuleTimerSlot.Race] = 300,
        };

    /// <summary>
    /// Body <c>0x87</c>–<c>0x95</c>: the per-rule round counts, the two ticket counts,
    /// the Sneaking and extra-time settings, and the tail, one constant each. The SNE
    /// dog-tag and checkpoint counts are the room's own and are passed in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder's fill code reads these from the game-settings record, so the names are
    /// the block's, not guesses: the record's <c>+752</c> is the 204-byte block, and each
    /// load below is at the block offset named. The full mapping is in
    /// <c>docs/analysis/rotation-payload-corpus.md</c> §5a.
    /// </para>
    /// <para>
    /// The round counts are the block's rule-timer array at indices 1, 3, 5, 7, 12, 14 and
    /// 16 — the rounds of SNE, CAP, RES, TDM, BASE, BOMB and TSNE. The ticket counts are
    /// indices 8 and 10, TDM and DM; the writer derives the TDM one at body <c>0x93</c>
    /// from the round. The tail bytes at <c>+0xBF</c>–<c>+0xC7</c> are the block's
    /// so-called unread tail: the builder does read them, so the name is a settings-screen
    /// fact, not a reader fact.
    /// </para>
    /// </remarks>
    internal const uint SneakingRounds = 0x02;

    /// <summary>Body <c>0x88</c>: object <c>0x8b</c>, the Capture round count — timer +0x70.</summary>
    internal const uint CaptureRounds = 0x02;

    /// <summary>Body <c>0x89</c>: object <c>0x8c</c>, the Rescue round count — timer +0x78.</summary>
    internal const uint RescueRounds = 0x04;

    /// <summary>Body <c>0x8a</c>: object <c>0x8d</c>, the Team Deathmatch round count — timer +0x80.</summary>
    internal const uint TeamDeathmatchRounds = 0x02;

    /// <summary>Body <c>0x8b</c>: object <c>0x8e</c>, the Base round count — timer +0x94.</summary>
    internal const uint BaseRounds = 0x02;

    /// <summary>Body <c>0x8c</c>: object <c>0x8f</c>, the Bomb round count — timer +0x9C.</summary>
    internal const uint BombRounds = 0x02;

    /// <summary>Body <c>0x8d</c>: object <c>0x90</c>, the Team Sneaking round count — timer +0xA4.</summary>
    internal const uint TeamSneakingRounds = 0x04;

    /// <summary>
    /// Body <c>0x8e</c>: the SNE dog-tag count, written from the room's live count.
    /// </summary>
    internal const int SneakingDogTagsOffset = 0x8e;

    /// <summary>Body <c>0x8f</c>: object <c>0x92</c>, block +0xC1 — a tail byte.</summary>
    internal const byte TailFieldC1 = 0x02;

    /// <summary>Body <c>0x90</c>: object <c>0x93</c>, block +0xBF — a tail byte.</summary>
    internal const byte TailFieldBF = 0x04;

    /// <summary>Body <c>0x91</c>: object <c>0x94</c>, block +0xC3 — a tail byte.</summary>
    internal const byte TailFieldC3 = 0x02;

    /// <summary>Body <c>0x92</c>: object <c>0x95</c>, block +0xC5 — a tail byte.</summary>
    internal const byte TailFieldC5 = 0x02;

    /// <summary>Body <c>0x94</c>: object <c>0x97</c>, the Deathmatch ticket count — timer +0x8C.</summary>
    internal const uint DeathmatchTickets = 0x63;

    /// <summary>
    /// Body <c>0x95</c>: the checkpoint count, written from the room's live count.
    /// </summary>
    internal const int CheckpointsOffset = 0x95;

    /// <summary>
    /// Body <c>0x96</c>–<c>0x99</c>: the Sneaking kill count, Capture's extra time, and
    /// the two tail flags the client's table declares at object <c>0x9a</c>–<c>0x9d</c>.
    /// </summary>
    /// <remarks>
    /// The builder fills the first two from block <c>+0xBD</c> and <c>+0xBC</c>, and the
    /// last two from bits 0 and 2 of <c>+0xC7</c>; the tail's meaning is still open, but
    /// its source is not.
    /// </remarks>
    internal const byte SneakingSnakeKills = 0x03;

    /// <summary>Body <c>0x97</c>: object <c>0x9b</c>, Capture's extra time — block +0xBC.</summary>
    internal const byte CaptureExtraTime = 0x00;

    /// <summary>Body <c>0x98</c>: object <c>0x9c</c>, block +0xC7 bit 0 — a tail flag.</summary>
    internal const byte TailFlagC7Bit0 = 0x01;

    /// <summary>Body <c>0x99</c>: object <c>0x9d</c>, block +0xC7 bit 2 — a tail flag.</summary>
    internal const byte TailFlagC7Bit2 = 0x01;

    /// <summary>
    /// Body <c>0x9a</c> and <c>0x9b</c>: the unique-character pair a round that assigns
    /// none carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pair is the slots that hold Snake and Metal Gear Mk.II, one field two bytes
    /// wide at object <c>0x9e</c>. The builder fills it from the block's
    /// <c>unique_red</c>/<c>unique_blue</c> (<c>+0xA8</c>/<c>+0xA9</c>) and writes
    /// <c>7f 7f</c> when both are zero. <b>The client also assigns it itself:</b>
    /// <c>0x77b628</c> draws two values with an LCG, retries while the second repeats the
    /// first, and writes the pair with <c>P2P_WriteOperationStateField(block, 0x9e, 2,
    /// …)</c>, which is why it moves between bodies of one SNE room (<c>03/02</c> →
    /// <c>03/06</c> → <c>04/09</c>).
    /// </para>
    /// <para>
    /// The pair is written when the room enables the unique-character setting and the
    /// round is Sneaking or Team Sneaking; every other body carries this in both bytes.
    /// </para>
    /// </remarks>
    internal const byte UnassignedUniqueCharacterSlot = 0x7f;

    /// <summary>Body <c>0x9c</c>: the unassigned slot, object <c>0xa0</c>.</summary>
    internal const byte UnassignedSlot = 0xff;

    /// <summary>
    /// Body <c>0x9e</c>–<c>0x9f</c>: the tail word that belongs to the room, object
    /// <c>0xa2</c>.
    /// </summary>
    /// <remarks>
    /// It holds across a room's own bodies — <c>277</c> for all three of
    /// <c>143.47.227.126:5725</c>, <c>296</c> for the three of
    /// <c>64.181.216.250:5720</c>, <c>3813</c> for the five of
    /// <c>99.66.131.177:5731</c> — and which counter it names is <b>[U]</b>.
    /// </remarks>
    internal const ushort RoomTailWord = 0x0ee5;
}
