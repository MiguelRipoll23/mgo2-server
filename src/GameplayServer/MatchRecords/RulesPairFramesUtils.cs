namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The rules-roster pair with the room's players in it: the member list under the
/// member class, the empty list under the other.
/// </summary>
/// <remarks>
/// <b>The list is the round's and not the briefing's.</b> Every non-empty list on
/// the bench (8 209 of them over three captures) is written after that capture's
/// round run, on the beat's pair, and none before it; a briefing's pair is the
/// bare <c>fe</c> under both classes. The value rides under the member class only
/// — the other class is empty in all 8 395 of its captured records. A room with
/// no players in its table keeps the empty pair
/// (<see cref="MatchRecordFramesUtils.RulesPair"/>).
/// </remarks>
public static class RulesPairFramesUtils
{
    /// <summary>
    /// The pair the room publishes: its players under the member class when it
    /// holds any, the empty list under the other.
    /// </summary>
    /// <param name="generation">Identifier set of the room being briefed or played.</param>
    /// <param name="members">Slots of the players the round holds.</param>
    /// <returns>The two records, in the order a frame carries them.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> RulesPair(
        MatchRecordGenerationUtils generation,
        IReadOnlyList<byte> members) =>
    [
        new(generation.RulesRoster,
            members.Count > 0
                ? RulesMemberListBodiesUtils.Build(members)
                : MatchRecordBodiesUtils.RulesRoster(),
            MatchRecordFramesUtils.RulesRosterMemberListClass),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(),
            MatchRecordFramesUtils.RulesRosterEmptyListClass),
    ];
}
