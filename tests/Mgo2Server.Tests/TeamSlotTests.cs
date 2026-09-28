using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the two encodings a team arrives in. The peer register (<c>0x4344</c>) sends
/// the raw roster slot and the player's own team change (<c>0x4440</c>) sends it
/// 1-based, and the same admin action fires both — so a value read with the wrong
/// rule lands the player on the wrong side while looking entirely plausible.
/// </summary>
[Trait("Category", "Shared")]
public sealed class TeamSlotUtilsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(254, 254)]
    public void The_peer_register_stores_the_raw_slot_it_carries(int reported, int expected)
    {
        Assert.Equal((short)expected, TeamSlotUtils.FromRawSlot(reported));
    }

    [Theory]
    [InlineData(255)]
    [InlineData(3)]
    [InlineData(-1)]
    public void A_slot_the_roster_has_no_meaning_for_is_stored_as_no_team(int reported)
    {
        Assert.Equal(TeamSlotUtils.NoTeam, TeamSlotUtils.FromRawSlot(reported));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void The_team_change_is_turned_back_into_the_slot_it_names(int reported, int expected)
    {
        Assert.Equal((short)expected, TeamSlotUtils.FromOneBasedTeam(reported));
    }

    [Fact]
    public void Anything_the_team_change_cannot_build_is_read_as_the_first_team()
    {
        // The client only ever builds 1 or 2; 0 and the sentinel are not its output,
        // and the collapse onto the first team is the one its own builder performs.
        Assert.Equal((short)0, TeamSlotUtils.FromOneBasedTeam(0));
        Assert.Equal((short)0, TeamSlotUtils.FromOneBasedTeam(254));
    }

    [Fact]
    public void The_two_packets_do_not_agree_on_the_same_value()
    {
        // The register reports the first team as 0 and the team change reports it as
        // 1. Reading either one through the other's rule is the mix-up the two
        // converters exist to prevent.
        Assert.Equal((short)0, TeamSlotUtils.FromRawSlot(0));
        Assert.Equal((short)0, TeamSlotUtils.FromOneBasedTeam(1));
        Assert.NotEqual(TeamSlotUtils.FromRawSlot(1), TeamSlotUtils.FromOneBasedTeam(1));
    }
}
