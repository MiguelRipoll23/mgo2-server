namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Constants of the event subsystem that are not protocol identifiers: the
/// selectors of the three lobbies, the fixed sizes of the records the client
/// parses, and the result words it compares against.
/// </summary>
public static class EventConstants
{
    /// <summary>Lobby selector of Survival.</summary>
    public const int SurvivalSelector = 4;

    /// <summary>Lobby selector of Tournament.</summary>
    public const int TournamentSelector = 3;

    /// <summary>Lobby selector of Tournament registration.</summary>
    public const int TournamentRegistrationSelector = 10;

    /// <summary>
    /// The transient event identifier the information and active-game records
    /// carry. It is one rather than zero because zero means "the current event"
    /// on the request side.
    /// </summary>
    public const int TransientEventIdentifier = 1;

    /// <summary>Size of the event information record the client parses.</summary>
    public const int InformationWireSize = 905;

    /// <summary>Size of the host-environment block embedded in that record.</summary>
    public const int HostEnvironmentWireSize = 204;

    /// <summary>Size of the team-create preset reply.</summary>
    public const int TeamCreatePresetWireSize = 4;

    /// <summary>
    /// Result the client treats as "no saved preset": it then builds the default
    /// creation screen itself, which carries richer localized defaults than a
    /// synthesized empty preset would.
    /// </summary>
    public const uint TeamCreatePresetNotFound = unchecked((uint)-1006);

    /// <summary>State a team must be in to appear in the joinable list.</summary>
    public const int TeamJoinableState = 1;

    /// <summary>Participant state of a member that has not decided yet.</summary>
    public const int ParticipantPendingState = 1;

    /// <summary>Participant state of a member ready to play.</summary>
    public const int ParticipantReadyState = 2;

    /// <summary>Slots in a team roster, of which the first is the leader.</summary>
    public const int TeamRosterSize = 8;

    /// <summary>Largest roster a team may actually fill.</summary>
    public const int TeamMemberLimit = 6;

    /// <summary>Rule families the information record's rule byte exposes.</summary>
    public const int InformationRuleBits = 0x7f;

    /// <summary>Roster slots the active-game snapshot carries.</summary>
    public const int SnapshotParticipantCount = 8;

    /// <summary>Bytes one snapshot participant occupies.</summary>
    public const int SnapshotParticipantWireSize = 25;

    /// <summary>Size of the active-game snapshot with its host environment.</summary>
    public const int SnapshotWireSize = 645;

    /// <summary>
    /// Size of the snapshot without the host environment, which is the form
    /// <c>0x4911</c>, <c>0x4913</c> and <c>0x4985</c> carry.
    /// </summary>
    public const int CompactSnapshotWireSize = SnapshotWireSize - HostEnvironmentWireSize;

    /// <summary>Offset of the participant array inside a compact snapshot.</summary>
    public const int CompactParticipantOffset = 156;

    /// <summary>Size of one joinable-team list row.</summary>
    public const int TeamListItemWireSize = 71;

    /// <summary>Size of the participant-added push.</summary>
    public const int ParticipantAddedWireSize = 28;

    /// <summary>Size of the participant-removed push.</summary>
    public const int ParticipantRemovedWireSize = 10;

    /// <summary>Size of the participant-decision push.</summary>
    public const int ParticipantDecisionWireSize = 12;

    /// <summary>Size of the active-team-cleared push.</summary>
    public const int TeamClearedWireSize = 6;

    /// <summary>Match state of a pairing that is waiting for a host.</summary>
    public const int MatchPairedState = 1;

    /// <summary>Match state of a pairing that has been assigned a host.</summary>
    public const int MatchAssignedState = 2;

    /// <summary>Match state of a pairing whose result was reported.</summary>
    public const int MatchCompletedState = 3;

    /// <summary>Match state of a pairing that was cancelled.</summary>
    public const int MatchCancelledState = 4;

    /// <summary>Lease status of an active assignment.</summary>
    public const int LeaseActiveState = 1;

    /// <summary>Lease status of a released assignment.</summary>
    public const int LeaseReleasedState = 2;

    /// <summary>Size of the Survival match-found push.</summary>
    public const int MatchFoundWireSize = 53;

