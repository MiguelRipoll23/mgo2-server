using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Mail;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Instructors;

/// <summary>
/// Owns the instructor subsystem: the graduation a student completes in a combat
/// training session, the announcement that follows it, and the readings the stats
/// screen renders.
/// <para>
/// The instructor skill itself is <b>not</b> granted here. Every character is
/// served the whole skill catalogue at its maximum level, so the skill is already
/// held and the award has exactly one visible consequence — the letter. What the
/// relationship row latches is therefore the letter, not the skill.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="mailService">Service the announcement is delivered through.</param>
public sealed class InstructorService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    MailService mailService)
    : DomainService(contextFactory)
{
    /// <summary>Identifier of the instructor skill in the client's catalogue.</summary>
    public const int SkillIdentifier = 17;

    /// <summary>
    /// Play time a character must have accumulated for the award — the documented
    /// "20 or more hours of gameplay".
    /// <para>
    /// Measured against the round reports, which is the same figure the personal
    /// stats screen shows, so a player can watch themselves approach the gate. Training
    /// lobbies are excluded from it: twenty hours spent in a training lobby is not
    /// twenty hours of gameplay.
    /// </para>
    /// </summary>
    public const long MinimumPlaySeconds = 20 * 60 * 60;

    /// <summary>
    /// Experience for character level 3, the other half of the documented
    /// requirement — "Level 3 or above".
    /// <para>
    /// The requirement is stated as a level because a level is what the player sees,
    /// but it is enforced on experience, which is what the level is derived from.
    /// It is service policy, not protocol: the client enforces neither half.
    /// </para>
    /// </summary>
    public const int MinimumLevelExperience = 375;

    /// <summary>Sender shown on the announcement letter.</summary>
    public const string AnnouncementSender = "GameMaster";

    /// <summary>Subject of the announcement letter.</summary>
    public const string AnnouncementSubject = "You've been awarded the Instructor Skill";

    /// <summary>
    /// The announcement body, reproduced verbatim from the original service's
    /// letter, including the Konami URL it points at. That URL no longer resolves;
    /// keeping it is the point, because this is a reproduction and shortening it
    /// would make it a paraphrase.
    /// </summary>
    public const string AnnouncementBody =
        """
        Congratulations!

        You've been approved for graduation by
        your instructor and have accumulated
        ample experience on the battlefield.
        As a result, you are now certified as an
        "Instructor" and have been awarded
        the "Instructor Skill".

        You can now create your own Combat
        Training sessions to train others, just
        as your instructor did for you.
        For more info on Combat Training, see
        the Combat Training Manual.

        Combat Training Manual:
        http://www.konami.jp/mgo/en/instructor.html

        From:
        the MGO staff at Kojima Productions
        """;

    /// <summary>
    /// Records an instructor review, and — only when the student <em>recognised</em>
    /// the instructor — writes the relationship.
    /// <para>
    /// Every review is stored regardless of the answer, so a rating without
    /// recognition is not lost and the score gauge has a source. Without recognition
    /// nothing else happens: no relationship, no announcement.
    /// </para>
    /// <para>
    /// Recognised or not, a re-review by the same student writes another review row;
    /// the relationship is keyed by the student, so a second graduation replaces the
    /// first rather than accumulating.
    /// </para>
    /// </summary>
    /// <param name="studentCharacterIdentifier">Character that was trained.</param>
    /// <param name="instructorCharacterIdentifier">Character that hosted the session.</param>
    /// <param name="rating">Star rating the student awarded.</param>
    /// <param name="recognised">Whether the student answered yes to the recognition prompt.</param>
    /// <param name="answerByte">Raw answer byte, stored so a declined prompt stays distinguishable from one never shown.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<GraduationResult> RecordGraduationAsync(
        int studentCharacterIdentifier,
        int instructorCharacterIdentifier,
        int rating,
        bool recognised,
        int answerByte,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var instructorName = await context.Characters
            .AsNoTracking()
            .Where(character => character.Identifier == instructorCharacterIdentifier)
            .Select(character => character.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        context.InstructorReviews.Add(new InstructorReview
        {
            InstructorCharacterIdentifier = instructorCharacterIdentifier,
            StudentCharacterIdentifier = studentCharacterIdentifier,
            Rating = (short)rating,
            Recognised = recognised,
            AnswerByte = (short)answerByte,
            ReviewedAt = DateTimeOffset.UtcNow,
        });

        // An instructor with no record of their own is first generation.
        var instructorGeneration = await context.CharacterInstructors
            .AsNoTracking()
            .Where(relationship => relationship.CharacterIdentifier == instructorCharacterIdentifier)
            .Select(relationship => (int?)relationship.Generation)
            .FirstOrDefaultAsync(cancellationToken) ?? 1;
        var generation = instructorGeneration + 1;

        if (recognised)
        {
            var graduatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var relationship = await context.CharacterInstructors
                .FirstOrDefaultAsync(
                    candidate => candidate.CharacterIdentifier == studentCharacterIdentifier,
                    cancellationToken);

            if (relationship is null)
            {
                context.CharacterInstructors.Add(new CharacterInstructor
                {
                    CharacterIdentifier = studentCharacterIdentifier,
                    InstructorCharacterIdentifier = instructorCharacterIdentifier,
                    InstructorName = instructorName,
                    Generation = generation,
                    Rating = (short)rating,
                    GraduatedAt = graduatedAt,
                });
            }
            else
            {
                // Re-parenting keeps the award latch: the student already holds the
                // skill and already has the letter, so a second graduation with a
                // different instructor must not deliver another one.
                relationship.InstructorCharacterIdentifier = instructorCharacterIdentifier;
                relationship.InstructorName = instructorName;
                relationship.Generation = generation;
                relationship.Rating = (short)rating;
                relationship.GraduatedAt = graduatedAt;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new GraduationResult(generation, recognised);
    }

    /// <summary>
    /// Awards the instructor skill to a character who has a recognised graduation on
    /// file and meets the requirements, delivering the announcement.
    /// <para>
    /// Idempotent: the relationship row is claimed with a conditional update, so the
    /// letter is sent exactly once even if two end-of-round reports arrive together.
    /// Driven by the end-of-round statistics report rather than by the graduation
    /// itself, because the host reports every player as they leave — including
    /// leaving combat training — so the award rides the session ending and one that
    /// is somehow missed is picked up by the next report.
    /// </para>
    /// <para>
    /// The letter is written before the student's client fetches its mailbox, which
    /// is why the awarding has to happen inline: leaving sends the statistics report
    /// first and the mailbox fetch a few seconds later.
    /// </para>
    /// <para>
    /// The latch is claimed before the letter is delivered rather than after, so a
    /// delivery that fails leaves the award latched with no letter. The alternative
    /// orders are both worse: delivering first then latching can send the letter
    /// twice, and neither is checkable from here.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Character to consider.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether this call is the one that awarded the skill.</returns>
    public async Task<bool> AwardPendingInstructorSkillAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var relationship = await context.CharacterInstructors
            .AsNoTracking()
            .Where(candidate => candidate.CharacterIdentifier == characterIdentifier)
            .Select(candidate => new { candidate.InstructorSkillAwardedAt })
            .FirstOrDefaultAsync(cancellationToken);

        // No recognised graduation on file, or the letter has already gone out.
        if (relationship is null || relationship.InstructorSkillAwardedAt is not null)
        {
            return false;
        }

        var experience = await context.Characters
            .AsNoTracking()
            .Where(character => character.Identifier == characterIdentifier)
            .Select(character => (int?)character.Experience)
            .FirstOrDefaultAsync(cancellationToken);

        if (experience is not { } experienceValue || experienceValue < MinimumLevelExperience)
        {
            return false;
        }

        var playSeconds = await context.RoundReports
            .AsNoTracking()
            .Where(report => report.TargetCharacterIdentifier == characterIdentifier &&
                report.LobbySubtype != LobbySubtypeConstants.Training &&
                report.LobbySubtype != LobbySubtypeConstants.CombatTraining)
            .SumAsync(report => (long?)report.Seconds, cancellationToken) ?? 0;

        if (playSeconds < MinimumPlaySeconds)
        {
            return false;
        }

        // The claim is the latch. A conditional update rather than a read-modify-write
        // because two reports for the same character can be in flight at once.
        var claimedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var claimed = await context.CharacterInstructors
            .Where(candidate => candidate.CharacterIdentifier == characterIdentifier &&
                candidate.InstructorSkillAwardedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.InstructorSkillAwardedAt,
                    claimedAt),
                cancellationToken);

        if (claimed == 0)
        {
            return false;
        }

        await mailService.SendSystemMailAsync(
            characterIdentifier,
            AnnouncementSender,
            AnnouncementSubject,
            AnnouncementBody,
            cancellationToken);

        return true;
    }

    /// <summary>Returns the instructor a character saved, or <c>null</c> when they never graduated.</summary>
    /// <param name="characterIdentifier">Character to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<InstructorRecord?> FindInstructorAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterInstructors
            .AsNoTracking()
            .Where(relationship => relationship.CharacterIdentifier == characterIdentifier)
            .Select(relationship => new InstructorRecord(
                relationship.InstructorCharacterIdentifier,
                relationship.InstructorName,
                relationship.Generation,
                relationship.Rating))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Returns the instructor score of a character, lifetime or this month.</summary>
    /// <param name="characterIdentifier">Character whose reviews are aggregated.</param>
    /// <param name="currentMonth">Whether to count only the current calendar month.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<InstructorScore> GetInstructorScoreAsync(
        int characterIdentifier,
        bool currentMonth = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var reviews = context.InstructorReviews
            .AsNoTracking()
            .Where(review => review.InstructorCharacterIdentifier == characterIdentifier);

        if (currentMonth)
        {
            var since = new DateTimeOffset(
                DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);
            reviews = reviews.Where(review => review.ReviewedAt >= since);
        }

        // Two scalars rather than one grouped projection: a character with no
        // reviews has no group, and both halves of the gauge are wanted anyway.
        var votes = await reviews.CountAsync(cancellationToken);
        var ratingSum = await reviews.SumAsync(review => (int)review.Rating, cancellationToken);

        return new InstructorScore(votes, ratingSum);
    }

    /// <summary>
    /// Counts the distinct students a character has graduated.
    /// <para>
    /// Counted from the reviews, which are append-only, rather than from the saved
    /// relationships: a relationship holds one current-state row per student, so a
    /// student who re-graduated under someone else would make the count go down, and
    /// a career counter that decreases is wrong.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Character whose students are counted.</param>
    /// <param name="currentMonth">Whether to count only the current calendar month.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> CountStudentsTrainedAsync(
        int characterIdentifier,
        bool currentMonth = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var reviews = context.InstructorReviews
            .AsNoTracking()
            .Where(review => review.InstructorCharacterIdentifier == characterIdentifier && review.Recognised);

        if (currentMonth)
        {
            var since = new DateTimeOffset(
                DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);
            reviews = reviews.Where(review => review.ReviewedAt >= since);
        }

        return await reviews
            .Select(review => review.StudentCharacterIdentifier)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
