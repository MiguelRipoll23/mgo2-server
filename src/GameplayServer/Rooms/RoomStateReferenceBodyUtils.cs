namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The pinned regions of the 161-byte room-state body: the bytes copied from
/// <c>.pcapngs/mgo2-started-briefing-then-solo-game.pcapng</c> for every offset this
/// server does not derive.
/// </summary>
/// <remarks>
/// They live beside <see cref="RoomStateRegionUtils"/> rather than inside it so the
/// writer stays a list of appends; what each region is measured to mean, and what is
/// still open, is in <c>docs/analysis/rotation-payload-corpus.md</c>.
/// </remarks>
internal static class RoomStateReferenceBodyUtils
{
    /// <summary>
    /// Body <c>0x57</c>–<c>0x64</c>: state fields 13 and 17 to 27, the room's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first byte is the selector flag, which field 53 repeats. The rest move with
    /// the room and keep the working capture's bytes, except the level band: <c>0x63</c>
    /// is its tolerance and <c>0x64</c> its base, in the same order as the host-settings
    /// block's <c>0xF7</c>/<c>0xF8</c>. The <c>.pcapngs/settings</c> capture that enables
    /// the limit writes <c>0x02</c>/<c>0x08</c> for level 8 ± 2; a room without it writes
    /// <c>0x00</c>/<c>0x00</c>.
    /// </para>
    /// <para>
    /// <b>The client fetches two bytes of this block, not one word.</b> The eight-byte
    /// field at key <c>0x5a</c> has two getters of its own — <c>0x8eb078</c> returns
    /// <c>block[1]</c> and <c>0x8eb030</c> <c>block[5]</c> — which is the measured
    /// evidence that the <c>0x5a</c>/<c>0x5e</c> pair is two independent values.
    ///
    /// <b>The two words at <c>0x5a</c> and <c>0x5e</c> are a pair.</b> One room's bodies
    /// hold them equal (<c>5/5</c>, <c>40/40</c>, <c>50/50</c>, <c>55/55</c>, <c>0/0</c>),
    /// another's hold them apart (<c>15/33</c>, <c>27/18</c>, <c>30/32</c>, <c>38/44</c>,
    /// <c>47/35</c>, <c>48/47</c>), and within one capture the pair does not move at all,
    /// so it is a standing total per team rather than a clock. The reading is the two
    /// teams' remaining tickets — the client's own TDM panel states the win condition
    /// as <c>reducing the other team's tickets to zero</c> — and it is **[I]**, not
    /// measured. They are not Drebin points, which travel in a <c>0x1001</c> record of
    /// their own (<c>docs/analysis/drebin-point-records.md</c>).
    /// </para>
    /// </remarks>
    internal static readonly byte[] StateFields17To27 =
    [
        0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x28,
        0x00, 0x02, 0x01, 0x00, LevelLimitTolerance, LevelLimitBase,
    ];

    /// <summary>Body <c>0x63</c>: the level band's ± half-width, in levels.</summary>
    /// <remarks>
    /// Measured by the capture that switches the limit on and fills the Create Game
    /// box with level 8 ± 2: the byte reads <c>0x02</c> there and <c>0x00</c> in every
    /// room that leaves the option off. The working server's body carries <c>0x02</c>
    /// with the enable bit clear, its selected band retained while the limit is off.
    /// </remarks>
    internal const byte LevelLimitTolerance = 0x02;

    /// <summary>Body <c>0x64</c>: the level band's centre, in levels.</summary>
    /// <remarks>
    /// <c>0x08</c> in the 8 ± 2 capture, <c>0x00</c> in a room that leaves the option
    /// off, and <c>0x0a</c> in the working server's body — the band it had chosen while
    /// the enable bit was clear.
    /// </remarks>
    internal const byte LevelLimitBase = 0x0a;

