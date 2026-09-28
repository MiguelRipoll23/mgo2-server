using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the two facts the joinability line rests on: which advertised addresses need
/// it, and what it says. There is no code to keep in step with a page any more, so
/// nothing here is a wire contract — the line is advice, and the page settles which
/// game is the player's by the account they sign in with.
/// </summary>
[Trait("Category", "Shared")]
public sealed class ExternalJoinUtilsTests
{
    [Theory]
    [InlineData("100.64.0.0", true)]
    [InlineData("100.101.102.103", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData(" 100.64.0.1 ", true)]
    [InlineData("100.63.255.255", false)]
    [InlineData("100.128.0.0", false)]
    [InlineData("100.0.0.1", false)]
    [InlineData("192.168.1.200", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::1", false)]
    [InlineData("fd7a:115c:a1e0::1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-an-address", false)]
    public void Only_an_address_inside_the_tailnet_range_is_a_tailnet_one(string? address, bool expected)
    {
        // The range is the tailnet's, 100.64.0.0/10: the neighbours on either side
        // are ordinary addresses, and a line sent for one of those would be advice
        // about a game the players could already reach.
        Assert.Equal(expected, ExternalJoinUtils.IsTailnetAddress(address));
    }

    [Fact]
    public void The_line_names_the_page_and_says_what_to_do_there()
    {
        Assert.Equal(
            "Go to altmgo.vercel.app and sign in to make your game joinable to other players outside your network",
            ExternalJoinUtils.BuildHintMessage("altmgo.vercel.app"));
    }

    [Fact]
    public void A_hostname_with_a_trailing_slash_is_not_doubled()
    {
        Assert.Equal(
            ExternalJoinUtils.BuildHintMessage("altmgo.vercel.app"),
            ExternalJoinUtils.BuildHintMessage("altmgo.vercel.app/"));
    }
}