    /// <summary>
    /// Size of the next-match card push (<c>0x4A13</c>): the card identity, the
    /// lobby triple, the win-count pair, two 52-byte team blocks and the
    /// rotation index. It is wider than the match-found push because each block
    /// carries the full eight character identifiers the ladder record renders.
    /// </summary>
    public const int NextMatchCardWireSize = 123;

    /// <summary>Size of a Survival state-update push.</summary>
    public const int StateUpdateWireSize = 15;

    /// <summary>Size of the event-game initialize push.</summary>
    public const int EventGameInitializeWireSize = 78;

    /// <summary>Size of the event-game host-initialize push.</summary>
    public const int EventGameHostInitializeWireSize = 227;

    /// <summary>Size of the assignment-confirmation reply.</summary>
    public const int ConfirmationResponseWireSize = 28;

    /// <summary>Size of the assigned-member-information reply.</summary>
    public const int AssignedMemberInformationWireSize = 196;

    /// <summary>
    /// Official ACTIVE_STATE_MISMATCH(-1018). An assignment reply carries it
    /// when the client's cached active state does not match the lease it holds.
    /// </summary>
    public const uint ResultActiveStateMismatch = unchecked((uint)-1018);

    /// <summary>
    /// Global snapshot byte that marks the assigned phase. The team screens
    /// advance only from this value, so every assignment packet carries it
    /// rather than the joinable phase a team list would use.
    /// </summary>
    public const int ActiveEventAssignedState = 5;

    /// <summary>Rotation selector of the assigned event game.</summary>
    public const int EventGameRotationIndex = 0;

    /// <summary>
    /// Official tournament reservation refusal(-1106). It is sent as a raw result
    /// word, because the client compares it against its own literal rather than
    /// against a masked code.
    /// <para>
    /// It is the same literal the client's event-not-found sentence is bound to,
    /// so a refused entry and an event that could not be resolved are one code as
    /// far as the client is concerned.
    /// </para>
    /// </summary>
    public const uint ResultTournamentReservationRejected = unchecked((uint)-1106);

    /// <summary>Official event-not-found refusal(-1106), the same literal as above.</summary>
    public const uint ResultEventNotFound = unchecked((uint)-1106);

    /// <summary>Size of the terminal event-game report.</summary>
    public const int EventGameResultWireSize = 29;

    /// <summary>Refusal of a terminal report whose shape is unusable.</summary>
    public const int EventGameResultMalformed = 1;

    /// <summary>Refusal of a terminal report from a connection that is not hosting a room.</summary>
    public const int EventGameResultNoRoom = 3;

    /// <summary>Refusal of a terminal report from a connection that does not host the room.</summary>
    public const int EventGameResultNotHost = 4;

    /// <summary>Size of the assignment-confirmation request.</summary>
    public const int ConfirmationRequestWireSize = 11;

    /// <summary>Size of the active-game snapshot selector request.</summary>
    public const int SnapshotSelectorRequestWireSize = 4;

    /// <summary>Size of the event-game entry removal request.</summary>
    public const int GameEntryRemoveRequestWireSize = 4;

    /// <summary>Size of a successful event-game entry removal reply.</summary>
    public const int GameEntryRemoveResponseWireSize = 8;

    /// <summary>Size of the assigned-member-information request.</summary>
    public const int AssignedMemberInformationRequestWireSize = 8;

    /// <summary>Size of the active-event start record (<c>0x4A00</c>).</summary>
    public const int ActiveEventStateWireSize = 229;

    /// <summary>
    /// Size of the event-detail record without its item-state bytes. The count
    /// occupies the third u16, so the record's length is not a constant.
    /// </summary>
    public const int EventDetailBaseWireSize = 96;

    /// <summary>Size of one event-list row.</summary>
    public const int EventListItemWireSize = 47;

    /// <summary>Size of an event-list boundary record.</summary>
    public const int EventListBoundaryWireSize = 4;

    /// <summary>Size of the event result summary.</summary>
    public const int EventResultWireSize = 58;

    /// <summary>Size of a row resynchronization record without its item states.</summary>
    public const int EventRowResyncBaseWireSize = 48;

    /// <summary>Size of a row update record without its item states.</summary>
    public const int EventRowUpdateBaseWireSize = 38;

