namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room-state body's single-valued fields, written one field at a time in body
/// order.
/// </summary>
/// <remarks>
/// <para>
/// Each method here names every field it appends, including the ones nobody has
/// identified, so a field is renamed in one place and the body's offsets follow from the
/// order of the appends. The values are in <see cref="RoomStateBodyConstants"/>;
/// the body's framed regions are in <see cref="RoomStateRegionUtils"/>.
/// </para>
/// <para>
/// <b>The order is the client's table order, not this server's.</b> The body packs the
/// 55 fields of the state-<c>0x00</c> table with seven omitted by the presence map, so
/// the object-to-body offset is not a constant: it is 5 before <c>0x58</c>, 3 through
/// the timer vector and 3 after it. Appending in table order is what keeps that
/// arithmetic right (<c>docs/analysis/rotation-payload-corpus.md</c> §5a).
/// </para>
/// </remarks>
internal static class RoomStateFieldUtils
{
    /// <summary>
    /// Body <c>0x4a</c>–<c>0x54</c>: the character cap, two client constants, the room's
    /// setup state, its team-kill kick, its switch word and its briefing time.
    /// </summary>
    /// <param name="body">The body being built.</param>
    /// <param name="playerCount">
    /// The room's population, host included, which the cap can never state fewer of.
    /// </param>
    /// <param name="autoAssignTeams">
    /// Whether the room puts joiners on a side itself. Set writes
    /// <see cref="RoomCommonSettingsFlag.AutoAssignTeams"/>; clear is the room whose
    /// client shows the Red / Blue / Auto selection screen.
    /// </param>
    /// <param name="levelLimit">
    /// The room's level band, whose enable bit is the <c>0x400000</c> switch.
    /// </param>
    internal static void WriteRoomSettings(
        List<byte> body,
        byte playerCount,
        bool autoAssignTeams,
        LevelLimitBand levelLimit)
    {
        // 0x4a: a cap one below the population reads as a full room to a joining client,
        // so the population is the floor and the capture's value is what sits beneath it.
        StateBodyWriterUtils.WriteUInt8(body, Math.Max(RoomStateBodyConstants.MinimumMaximumCharacters, (int)playerCount));

        // 0x4b-0x4c: the client's own 0x00b4.
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, RoomStateBodyConstants.ClientFixedValue);

        // 0x4d: the room's setup state — in game, which is every body this server sends.
        body.Add((byte)RoomSetupState.InGame);

        // 0x4e: the team-kill kick, from 0x4313's team_kill_kick.
        StateBodyWriterUtils.WriteUInt8(body, RoomStateBodyConstants.TeamKillKick);

        // 0x4f-0x52: the settings switches, with Auto Assign Teams and the level-limit
        // enable the room's own. The band itself is two fields later, at 0x63/0x64.
        var switches = autoAssignTeams
            ? RoomStateBodyConstants.RoomSettingsSwitches | RoomCommonSettingsFlag.AutoAssignTeams
            : RoomStateBodyConstants.RoomSettingsSwitches;

        if (levelLimit.Enabled)
        {
            switches |= RoomCommonSettingsFlag.LevelLimitEnabled;
        }

        StateBodyWriterUtils.WriteUInt32LittleEndian(body, (uint)switches);

