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
    /// the room — <c>0x60</c> is <c>2</c> in most rooms and <c>0</c> in the all-default
    /// ones, <c>0x63</c> is <c>2</c>, <c>3</c>, <c>6</c> or <c>0</c>, <c>0x64</c> is
    /// <c>10</c> or <c>0</c> — and keep the working capture's bytes.
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
        0x00, 0x02, 0x01, 0x00, 0x02, 0x0a,
    ];

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
    /// Body <c>0x65</c>–<c>0x86</c>: state field 29, seventeen rule times in seconds.
    /// </summary>
    /// <remarks>
    /// Elements 0 to 7 are the eight standard rules out of the room's timer bank;
    /// elements 8 to 11 are the room family's own four words (here <c>0xd003</c>,
    /// <c>0xaca0</c>, <c>0x2400</c> and <c>0x0022</c>) whose meaning is open; elements 12
    /// to 16 are the times of rules 12 to 16 by rule id (SDM, INTERVAL, ANEST, SCAP,
    /// RACE), which the lobby states in a reply this server never receives. The working
    /// capture's vector is below, and a live room's first eight elements come from its
    /// bank instead.
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
