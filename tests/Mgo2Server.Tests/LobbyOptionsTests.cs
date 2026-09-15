using Mgo2Server.Shared.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the configuration a lobby server accepts. The gate and the account
/// server are permanent endpoints rather than gameplay lobbies, so they name
/// themselves with a name and a port and select no game type.
/// </summary>
public sealed class LobbyOptionsTests
{
    [Fact]
    public void Accepts_an_endpoint_without_a_game_type()
    {
        var options = new LobbyOptions { Name = "GATE", Port = 5731 };

        options.Validate(isGameLobby: false);
    }

    [Fact]
    public void Rejects_a_game_lobby_without_a_game_type()
    {
        var options = new LobbyOptions { Name = "Free Battle", Port = 5733 };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Rejects_an_endpoint_without_a_name()
    {
        var options = new LobbyOptions { Port = 5731 };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isGameLobby: false));
    }

    [Fact]
    public void Rejects_an_endpoint_without_a_port()
    {
        var options = new LobbyOptions { Name = "GATE" };

        Assert.Throws<InvalidOperationException>(() => options.Validate(isGameLobby: false));
    }
}
