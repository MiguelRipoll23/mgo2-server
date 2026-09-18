using Mgo2Server.Http.Discord;

namespace Mgo2Server.Tests;

/// <summary>
/// The command registration needs the application identifier, which is not a
/// setting of the deployment but the first segment of the bot token.
/// </summary>
public sealed class DiscordApplicationIdentifierTests
{
    [Fact]
    public void TheIdentifierIsReadFromTheToken()
    {
        Assert.Equal(
            "123456789012345678",
            DiscordApplicationIdentifierUtils.FromBotToken("MTIzNDU2Nzg5MDEyMzQ1Njc4.abcdef.ghijkl"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("!!!.x.y")]
    [InlineData("YWJj.x.y")]
    public void ATokenThatCarriesNoIdentifierIsRefused(string? token)
    {
        Assert.Null(DiscordApplicationIdentifierUtils.FromBotToken(token));
    }
}