    /// <summary>
    /// The ticket count each side's word of <see cref="StateFields17To27"/> holds —
    /// the first team's, then the second's.
    /// </summary>
    /// <remarks>
    /// <b>It is not the rule's kill limit.</b> The bank's ticket word is where a
    /// ticketed round ends (fifty in this room's configuration); what the client's
    /// panel counts down is this pair, which the room body states as forty for each
    /// side, and the <c>0x93</c> allotment beside it agrees. A death therefore
    /// counts down from forty: a host that counted from the bank's fifty wrote a
    /// count higher than the panel was holding and the panel never moved.
    /// </remarks>
    internal const int FirstTeamTicketOffset = 2;

    /// <summary>Offset of the second side's remaining tickets.</summary>
    internal const int SecondTeamTicketOffset = 6;

    /// <summary>
    /// The allotment the room body states for a side, which a death counts down
    /// from (<see cref="FirstTeamTicketOffset"/>).
    /// </summary>
    internal static int TicketAllotment => StateFields17To27[FirstTeamTicketOffset];

    /// <summary>
    /// Body <c>0x58</c>–<c>0x64</c>: the reference fields, with the round's own
    /// ticket allotment in place of the reference pair at <c>0x5a</c>/<c>0x5e</c>.
    /// </summary>
    /// <remarks>
    /// The ticket panel counts down from this pair, so a body that states the
    /// capture's forty while the round's own rule states fifty leaves the panel
    /// holding a number the per-death records never move
    /// (<see cref="MatchRecords.TeamTicketBodiesUtils"/>).
    /// </remarks>
    /// <param name="teamTickets">The round's ticket allotment, both sides.</param>
    /// <returns>The bytes the body carries.</returns>
    internal static byte[] StateFields17To27For(int teamTickets)
    {
        var fields = (byte[])StateFields17To27.Clone();
        var allotment = (byte)Math.Clamp(teamTickets, 0, byte.MaxValue);
        fields[FirstTeamTicketOffset] = allotment;
        fields[SecondTeamTicketOffset] = allotment;
        return fields;
    }

    /// <summary>
    /// Body <c>0x65</c>–<c>0x86</c>: state field 29, seventeen rule times in seconds.
    /// </summary>
    /// <remarks>
    /// Elements 0 to 7 are the eight standard rules out of the room's timer bank;
    /// elements 8 to 11 are the bank's slots for rule ids 8 to 11, which no shipped rule
    /// uses, so they carry no room fact: they are the only four of the seventeen that are
    /// not whole minutes, they are identical in every room that sets them while their
    /// neighbours move per room, and the two known variants differ in one bit
    /// (<c>0x2400</c> against <c>0x2800</c>); elements 12 to 16 are the times of rules 12
    /// to 16 by rule id (SDM, INTERVAL, ANEST, SCAP, RACE), which the lobby states in a
    /// reply this server never receives. The working capture's vector is below, and a live
    /// room's first eight elements come from its bank instead.
    /// </remarks>
    internal static readonly ushort[] TimerVector =
    [
        300, 420, 180, 300, 540, 300, 300, 240,
        53251, 44192, 9216, 34,
        180, 5940, 0, 300, 300,
    ];

    /// <summary>Body <c>0x87</c>–<c>0x9b</c>: state fields 30 to 49.</summary>
    /// <remarks>
    /// The last two bytes are the Sneaking role slots — the players holding Snake and
    /// Metal Gear Mk.II — with <c>0x7f</c> as their unassigned value, which every body
    /// outside the SNE/TSNE rounds carries. Byte twelve (<c>0x93</c>) is the round's
    /// ticket allotment in a room that plays a ticketed rule, never below the remaining
    /// counts at <c>0x5a</c>/<c>0x5e</c>. The other bytes move with the room.
    /// </remarks>
    internal static readonly byte[] StateFields30To49 =
    [
        0x02, 0x02, 0x04, 0x02, 0x02, 0x02, 0x04, 0x00, 0x02, 0x04, 0x02,
        0x02, 0x28, 0x63, 0x00, 0x03, 0x00, 0x01, 0x01, 0x7f, 0x7f,
    ];
}
