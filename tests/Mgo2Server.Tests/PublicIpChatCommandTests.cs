using Mgo2Server.GameLobbyServer.Commands.Game.Chat;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the public-address chat command: the text that triggers it, and the line
/// it answers with.
/// <para>
/// The command is matched exactly as the self-test's is — the whole line, the
/// prefixed word, case and surrounding space irrelevant. Nothing looser is used
/// here, because a bare <c>ip</c> is a word a player can legitimately type, and
/// swallowing a message someone meant to send is worse than a command that needs
/// its slash.
/// </para>
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class PublicIpChatCommandTests
{
    [Theory]
    [InlineData("/ip")]
    [InlineData("/IP")]
    [InlineData("  /ip  ")]
    public void The_command_answers_to_its_own_text(string text)
    {
        Assert.True(PublicIpChatCommandService.IsCommand(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("ip")]
    [InlineData("/ip address")]
    [InlineData("what is my ip")]
    [InlineData("//ip")]
    [InlineData("/test")]
    public void Nothing_but_the_whole_command_line_is_the_command(string text)
    {
        Assert.False(PublicIpChatCommandService.IsCommand(text));
    }

    [Fact]
    public void The_two_chat_commands_do_not_claim_each_others_text()
    {
        // Both ride on the same handler, so one swallowing the other would make a
        // command silently stop working depending on which check runs first.
        Assert.False(PublicIpChatCommandService.IsCommand(SurvivalTestService.CommandText));
        Assert.False(SurvivalTestService.IsCommand(PublicIpChatCommandService.CommandText));
    }

    [Fact]
    public void A_registered_address_is_reported_in_the_characters_own_words()
    {
        Assert.Equal(
            "Your public IP is 203.0.113.7.",
            PublicIpChatCommandService.BuildMessage("203.0.113.7"));
    }

    [Fact]
    public void A_character_with_no_endpoint_row_is_told_so_rather_than_left_waiting()
    {
        Assert.Equal(
            "No public address is registered for your character.",
            PublicIpChatCommandService.BuildMessage(null));

        Assert.Equal(
            "No public address is registered for your character.",
            PublicIpChatCommandService.BuildMessage("   "));
    }

    [Fact]
    public void An_address_too_long_to_carry_is_refused_rather_than_cut()
    {
        // A truncated address still reads as an address, which would send the
        // player hunting a NAT problem they do not have.
        Assert.Equal(
            "No public address is registered for your character.",
            PublicIpChatCommandService.BuildMessage(new string('1', 200)));
    }

    [Fact]
    public void Every_line_the_command_can_produce_fits_the_chat_line()
    {
        var lines = new[]
        {
            PublicIpChatCommandService.BuildMessage("203.0.113.7"),
            PublicIpChatCommandService.BuildMessage(null),
            PublicIpChatCommandService.BuildMessage(new string('1', 200)),
        };

        Assert.All(lines, line => Assert.True(
            line.Length <= ChatPayloadBuilder.MaximumTextLength,
            $"a {line.Length}-character line does not fit"));
    }
}
