using Mgo2Server.AccountLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Commands.Game.Events;
using Mgo2Server.GameLobbyServer.Commands.Game.Rooms;
using Mgo2Server.GateLobbyServer.Commands;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Tests;

/// <summary>
/// Assembles each server role's command registry the way its runner does.
/// <para>
/// The registry refuses a second handler for one identifier, and it refuses by
/// throwing, so a role that claims an identifier twice stops at startup instead
/// of serving a lobby whose second handler is unreachable. Nothing assembled a
/// registry outside the processes themselves, so that refusal had no test to
/// fail and a duplicate reached a deployment as every gameplay lobby refusing
/// to start.
/// </para>
/// </summary>
[Trait("Category", "Shared")]
public sealed class CommandRegistrationTests
{
    [Fact]
    public void The_gate_assembles_its_registry()
    {
        var registry = new CommandRegistry();

        GateCommandHandlerRegistration.RegisterCommandHandlers(registry);

        Assert.True(registry.Has(ServerType.Gate, CommandConstants.GetLobbyList));
        Assert.True(registry.Has(ServerType.Gate, CommandConstants.GetNews));
    }

    [Fact]
    public void The_account_server_assembles_its_registry()
    {
        var registry = new CommandRegistry();

        AccountCommandHandlerRegistration.RegisterCommandHandlers(registry);

        Assert.True(registry.Has(ServerType.Account, CommandConstants.CheckSession));
        Assert.True(registry.Has(ServerType.Account, CommandConstants.GetCharacterList));
    }

    [Fact]
    public void The_gameplay_lobby_assembles_its_registry()
    {
        var registry = new CommandRegistry();

        GameCommandHandlerRegistration.RegisterCommandHandlers(registry);

        Assert.True(registry.Has(ServerType.GameplayLobby, CommandConstants.GetGameList));
        Assert.True(registry.Has(ServerType.GameplayLobby, CommandConstants.GetEventInformation));
    }

    [Fact]
    public void The_team_creation_card_is_registered_once_for_the_gameplay_lobby()
    {
        var registry = new CommandRegistry();

        GameCommandHandlerRegistration.RegisterCommandHandlers(registry);

        // One identifier has one handler, so this is the question a second
        // registration fails rather than answers.
        Assert.Equal(
            typeof(GetTeamCreateInformationHandler),
            registry.ResolveHandlerType(ServerType.GameplayLobby, CommandConstants.GetTeamCreateInformation));
    }

    [Fact]
    public void The_room_host_hand_off_is_the_identifier_the_client_sends_it_on()
    {
        var registry = new CommandRegistry();

        GameCommandHandlerRegistration.RegisterCommandHandlers(registry);

        Assert.Equal(
            typeof(PassRoundHandler),
            registry.ResolveHandlerType(ServerType.GameplayLobby, CommandConstants.PassRound));
    }
}
