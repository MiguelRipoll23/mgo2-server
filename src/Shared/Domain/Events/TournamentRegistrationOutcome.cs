namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Outcome of asking for a Tournament place.</summary>
public enum TournamentReserveOutcome
{
    /// <summary>The character holds the place now.</summary>
    Reserved,

    /// <summary>The character already held a place in this event.</summary>
    AlreadyReserved,

    /// <summary>The character holds a place in a different event.</summary>
    ReservedElsewhere,

    /// <summary>Every place in this event is taken.</summary>
    NoPlacesLeft,

    /// <summary>The character does not exist.</summary>
    CharacterMissing,

    /// <summary>The character's level is outside the configured limits.</summary>
    LevelNotEligible,

    /// <summary>
    /// The named event is not one anybody may enter: there is no such event, it
    /// is unpublished, or its window is shut. It is one refusal rather than
    /// three because to a client they are one thing.
    /// </summary>
    EventUnavailable,
}

/// <summary>Outcome of submitting a team for a Tournament event.</summary>
public enum TournamentSubmitOutcome
{
    /// <summary>The team now holds a place in the bracket field.</summary>
    Registered,

    /// <summary>The team already held a place in this event.</summary>
    AlreadyRegistered,

    /// <summary>No such team, or the character is not its leader.</summary>
    NotTheLeader,

    /// <summary>The field is frozen and can no longer accept a team.</summary>
    BracketFrozen,

    /// <summary>A member already plays for another team in this event.</summary>
    MemberRegisteredElsewhere,

    /// <summary>Every team place is taken.</summary>
    TournamentFull,

    /// <summary>The named event is not one anybody may enter.</summary>
    EventUnavailable,
}

/// <summary>Outcome of releasing the place a character holds.</summary>
public enum TournamentCancelOutcome
{
    /// <summary>The place was released.</summary>
    Released,

    /// <summary>The character held no live place.</summary>
    NotHeld,

    /// <summary>
    /// The place belongs to a field that has already been drawn, and a drawn
    /// field is no longer the caller's to withdraw from: the next round is
    /// waiting for the team that walked away.
    /// </summary>
    AlreadyFrozen,
}
