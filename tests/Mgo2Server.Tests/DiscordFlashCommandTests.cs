using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The flash command arrives over the gateway as an interaction, so it is only
/// run for the staff roles, only in the configured guild, and its outcome is
/// written back as the answer to the interaction.
/// </summary>
public sealed class DiscordFlashCommandTests
{
    private const string ModeratorRole = "100000000000000001";
    private const string ManagerRole = "100000000000000002";
    private const string OtherRole = "100000000000000003";
    private const string Interaction = "300000000000000001";
    private const string InteractionToken = "interaction-token";

    [Theory]
    [InlineData(ModeratorRole)]
    [InlineData(ManagerRole)]
    public async Task StaffRolesRelayTheFlashNews(string role)
    {
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var responder = new RecordingResponder();

        await CreateService(responder, registry).HandleInteractionAsync(
            CommandInteraction("Maintenance in ten minutes", role),
            CancellationToken.None);

        // The command and the broadcast endpoints share the same flow, so the
        // lobby receives the announcement the coordinator relays.
        Assert.True(lobby.Outgoing.TryRead(out var message));
        Assert.Equal("Maintenance in ten minutes", message.FlashNews.Message);
        Assert.Equal(FlashNewsSubcommand.ServerMessage, (ushort)message.FlashNews.Subcommand);

        var reply = Assert.Single(responder.Answers);
        Assert.Equal(Interaction, reply.InteractionIdentifier);
        Assert.Contains("1 lobbies", reply.Content);
    }

    [Fact]
    public async Task ACommandFromAnotherRoleIsRefused()
    {
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var responder = new RecordingResponder();

        await CreateService(responder, registry).HandleInteractionAsync(
            CommandInteraction("Maintenance in ten minutes", OtherRole),
            CancellationToken.None);

        Assert.Contains("Moderator or Manager", Assert.Single(responder.Answers).Content);
        Assert.False(lobby.Outgoing.TryRead(out _));
    }

    [Fact]
    public async Task ACommandWithoutAMessageIsRefused()
    {
        var responder = new RecordingResponder();

        await CreateService(responder).HandleInteractionAsync(
            CommandInteraction(null, ModeratorRole),
            CancellationToken.None);

        Assert.Contains("required", Assert.Single(responder.Answers).Content);
    }

    [Fact]
    public async Task ACommandOfAnotherGuildIsIgnored()
    {
        var responder = new RecordingResponder();
        var interaction = CommandInteraction("hello", ModeratorRole);
        interaction.GuildIdentifier = "99";

        await CreateService(responder).HandleInteractionAsync(interaction, CancellationToken.None);

        Assert.Empty(responder.Answers);
    }

    [Fact]
    public async Task AnUnrelatedCommandIsIgnored()
    {
        var responder = new RecordingResponder();
        var interaction = CommandInteraction("hello", ModeratorRole);
        interaction.Data!.Name = "something-else";

        await CreateService(responder).HandleInteractionAsync(interaction, CancellationToken.None);

        Assert.Empty(responder.Answers);
    }

    [Fact]
    public async Task AMessageLongerThanTheTickerCarriesIsTrimmed()
    {
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var responder = new RecordingResponder();

        await CreateService(responder, registry).HandleInteractionAsync(
            CommandInteraction(new string('x', 300), ModeratorRole),
            CancellationToken.None);

        Assert.True(lobby.Outgoing.TryRead(out var packet));
        Assert.Equal(255, packet.FlashNews.Message.Length);
    }

    [Fact]
    public void AnUnconfiguredRoleAllowsNobody()
    {
        var options = new DiscordOptions
        {
            Enabled = true,
            BotToken = "token",
        };

        Assert.False(options.AllowsFlashCommand([ModeratorRole]));
    }

    private static DiscordCommandService CreateService(
        RecordingResponder responder,
        LobbyConnectionRegistryService? registry = null) =>
        new(
            responder,
            new FlashNewsDispatcherService(
                registry ?? new LobbyConnectionRegistryService(
                    NullLogger<LobbyConnectionRegistryService>.Instance),
                NullLogger<FlashNewsDispatcherService>.Instance),
            Options.Create(Configured()),
            NullLogger<DiscordCommandService>.Instance);

    private static DiscordOptions Configured() => new()
    {
        Enabled = true,
        BotToken = "token",
        GuildIdentifier = "10",
        ModeratorRoleIdentifier = ModeratorRole,
        ManagerRoleIdentifier = ManagerRole,
    };

    private static DiscordInteraction CommandInteraction(string? message, string role) => new()
    {
        Identifier = Interaction,
        Token = InteractionToken,
        Type = 2,
        GuildIdentifier = "10",
        ChannelIdentifier = "200000000000000001",
        Member = new DiscordInteractionMember { Roles = [role] },
        Data = new DiscordApplicationCommandData
        {
            Name = DiscordOptions.FlashCommandName,
            Options = message is null
                ?
                [
                    new DiscordApplicationCommandOption
                    {
                        Name = "other",
                        Value = JsonSerializer.SerializeToElement("x"),
                    },
                ]
                :
                [
                    new DiscordApplicationCommandOption
                    {
                        Name = "message",
                        Value = JsonSerializer.SerializeToElement(message),
                    },
                ],
        },
    };

    private sealed class RecordingResponder : IDiscordInteractionResponder
    {
        public List<(string InteractionIdentifier, string Content)> Answers { get; } = [];

        public Task<bool> RespondAsync(
            string interactionIdentifier,
            string interactionToken,
            string content,
            CancellationToken cancellationToken)
        {
            Answers.Add((interactionIdentifier, content));
            return Task.FromResult(true);
        }
    }
}