    /// <summary>Size of an event-state record.</summary>
    public const int EventStateWireSize = 19;

    /// <summary>Size of a roster-state record.</summary>
    public const int RosterStateWireSize = 15;

    /// <summary>Size of a counter-pair record.</summary>
    public const int CounterPairWireSize = 8;

    /// <summary>Size of a prefixed value record.</summary>
    public const int PrefixedValueWireSize = 10;

    /// <summary>Size of an advance record.</summary>
    public const int AdvanceWireSize = 12;

    /// <summary>Size of a sequence-update record.</summary>
    public const int SequenceUpdateWireSize = 8;

    /// <summary>Size of the assigned game detail.</summary>
    public const int AssignedGameDetailWireSize = 401;

    /// <summary>Count of u16 fields an event detail carries.</summary>
    public const int EventDetailU16Count = 9;

    /// <summary>Count of signed values an event detail carries.</summary>
    public const int EventDetailValueCount = 11;

    /// <summary>Count of metadata bytes an event detail carries.</summary>
    public const int EventDetailMetadataCount = 10;

    /// <summary>Words in one event-list bitset row.</summary>
    public const int EventRowWordCount = 8;

    /// <summary>
    /// Largest Tournament field a bracket can hold. It is the client's own
    /// limit: the bracket it displays is eight rows, so a ninth team could be
    /// seeded but never shown.
    /// </summary>
    public const int BracketMaximumEntrants = 8;

    /// <summary>
    /// Highest round number the client can address. Row <c>n</c> is written at
    /// an offset of sixteen bytes per round, and a ninth row would land on the
    /// snapshot the renderer diffs against, so this is a hard boundary rather
    /// than a display preference.
    /// </summary>
    public const int BracketMaximumRound = 8;

    /// <summary>Words in one round's bracket bitmap, which is 128 bits.</summary>
    public const int BracketBitmapWordCount = 4;

    /// <summary>Size of one round's bracket bitmap row.</summary>
    public const int BracketBitmapWireSize = 16;

    /// <summary>Size of the round-result record before its per-entrant status column.</summary>
    public const int RoundResultBaseWireSize = 32;

    /// <summary>Size of the bracket-state record before its rows and status column.</summary>
    public const int BracketStateBaseWireSize = 6;

    /// <summary>
    /// Words in the final standings. The client reads a fixed eight, not a count,
    /// so the record has no length of its own to get wrong.
    /// </summary>
    public const int BracketStandingWordCount = 8;

    /// <summary>Size of the final standings record and its reward.</summary>
    public const int BracketFinalStandingWireSize = 46;

    /// <summary>Size of the battle-list start marker.</summary>
    public const int BattleListStartWireSize = 270;

    /// <summary>Size of one battle-list team row.</summary>
    public const int BattleListItemWireSize = 53;

    /// <summary>
    /// Smallest field that can play. One team has nobody to meet, and a bracket
    /// of one would crown an entrant that never played.
    /// </summary>
    public const int MinimumFieldSize = 2;

    /// <summary>
    /// Slots the game-entry information record carries. The client reads a fixed
    /// four, so an entry beyond the fourth has no row to appear in.
    /// </summary>
    public const int GameEntrySlotCount = 4;

    /// <summary>
    /// First slot an entry is placed in. Neither the assignment record nor the
    /// reservation record of the reference server is written into slot zero, and
    /// nothing establishes what occupies it, so entries start one slot later.
    /// </summary>
    public const int GameEntryFirstSlot = 1;

    /// <summary>Size of one game-entry record.</summary>
    public const int GameEntryRecordWireSize = 57;

    /// <summary>
    /// Size of the game-entry information reply: the result, the advisory count,
    /// and four records. A shorter reply is read out of the receive buffer's
    /// remainder rather than refused, because the client's readers bound-check the
    /// buffer instead of the payload.
    /// </summary>
    public const int GameEntryInfoWireSize = 8 + (GameEntrySlotCount * GameEntryRecordWireSize);

    /// <summary>Whether a selector names one of the three event lobbies.</summary>
    /// <param name="selector">Selector to test.</param>
    public static bool IsEventSelector(int selector) =>
        selector is SurvivalSelector or TournamentSelector or TournamentRegistrationSelector;
}
