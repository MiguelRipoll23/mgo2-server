namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The identifier set one round's stream uses. The recorded host plays a round
/// with one set and the next round with the same set a <c>0x800</c> bit along,
/// so the two generations are one bit apart throughout the stream.
/// </summary>
/// <remarks>
/// <para>
/// Measured from the two rounds of <c>mgo2-game1.pcapng</c>. Its first round's
/// run (t+46.4 s, counter 0x8019) carries the operations record <c>0x1a54</c>,
/// the payload <c>0x1ade</c>, the rules-roster record <c>0x0a61</c>, the
/// match-start marker <c>0x5ade</c> and the beat's vitals record <c>0x090c</c>;
/// its second round's run (t+194.0 s, counter 0x9023) carries <c>0x1254</c>,
/// <c>0x12de</c>, <c>0x0261</c>, <c>0x52de</c> and <c>0x010c</c>, each the first
/// round's identifier with bit <see cref="SiblingBit"/> set.
/// </para>
/// <para>
/// The switch is a <em>round</em> boundary, not a join: the second player's
/// records between t+154 s and t+185 s still use the first generation. This
/// file carries the identifiers alone; the second round's extra state group
/// (<c>0x00d9</c>–<c>0x00df</c>), its member-bearing rules body
/// (<c>ff 0f fe fe</c>) and its 25-byte roster entry are the live round's own
/// and are not replayed.
/// </para>
/// </remarks>
/// <param name="Vitals">Beat-frame vitals record identifier.</param>
/// <param name="RulesRoster">Beat-frame rules-roster record identifier.</param>
/// <param name="RoundClock">Operations record identifier.</param>
/// <param name="RoundClockAlternate">The same record with the sibling bit set.</param>
/// <param name="MatchStartMarker">Record that opens the round.</param>
/// <param name="MatchStartPayload">Payload record inside the run.</param>
/// <param name="ReportedByte">
/// Byte the round was opened on — the one the client carried in its readiness
/// report and the one the run's payload record hands back.
/// </param>
public sealed record MatchRecordGenerationUtils(
    ushort Vitals,
    ushort RulesRoster,
    ushort RoundClock,
    ushort RoundClockAlternate,
    ushort MatchStartMarker,
    ushort MatchStartPayload,
    byte ReportedByte)
{
    /// <summary>The bit one round's identifiers differ from the next round's by.</summary>
    public const ushort SiblingBit = 0x0800;

    /// <summary>
    /// The first round's set, as the capture's own first run and beat frames
    /// carry it.
    /// </summary>
    public static MatchRecordGenerationUtils First { get; } =
        new(0x090c, 0x0a61, 0x1a54, 0x9a54, 0x5ade, 0x1ade, 0x00);

    /// <summary>
    /// The set the next round uses: every identifier of this one with
    /// <see cref="SiblingBit"/> flipped. The byte a round is opened on is the
    /// reporting client's own, so it stays out of the identifier set and this
    /// keeps whatever byte it already carried.
    /// </summary>
    public MatchRecordGenerationUtils Sibling =>
        new(
            (ushort)(Vitals ^ SiblingBit),
            (ushort)(RulesRoster ^ SiblingBit),
            (ushort)(RoundClock ^ SiblingBit),
            (ushort)(RoundClockAlternate ^ SiblingBit),
            (ushort)(MatchStartMarker ^ SiblingBit),
            (ushort)(MatchStartPayload ^ SiblingBit),
            ReportedByte);

    /// <summary>
    /// The set the next round uses, carrying the byte a client is opening it
    /// with. It is the older per-peer form of <see cref="Sibling"/>; a room that
    /// opens one round for several peers derives the identifiers once and echoes
    /// each report's own byte, so it uses <see cref="Sibling"/> instead.
    /// </summary>
    /// <param name="reported">Byte the client's readiness report carried.</param>
    /// <returns>The next round's generation.</returns>
    public MatchRecordGenerationUtils Next(byte reported) => Sibling with { ReportedByte = reported };

    /// <summary>
    /// The identifiers a readiness report can arrive under: each round's
    /// payload record, and the same record with the payload-class bit set.
    /// </summary>
    /// <remarks>
    /// Both rounds' sets are listed because the report does not stay on one —
    /// the first round reports under <c>0x1ade</c> and the next under
    /// <c>0x12de</c> — and both plain and class-bit forms because a live client
    /// sends <c>0x9ade</c> (<c>0x1ade | 0x8000</c>) twenty-three times where the
    /// captures show <c>0x1ade</c> alone. Registering the four covers both
    /// observations without guessing which one a given build will send.
    /// </remarks>
    public static IReadOnlyList<ushort> ReadinessReportIdentifiers { get; } =
    [
        First.MatchStartPayload,
        (ushort)(First.MatchStartPayload | PeerAcknowledgementUtils.PayloadClassBit),
        First.Next(0).MatchStartPayload,
        (ushort)(First.Next(0).MatchStartPayload | PeerAcknowledgementUtils.PayloadClassBit),
    ];
}
