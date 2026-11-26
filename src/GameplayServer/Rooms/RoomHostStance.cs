namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The host's stance: the room's own "Conditions" row, carried at block <c>+0x5E</c>
/// (<c>host_stance</c>) and body <c>0x60</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The byte has ten legal values, <c>0</c>–<c>9</c>, and slot <c>8</c> is a gap.</b>
/// The client's table at <c>0xE1BC48</c> has no name for <c>8</c>; it would render as
/// the inferred disc label <i>Special</i>. <c>HOST_STANCE_NONE</c> is therefore
/// <c>9</c>, not <c>8</c> — an earlier reading sent <c>8</c> for NONE and was wrong.
/// </para>
/// <para>
/// The names are string-resource keys, not a debug enum: each hashes into disc set
/// <c>[40eff4]</c>, and <c>0xA31194</c>–<c>0xA312CC</c> builds the ten-entry dispatch
/// table <c>0xA31B68</c> resolves for the text widget. The player reads
/// <i>Casual</i>, <i>Serious</i>, <i>Newbies Welcome</i> and so on, which is why
/// <see cref="Easy"/> is 0 and <see cref="Beginner"/> is 2.
/// </para>
/// <para>
/// Values <c>0</c>–<c>4</c> are the ones the <c>+/−</c> cycler offers in a normal
/// lobby; above <c>4</c> the row is gated on a lobby flag (mask <c>0x20020</c>) and is
/// training-only. Across 214 archived <c>0x4310</c> pushes the modal value is <c>0</c>
/// (<see cref="Easy"/>, <i>Casual</i>), which is what a human host most often sends.
/// </para>
/// </remarks>
public enum RoomHostStance : byte
{
    /// <summary>The client's <c>HOST_STANCE_EASY</c>, drawn as disc label <i>Casual</i>.</summary>
    Easy = 0,

    /// <summary>The client's <c>HOST_STANCE_REAL</c>, drawn as disc label <i>Serious</i>.</summary>
    Real = 1,

    /// <summary>
    /// The client's <c>HOST_STANCE_BEGINNER</c>, drawn as <i>Newbies Welcome</i> — the
    /// value the working room writes.
    /// </summary>
    Beginner = 2,

    /// <summary>The client's <c>HOST_STANCE_EVERYONE</c>.</summary>
    Everyone = 3,

    /// <summary>The client's <c>HOST_STANCE_OTHER</c>.</summary>
    Other = 4,

    /// <summary>The client's <c>HOST_STANCE_TRAINING</c>; training-gated.</summary>
    Training = 5,

    /// <summary>The client's <c>HOST_STANCE_INSTRUCTOR_ENTRY</c>; training-gated.</summary>
    InstructorEntry = 6,

    /// <summary>The client's <c>HOST_STANCE_INSTRUCTOR_STARTED</c>; training-gated.</summary>
    InstructorStarted = 7,

    /// <summary>
    /// No name in the client's table — the inferred disc label is <i>Special</i>.
    /// Never write this value.
    /// </summary>
    Unnamed = 8,

    /// <summary>The client's <c>HOST_STANCE_NONE</c>; the tenth value, not the ninth.</summary>
    None = 9,
}
