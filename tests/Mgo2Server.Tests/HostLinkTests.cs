using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the host-link contract an external page resolves codes with. The code is
/// derived rather than stored, so the derivation is the whole interface: the same
/// secret and name have to give the same four digits on both sides, and the
/// vectors below are the ones that page is written against.
/// </summary>
[Trait("Category", "Shared")]
public sealed class HostLinkUtilsTests
{
    [Theory]
    [InlineData("secret", "Snake", "1068")]
    [InlineData("secret", "Raiden", "5274")]
    [InlineData("a-long-base64-secret", "OCELOT", "0150")]
    public void A_code_is_the_documented_function_of_the_secret_and_the_name(
        string secret,
        string name,
        string expected)
    {
        Assert.Equal(expected, HostLinkUtils.BuildCode(secret, name));
    }

    [Fact]
    public void The_name_is_taken_exactly_so_two_cases_are_two_characters()
    {
        Assert.Equal("6065", HostLinkUtils.BuildCode("secret", "snake"));
        Assert.NotEqual(
            HostLinkUtils.BuildCode("secret", "Snake"),
            HostLinkUtils.BuildCode("secret", "snake"));
    }

    [Fact]
    public void The_secret_changes_the_code_a_name_gets()
    {
        Assert.NotEqual(
            HostLinkUtils.BuildCode("secret", "Snake"),
            HostLinkUtils.BuildCode("another-secret", "Snake"));
    }

    [Theory]
    [InlineData("Snake")]
    [InlineData("")]
    [InlineData("a name at the sixteen-character cap")]
    [InlineData("名前")]
    public void A_code_is_always_exactly_four_digits(string name)
    {
        var code = HostLinkUtils.BuildCode("secret", name);

        Assert.Equal(HostLinkUtils.CodeDigits, code.Length);
        Assert.All(code, character => Assert.InRange(character, '0', '9'));
        Assert.InRange(int.Parse(code), 0, HostLinkUtils.CodeSpace - 1);
    }

    [Fact]
    public void A_name_the_code_space_cannot_separate_is_still_stable()
    {
        // No collision here is a promise the four-digit space cannot make; what it
        // can promise is that the same input never drifts.
        var first = HostLinkUtils.BuildCode("secret", "Snake");
        var second = HostLinkUtils.BuildCode("secret", "Snake");

        Assert.Equal(first, second);
    }

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
        // are ordinary addresses, and a link sent for one of those would be sent
        // for a game the players could already reach.
        Assert.Equal(expected, HostLinkUtils.IsTailnetAddress(address));
    }

    [Fact]
    public void The_line_names_the_page_and_carries_the_code()
    {
        var message = HostLinkUtils.BuildMessage("https://placeholder.vercel.app", "1234");

        Assert.Equal(
            "Go to https://placeholder.vercel.app/1234 to make your game available to join from other players outside your network",
            message);
    }

    [Fact]
    public void A_base_address_with_a_trailing_slash_is_not_doubled()
    {
        Assert.Equal(
            HostLinkUtils.BuildMessage("https://placeholder.vercel.app", "1234"),
            HostLinkUtils.BuildMessage("https://placeholder.vercel.app/", "1234"));
    }
}
