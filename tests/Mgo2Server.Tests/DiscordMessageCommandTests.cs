using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The message command arrives over the gateway as an interaction, so it is only
/// run for the staff roles, only in the configured guild, and it writes an
/// official bot message in the channel it was used in.
/// </summary>
[Trait("Category", "Http")]
public sealed class DiscordMessageCommandTests
{
    private const string ModeratorRole = "100000000000000001";
    private const string ManagerRole = "100000000000000002";
    private const string OtherRole = "100000000000000003";
    private const string Channel = "200000000000000001";
    private const string Interaction = "300000000000000001";
    private const string InteractionToken = "interaction-token";

    [Theory]
    [InlineData(ModeratorRole)]
    [InlineData(ManagerRole)]
    public async Task StaffRolesWriteAnOfficialMessageInTheChannel(string role)
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction("Maintenance in ten minutes", role),
            CancellationToken.None);

        var message = Assert.Single(messageService.Messages);
        Assert.Equal(Channel, message.ChannelIdentifier);
        Assert.Equal("Maintenance in ten minutes", message.Content);

        var reply = Assert.Single(responder.Answers);
        Assert.Equal(Interaction, reply.InteractionIdentifier);
        Assert.Contains("sent", reply.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACommandFromAnotherRoleIsRefused()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction("Maintenance in ten minutes", OtherRole),
            CancellationToken.None);

        Assert.Contains("Moderator or Manager", Assert.Single(responder.Answers).Content);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public async Task ACommandWithoutABodyIsRefused()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction(null, ModeratorRole),
            CancellationToken.None);

        Assert.Contains("required", Assert.Single(responder.Answers).Content);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public async Task ACommandOfAnotherGuildIsIgnored()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();
        var interaction = CommandInteraction("hello", ModeratorRole);
        interaction.GuildIdentifier = "99";

        await CreateService(responder, messageService).HandleInteractionAsync(interaction, CancellationToken.None);

        Assert.Empty(responder.Answers);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public async Task AnUnrelatedCommandIsIgnored()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();
        var interaction = CommandInteraction("hello", ModeratorRole);
        interaction.Data!.Name = "something-else";

        await CreateService(responder, messageService).HandleInteractionAsync(interaction, CancellationToken.None);

        Assert.Empty(responder.Answers);
        Assert.Empty(messageService.Messages);
    }

    [Fact]
    public async Task ABodyLongerThanAChannelMessageIsWrittenAsSeveralMessages()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction(new string('x', 3000), ModeratorRole),
            CancellationToken.None);

        Assert.Equal(2, messageService.Messages.Count);
        Assert.All(messageService.Messages, message =>
        {
            Assert.Equal(Channel, message.ChannelIdentifier);
            Assert.InRange(message.Content.Length, 1, 2000);
        });

        Assert.Equal(3000, string.Concat(messageService.Messages.Select(m => m.Content)).Length);
        Assert.Contains("2 messages", Assert.Single(responder.Answers).Content);
    }

    [Fact]
    public async Task ALongBodyIsBrokenOnALineWhereThereIsOne()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();
        var firstParagraph = string.Join(' ', Enumerable.Repeat("word", 300));
        var secondParagraph = string.Join(' ', Enumerable.Repeat("word", 300));

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction($"{firstParagraph}\n{secondParagraph}", ModeratorRole),
            CancellationToken.None);

        Assert.Equal(
            [$"{firstParagraph}\n", secondParagraph],
            messageService.Messages.Select(message => message.Content).ToArray());
    }

    [Fact]
    public async Task ABodyWithoutABreakIsCutAtTheChannelLength()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction(new string('x', 4500), ModeratorRole),
            CancellationToken.None);

        Assert.Equal(
            [2000, 2000, 500],
            messageService.Messages.Select(message => message.Content.Length).ToArray());
    }

    [Fact]
    public async Task ABodyLongerThanTheOptionIsTrimmedToWhatDiscordTakes()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction(new string('x', 7000), ModeratorRole),
            CancellationToken.None);

        Assert.Equal(6000, string.Concat(messageService.Messages.Select(m => m.Content)).Length);
    }

    [Fact]
    public async Task ARefusedMessageIsAnsweredWithAFailure()
    {
        var messageService = new RecordingMessageService(accepted: false);
        var responder = new RecordingResponder();

        await CreateService(responder, messageService).HandleInteractionAsync(
            CommandInteraction("Maintenance in ten minutes", ModeratorRole),
            CancellationToken.None);

        Assert.Contains("could not be sent", Assert.Single(responder.Answers).Content);
    }

    [Fact]
    public async Task ACommandWithoutAChannelIsIgnored()
    {
        var messageService = new RecordingMessageService();
        var responder = new RecordingResponder();
        var interaction = CommandInteraction("hello", ModeratorRole);
        interaction.ChannelIdentifier = null;

        await CreateService(responder, messageService).HandleInteractionAsync(interaction, CancellationToken.None);

        Assert.Empty(responder.Answers);
        Assert.Empty(messageService.Messages);
    }

    private static DiscordCommandService CreateService(
        RecordingResponder responder,
        RecordingMessageService messageService) =>
        new(
            responder,
            messageService,
            new FlashNewsDispatcherService(
                new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance),
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

    private static DiscordInteraction CommandInteraction(string? body, string role) => new()
    {
        Identifier = Interaction,
        Token = InteractionToken,
        Type = 2,
        GuildIdentifier = "10",
        ChannelIdentifier = Channel,
        Member = new DiscordInteractionMember { Roles = [role] },
        Data = new DiscordApplicationCommandData
        {
            Name = DiscordOptions.MessageCommandName,
            Options = body is null
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
                        Name = "body",
                        Value = JsonSerializer.SerializeToElement(body),
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

    private sealed class RecordingMessageService(bool accepted = true) : IDiscordMessageService
    {
        public List<(string ChannelIdentifier, string Content)> Messages { get; } = [];

        public Task<bool> SendChannelMessageAsync(
            string channelIdentifier,
            string content,
            CancellationToken cancellationToken)
        {
            Messages.Add((channelIdentifier, content));
            return Task.FromResult(accepted);
        }
    }
}