        // 0x53-0x54: a whole-minute limit; the briefing time is at 0x61-0x62.
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, (ushort)RoomStateBodyConstants.MinuteLimit);
    }

    /// <summary>
    /// Body <c>0x57</c>: the room's unique-character setting, <c>0</c> or <c>1</c>.
    /// </summary>
    /// <remarks>
    /// It is the leading copy; the trailing copy is at <c>0xa0</c>
    /// (<see cref="WriteTailFields"/>). The client's role picker assigns the pair at
    /// <c>0x9a</c>/<c>0x9b</c> when the room states the setting.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="uniqueCharacters">The setting, <c>0</c> or <c>1</c>.</param>
    internal static void WriteUniqueCharacterSetting(List<byte> body, byte uniqueCharacters)
    {
        body.Add(uniqueCharacters); // 0x57
    }

    /// <summary>
    /// Body <c>0x58</c>–<c>0x5f</c>: the ticket block — both teams' ticket words and
    /// the four bytes between them.
    /// </summary>
    /// <remarks>
    /// The ticket pair is what the client's panel counts down, so it carries the round's
    /// own limit rather than the reference forty; the round's starting count at
    /// <c>0x93</c> is written beside it from the same value.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="teamTickets">The round's ticket limit, both sides.</param>
    internal static void WriteTeamTicketBlock(List<byte> body, int teamTickets)
    {
        // A byte, as the capture's pair is: the field is two bytes wide but no capture
        // carries a high byte, and clamping here keeps a value the client cannot hold
        // from wrapping into the byte beside it.
        var tickets = (byte)Math.Clamp(teamTickets, 0, byte.MaxValue);

        body.Add(RoomStateBodyConstants.TicketBlockField5A);  // 0x58
        body.Add(RoomStateBodyConstants.TicketBlockField5B);  // 0x59
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, tickets); // 0x5a-0x5b, red team
        body.Add(RoomStateBodyConstants.TicketBlockField5E);  // 0x5c
        body.Add(RoomStateBodyConstants.TicketBlockField5F);  // 0x5d
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, tickets); // 0x5e-0x5f, blue team
    }

    /// <summary>Body <c>0x60</c>: the host's stance, <c>0</c>–<c>9</c>.</summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteHostStance(List<byte> body)
    {
        body.Add(RoomStateBodyConstants.HostStance); // 0x60
    }

    /// <summary>
    /// Body <c>0x61</c>–<c>0x62</c>: the briefing time in minutes.
    /// </summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteBriefingTime(List<byte> body)
    {
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, RoomStateBodyConstants.BriefingTime); // 0x61-0x62
    }

    /// <summary>
    /// Body <c>0x63</c>–<c>0x64</c>: the level band's tolerance and base, written
    /// whether or not the limit is in force.
    /// </summary>
    /// <remarks>
    /// The two bytes are capture-measured: the limit-on capture writes <c>2</c>/<c>8</c>
    /// for level 8 ± 2 and a room without the option writes <c>0</c>/<c>0</c>.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="levelLimit">The room's level band.</param>
    internal static void WriteLevelBand(List<byte> body, LevelLimitBand levelLimit)
    {
        body.Add(levelLimit.Tolerance); // 0x63
        body.Add(levelLimit.Base);      // 0x64
    }

    /// <summary>
    /// Body <c>0x87</c>–<c>0x95</c>: the per-rule round counts, the TDM/DM ticket
    /// counts, the SNE dog-tag and checkpoint counts, the Sneaking and extra-time
    /// settings, and the tail.
    /// </summary>
    /// <remarks>
    /// <b>The counts are the room's own when it states a bank.</b> The seventeen words
    /// are in the reply's order — SNE time and rounds, CAP, RES, TDM with its tickets,
    /// DM with its tickets, BASE, BOMB and TSNE — so the round counts are indices 1, 3,
    /// 5, 7, 12, 14 and 16, and the tickets are 8 and 10. The reference body states no
    /// bank and falls back to the capture's values. The tail bytes are the block's
    /// <c>+0xBF</c>–<c>+0xC7</c>.
    /// </remarks>
    /// <param name="body">The body being built.</param>
    /// <param name="ruleTimers">The room's seventeen-word bank, or <c>null</c> for the reference body.</param>
    /// <param name="teamTickets">The round's ticket limit, used at <c>0x93</c> only without a bank.</param>
    /// <param name="sneakingDogTags">The room's SNE dog-tag count, body <c>0x8e</c>.</param>
    /// <param name="checkpoints">The room's checkpoint count, body <c>0x95</c>.</param>
    internal static void WriteRuleStateFields(
        List<byte> body,
        IReadOnlyList<int>? ruleTimers,
        int teamTickets,
        byte sneakingDogTags,
        byte checkpoints)
    {
        body.Add(RuleTimerByte(ruleTimers, 1, RoomStateBodyConstants.SneakingRounds)); // 0x87
        body.Add(RuleTimerByte(ruleTimers, 3, RoomStateBodyConstants.CaptureRounds)); // 0x88
        body.Add(RuleTimerByte(ruleTimers, 5, RoomStateBodyConstants.RescueRounds)); // 0x89
        body.Add(RuleTimerByte(ruleTimers, 7, RoomStateBodyConstants.TeamDeathmatchRounds)); // 0x8a
        body.Add(RuleTimerByte(ruleTimers, 12, RoomStateBodyConstants.BaseRounds)); // 0x8b
        body.Add(RuleTimerByte(ruleTimers, 14, RoomStateBodyConstants.BombRounds)); // 0x8c
        body.Add(RuleTimerByte(ruleTimers, 16, RoomStateBodyConstants.TeamSneakingRounds)); // 0x8d
        body.Add(sneakingDogTags); // 0x8e
        body.Add(RoomStateBodyConstants.TailFieldC1); // 0x8f
        body.Add(RoomStateBodyConstants.TailFieldBF); // 0x90
        body.Add(RoomStateBodyConstants.TailFieldC3); // 0x91
        body.Add(RoomStateBodyConstants.TailFieldC5); // 0x92

        // 0x93: object 0x96, the TDM ticket count the round starts on.
        body.Add(RuleTimerByte(
            ruleTimers,
            8,
            (byte)Math.Clamp(teamTickets, 0, byte.MaxValue)));

        body.Add(RuleTimerByte(ruleTimers, 10, RoomStateBodyConstants.DeathmatchTickets)); // 0x94
        body.Add(checkpoints); // 0x95
    }

    /// <summary>
    /// One bank word as the body's byte, or the reference value when no bank is given.
    /// </summary>
    private static byte RuleTimerByte(IReadOnlyList<int>? ruleTimers, int index, uint reference) =>
        ruleTimers is null
            ? (byte)reference
            : (byte)Math.Clamp(ruleTimers[index], 0, byte.MaxValue);

    /// <summary>
    /// Body <c>0x96</c>–<c>0x99</c>: the Sneaking kill count, Capture's extra time, and
    /// the two tail flags.
    /// </summary>
    /// <param name="body">The body being built.</param>
    internal static void WriteSneakingStateFields(List<byte> body)
    {
        body.Add(RoomStateBodyConstants.SneakingSnakeKills); // 0x96
        body.Add(RoomStateBodyConstants.CaptureExtraTime); // 0x97
        body.Add(RoomStateBodyConstants.TailFlagC7Bit0); // 0x98
        body.Add(RoomStateBodyConstants.TailFlagC7Bit2); // 0x99
    }

    /// <summary>
    /// Body <c>0x9a</c>–<c>0x9b</c>: the unique-character pair at object <c>0x9e</c>.
    /// </summary>
    /// <param name="body">The body being built.</param>
    /// <param name="red">Slot the first unique character occupies, or <c>0x7f</c>.</param>
    /// <param name="blue">Slot the second unique character occupies, or <c>0x7f</c>.</param>
    internal static void WriteUniqueCharacterSlots(List<byte> body, byte red, byte blue)
    {
        body.Add(red);
        body.Add(blue);
    }

    /// <summary>
    /// Body <c>0x9c</c>–<c>0xa0</c>: the unassigned slot, the settings
    /// generation, the room's tail word, and the unique-character setting repeated.
    /// </summary>
    /// <param name="body">The body being built.</param>
    /// <param name="settingsGeneration">
    /// The room's settings generation: the byte the client adopts and bumps on each
    /// applied field-<c>0xa1</c> record.
    /// </param>
    /// <param name="uniqueCharacters">The room's unique-character setting, <c>0</c> or <c>1</c>.</param>
    internal static void WriteTailFields(List<byte> body, byte settingsGeneration, byte uniqueCharacters)
    {
        body.Add(RoomStateBodyConstants.UnassignedSlot); // 0x9c
        body.Add(settingsGeneration);                                  // 0x9d
        StateBodyWriterUtils.WriteUInt16LittleEndian(body, RoomStateBodyConstants.RoomTailWord); // 0x9e-0x9f
        body.Add(uniqueCharacters);            // 0xa0
    }
}
