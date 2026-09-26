namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The identifier range the testing tools hand out, and the one question every
/// part of the event subsystem asks about it.
/// <para>
/// A fake player has no account, no character and nothing anybody could log in
/// as, so the only thing that tells one apart from a real character is the
/// number it is given. That makes the range load-bearing well beyond the tools
/// that write it: the queue reads it to decide whether a member's decision byte
/// matters, the pairing listing reads it to label a side, and the member-state
/// change reads it to decide whose byte it is allowed to move. Three readers
/// with three reasons to disagree is three chances to get a boundary subtly
/// wrong, so the range and the test of it live here together.
/// </para>
/// </summary>
public static class FakePlayerIdentifierUtils
{
    /// <summary>
    /// First identifier a fake team or player is given. It sits far above any
    /// character a client could have been assigned, so a fake identifier in a
    /// roster is never mistaken for a real one and never collides with one.
    /// <para>
    /// It is public so the parts that decide whether a roster may enter — the
    /// matchmaking readiness rule in particular — can tell a player who has no
    /// button to press from one who does.
    /// </para>
    /// </summary>
    public const int FirstFakeIdentifier = 1_000_000_000;

    /// <summary>
    /// Whether a character is one the testing tools made, which is exactly the
    /// question the range above answers.
    /// </summary>
    /// <param name="characterIdentifier">Character to test.</param>
    public static bool IsFake(int characterIdentifier) =>
        characterIdentifier >= FirstFakeIdentifier;
}
