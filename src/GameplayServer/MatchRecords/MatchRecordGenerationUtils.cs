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
/// match-start marker <c>0x5ade</c>, the beat's vitals record <c>0x090c</c> and
/// the item-box group <c>0x1a50</c>/<c>0x1a5a</c>; its second round's run
/// (t+194.0 s, counter 0x9023) carries <c>0x1254</c>, <c>0x12de</c>, <c>0x0261</c>,
/// <c>0x52de</c>, <c>0x010c</c> and the item-box group <c>0x1250</c>/<c>0x125a</c>,
/// each the first round's identifier with bit <see cref="SiblingBit"/> set.
/// </para>
/// <para>
/// <b>The set follows the round, item boxes included.</b> The item-box group is
/// generation-bound like the rest, and the two forms do not mix inside a round:
/// over the 106 s of game1's first round that follow its run the recorded host
/// writes <c>0x1a50</c> and <c>0x1a5a</c> and not one <c>0x1250</c> or
/// <c>0x125a</c>, while its second round writes <c>0x1250</c>/<c>0x125a</c> and
/// none of the first form. game2 is the same across its two rounds — its first
/// is the sibling one, its client having reported <c>0x12de</c>, and carries
/// <c>0x1250</c>/<c>0x125a</c>; its second carries <c>0x1a50</c>/<c>0x1a5a</c>,
/// and its third, the sibling again, carries the sibling pair back.
/// Replaying the group under one pair for every round sends the wrong pair in
/// half of them.
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
/// <param name="ItemBox">First identifier of the beat's item-box group.</param>
/// <param name="ItemBoxState">Second identifier of the beat's item-box group.</param>
/// <param name="ReportedByte">
/// Byte the capture's round start carried — the one the client carried in its
/// readiness report and the one the run's payload record hands back. No round
/// start is written, so it stays at the value the recorded host's own run held.
/// </param>
public sealed record MatchRecordGenerationUtils(
    ushort Vitals,
    ushort RulesRoster,
    ushort RoundClock,
    ushort RoundClockAlternate,
    ushort MatchStartMarker,
    ushort MatchStartPayload,
    ushort ItemBox,
    ushort ItemBoxState,
    byte ReportedByte)
{
    /// <summary>The bit one round's identifiers differ from the next round's by.</summary>
    public const ushort SiblingBit = 0x0800;

    /// <summary>
    /// The first round's set, as the capture's own first run and beat frames
    /// carry it.
    /// </summary>
    public static MatchRecordGenerationUtils First { get; } =
        new(0x090c, 0x0a61, 0x1a54, 0x9a54, 0x5ade, 0x1ade, 0x1a50, 0x1a5a, 0x00);

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
            (ushort)(ItemBox ^ SiblingBit),
            (ushort)(ItemBoxState ^ SiblingBit),
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
    /// The identifier set a round plays under when its readiness report arrived
    /// on this identifier: the generation the reporting client is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first round's set is not a function of the round number.</b>
    /// <c>mgo2-game1.pcapng</c> opens its first round under
    /// <see cref="First"/> — the report at t+44.76 s is <c>0x1ade</c> and the
    /// run at t+46.42 s carries <c>0x1a54</c> — while <c>mgo2-game2.pcapng</c>
    /// opens its first under the sibling, its report at t+56.72 s being
    /// <c>0x12de</c> and its run at t+62.18 s carrying <c>0x1254</c>. A room
    /// that opened every first round as <see cref="First"/> would answer that
    /// client's own report with a <c>0x5ade</c> marker where it asked under
    /// <c>0x12de</c>, and would stream the round under a set its client is not
    /// on.
    /// </para>
    /// <para>
    /// The report names the round's set in all five rounds of the two captures
    /// that carry a report, and the eight rounds the two captures open alternate
    /// without a break — game1's five are <c>0x1a54</c>, <c>0x1254</c>,
    /// <c>0x1a54</c>, <c>0x1254</c>, <c>0x1a54</c> (t+46.4 s to t+765.8 s) and
    /// game2's three are <c>0x1254</c>, <c>0x1a54</c>, <c>0x1254</c> — so the
    /// parity is carried from the rounds the client has already played rather
    /// than reset with the match. That is the reading **[I]**; the measurement
/// is that the set a round plays under is the one its own report arrived
/// under. A room would therefore take its first round's set from the report and
/// alternate from there; this server writes no round start at all, so it holds
/// <see cref="First"/> for the whole stream and the reading stays here as the
/// measurement it is rather than as behaviour.
    /// </para>
    /// </remarks>
    /// <param name="identifier">Identifier the report arrived on, class bit included.</param>
    /// <returns>The generation the round the report opens plays under.</returns>
    public static MatchRecordGenerationUtils OfReadinessReport(ushort identifier) =>
        (identifier & ~PeerAcknowledgementUtils.PayloadClassBit) == First.MatchStartPayload
            ? First
            : First.Sibling;

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

    /// <summary>
    /// Whether a record identifier is one a client's readiness report arrives
    /// under.
    /// </summary>
    /// <remarks>
/// Read by the registration that binds the report to its handler, which
/// recognises it and writes no answer: the answer a capture shows is the round's
/// own opening marker (<see cref="MatchStartMarker"/>), and no round start is
/// sent — see <c>docs/protocol/DATA_PHASE_DELIVERY.md</c>.
    /// </remarks>
    /// <param name="identifier">Record identifier to test.</param>
    /// <returns><c>true</c> when the identifier is one a report arrives under.</returns>
    public static bool IsReadinessReportIdentifier(ushort identifier) =>
        ReadinessReportIdentifiers.Contains(identifier);
}
