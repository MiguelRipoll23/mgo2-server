using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the deletion cooldown, which the server both enforces and states: the
/// character list carries the remaining time per slot so the client can draw its own
/// wait screen. If the two disagree the player is told one thing and refused for
/// another, which is why the boundary is pinned rather than assumed.
/// </summary>
[Trait("Category", "Shared")]
public sealed class CharacterDeletionCooldownTests
{
    /// <summary>Moment the cooldown is measured from.</summary>
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>The cooldown is a week, which is what the client's wait screen counts down.</summary>
    [Fact]
    public void The_cooldown_is_seven_days()
    {
        Assert.Equal(TimeSpan.FromDays(7), CharacterService.DeletionCooldown);
    }

    /// <summary>A fresh character reports the whole cooldown, not a rounded hour.</summary>
    [Fact]
    public void A_character_that_was_just_created_reports_the_whole_cooldown()
    {
        Assert.Equal(
            (int)TimeSpan.FromDays(7).TotalSeconds,
            CharacterService.SecondsUntilDeletable(CreatedAt, CreatedAt));
    }

    /// <summary>Whole seconds, counted down as the character ages.</summary>
    [Theory]
    [InlineData(0, 604800)]
    [InlineData(1, 604799)]
    [InlineData(86400, 518400)]
    [InlineData(604799, 1)]
    public void The_countdown_shrinks_second_by_second(int elapsedSeconds, int expected)
    {
        var now = CreatedAt + TimeSpan.FromSeconds(elapsedSeconds);

        Assert.Equal(expected, CharacterService.SecondsUntilDeletable(CreatedAt, now));
    }

    /// <summary>
    /// The cooldown expiring is what makes a character deletable, and it happens on the
    /// instant the countdown reaches zero rather than a tick later.
    /// </summary>
    [Fact]
    public void The_cooldown_expires_exactly_when_the_countdown_reaches_zero()
    {
        var oneSecondShort = CreatedAt + CharacterService.DeletionCooldown - TimeSpan.FromSeconds(1);
        var exactly = CreatedAt + CharacterService.DeletionCooldown;

        Assert.Equal(1, CharacterService.SecondsUntilDeletable(CreatedAt, oneSecondShort));
        Assert.False(CharacterService.CanDelete(CreatedAt, oneSecondShort));
        Assert.Equal(0, CharacterService.SecondsUntilDeletable(CreatedAt, exactly));
        Assert.True(CharacterService.CanDelete(CreatedAt, exactly));
    }

    /// <summary>A character long past the cooldown reports no wait, not a negative one.</summary>
    [Fact]
    public void A_settled_character_reports_no_countdown()
    {
        var now = CreatedAt + TimeSpan.FromDays(365);

        Assert.Equal(0, CharacterService.SecondsUntilDeletable(CreatedAt, now));
        Assert.True(CharacterService.CanDelete(CreatedAt, now));
    }

    /// <summary>
    /// A creation time far enough in the future would overflow the wire's signed word, so
    /// the value is clamped rather than wrapped: wrapped, it reads as a wait that has
    /// already expired and the client would offer a deletion the server refuses.
    /// </summary>
    [Fact]
    public void A_future_creation_time_is_clamped_rather_than_wrapped_negative()
    {
        Assert.Equal(int.MaxValue, CharacterService.SecondsUntilDeletable(DateTimeOffset.MaxValue, CreatedAt));
    }

    /// <summary>
    /// A row with no creation time is the oldest possible, so it is deletable rather than
    /// stuck behind an epoch it never had.
    /// </summary>
    [Fact]
    public void A_character_without_a_creation_time_is_deletable()
    {
        Assert.True(CharacterService.CanDelete(default, CreatedAt));
    }
}
