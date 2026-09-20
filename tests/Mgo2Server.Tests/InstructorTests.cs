using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Instructors;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rule that decides whether a graduation names an instructor, which is
/// the one place in the instructor flow where a plausible shorthand is wrong and the
/// mistake is permanent: the client offers the prompt once and the name cannot be
/// erased, so a false recognition is recorded forever.
/// </summary>
[Trait("Category", "Shared")]
public sealed class InstructorGraduationUtilityTests
{
    [Fact]
    public void Only_an_accepted_prompt_is_a_recognition()
    {
        Assert.True(InstructorGraduationUtils.IsRecognised(InstructorGraduationUtils.RecognisedAnswer));

        // Declined, and never asked. Neither names an instructor.
        Assert.False(InstructorGraduationUtils.IsRecognised(InstructorGraduationUtils.DeclinedAnswer));
        Assert.False(InstructorGraduationUtils.IsRecognised(InstructorGraduationUtils.NotAskedAnswer));
    }

    /// <summary>
    /// The shorthand <c>(answer &amp; 0x20) == 0</c> reads a declined prompt — which
    /// does have bit 5 clear — as an acceptance. Pinned because that is exactly the
    /// bug the reference server documents paying for.
    /// </summary>
    [Fact]
    public void A_declined_prompt_is_not_read_as_an_acceptance()
    {
        Assert.False(InstructorGraduationUtils.IsRecognised(InstructorGraduationUtils.DeclinedAnswer));

        var shorthand = (InstructorGraduationUtils.DeclinedAnswer & 0x20) == 0;
        Assert.True(shorthand);
    }

    [Fact]
    public void An_absent_answer_is_never_a_recognition()
    {
        Assert.False(InstructorGraduationUtils.IsRecognised(InstructorGraduationUtils.NoAnswer));
    }
}

/// <summary>
/// Guards the two halves of the documented instructor requirement against the level
/// table this server actually derives levels from.
/// </summary>
[Trait("Category", "Shared")]
public sealed class InstructorRequirementTests
{
    /// <summary>
    /// The requirement is stated as "Level 3 or above", so the constant has to be the
    /// experience at which this server starts displaying level 3 — not one below it,
    /// and not level 4's threshold.
    /// </summary>
    [Fact]
    public void The_experience_requirement_is_the_level_three_threshold()
    {
        const int required = InstructorService.MinimumLevelExperience;

        Assert.Equal(3, LevelUtils.CalculateLevel(required));
        Assert.Equal(2, LevelUtils.CalculateLevel(required - 1));
    }

    /// <summary>Twenty hours, the other half of the documented requirement.</summary>
    [Fact]
    public void The_play_time_requirement_is_twenty_hours()
    {
        Assert.Equal(20 * 60 * 60, InstructorService.MinimumPlaySeconds);
    }
}
