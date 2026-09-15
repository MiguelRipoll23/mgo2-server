namespace Mgo2Server.Shared.Domain.Instructors;

/// <summary>
/// The instructor a character has saved, as the stats screen and the join
/// announcement show them.
/// </summary>
/// <param name="InstructorCharacterIdentifier">Character the student recognised.</param>
/// <param name="InstructorName">Name of that character at graduation.</param>
/// <param name="Generation">Generation of the relationship.</param>
/// <param name="Rating">Rating the student gave at graduation.</param>
public sealed record InstructorRecord(
    int InstructorCharacterIdentifier,
    string InstructorName,
    int Generation,
    short Rating);

/// <summary>
/// A star rating as the client's gauge reads it: the vote count is the
/// denominator the screen prints, and the sum is the numerator it draws.
/// <para>
/// The client computes <c>clamp(ceil(2 * numerator / denominator), 0, 10)</c>
/// half-stars, so sending the sum over the count makes the ratio the average and
/// the gauge lands on the real star count.
/// </para>
/// </summary>
/// <param name="Votes">Number of reviews the character received.</param>
/// <param name="RatingSum">Sum of the ratings received.</param>
public readonly record struct InstructorScore(int Votes, int RatingSum);

/// <summary>What a graduation did, so the caller can log it without re-querying.</summary>
/// <param name="Generation">Generation of instructor the student now belongs to.</param>
/// <param name="Recognised">Whether the student recognised the instructor.</param>
public readonly record struct GraduationResult(int Generation, bool Recognised);
