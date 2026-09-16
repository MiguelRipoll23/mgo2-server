using System.Text;
using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSec.Cryptography;

namespace Mgo2Server.Tests;

/// <summary>
/// The interactions endpoint is reachable by anyone, so a request without a
/// signature of the application is answered with nothing, and the command it
/// carries is only run for the staff roles.
/// </summary>
public sealed class DiscordFlashCommandTests
{
    private const string ModeratorRole = "100000000000000001";
    private const string ManagerRole = "100000000000000002";
    private const string OtherRole = "100000000000000003";

    [Fact]
    public void AnUnsignedRequestIsRefused()
    {
        var result = CreateService().Handle(Encoding.UTF8.GetBytes("{\"type\":1}"), null, "123");

        Assert.Equal(401, result.StatusCode);
        Assert.Null(result.Response);
    }

    [Fact]
    public void ARequestSignedWithAnotherKeyIsRefused()
    {
        using var other = Key.Create(SignatureAlgorithm.Ed25519);
        var body = Encoding.UTF8.GetBytes("{\"type\":1}");

        var result = CreateService().Handle(body, Sign(other, body, "123"), "123");

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void APingIsAcknowledged()
    {
        var body = Encoding.UTF8.GetBytes("{\"type\":1}");

        var result = CreateService().Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1, result.Response!.Type);
    }

    [Fact]
    public void ADisabledIntegrationAnswersNothing()
    {
        var body = Encoding.UTF8.GetBytes("{\"type\":1}");

        var result = CreateService(new DiscordOptions())
            .Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Equal(503, result.StatusCode);
    }

    [Theory]
    [InlineData(ModeratorRole)]
    [InlineData(ManagerRole)]
    public void StaffRolesRelayTheFlashNews(string role)
    {
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var body = CommandBody("Maintenance in ten minutes", role);

        var result = CreateService(registry: registry).Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Equal(200, result.StatusCode);
        Assert.Contains("1 lobbies", result.Response!.Data!.Content);

        // The command and the broadcast endpoints share the same flow, so the
        // lobby receives the announcement the coordinator relays.
        Assert.True(lobby.Outgoing.TryRead(out var message));
        Assert.Equal("Maintenance in ten minutes", message.FlashNews.Message);
        Assert.Equal(FlashNewsSubcommand.ServerMessage, (ushort)message.FlashNews.Subcommand);
    }

    [Fact]
    public void ACommandFromAnotherRoleIsRefused()
    {
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var body = CommandBody("Maintenance in ten minutes", OtherRole);

        var result = CreateService(registry: registry).Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Moderator or Manager", result.Response!.Data!.Content);
        Assert.False(lobby.Outgoing.TryRead(out _));
    }

    [Fact]
    public void ACommandWithoutAMessageIsRefused()
    {
        var body = CommandBody(null, ModeratorRole);

        var result = CreateService().Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Equal(200, result.StatusCode);
        Assert.Contains("required", result.Response!.Data!.Content);
    }

    [Fact]
    public void AnUnconfiguredRoleAllowsNobody()
    {
        var options = new DiscordOptions
        {
            Enabled = true,
            BotToken = "token",
            ApplicationIdentifier = "1",
            ApplicationPublicKey = KeyPair.PublicKey,
        };

        Assert.False(options.AllowsFlashCommand([ModeratorRole]));

        var body = CommandBody("hello", ModeratorRole);
        var result = CreateService(options).Handle(body, Sign(KeyPair.Key, body, "123"), "123");

        Assert.Contains("Moderator or Manager", result.Response!.Data!.Content);
    }

    private static byte[] CommandBody(string? message, string role)
    {
        var options = message is null
            ? """[{"name":"other","value":"x"}]"""
            : $$"""[{"name":"message","value":{{JsonSerializer.Serialize(message)}}}]""";

        var payload =
            $$"""
            {
              "type": 2,
              "id": "1",
              "token": "interaction-token",
              "guild_id": "10",
              "member": { "roles": ["{{role}}"] },
              "data": { "name": "flash", "options": {{options}} }
            }
            """;

        return Encoding.UTF8.GetBytes(payload);
    }

    private static string Sign(Key key, byte[] body, string timestamp) =>
        Convert.ToHexString(SignatureAlgorithm.Ed25519.Sign(
            key,
            Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray()));

    private static DiscordInteractionService CreateService(
        DiscordOptions? options = null,
        LobbyConnectionRegistryService? registry = null) =>
        new(
            new FlashNewsDispatcherService(
                registry ?? new LobbyConnectionRegistryService(
                    NullLogger<LobbyConnectionRegistryService>.Instance),
                NullLogger<FlashNewsDispatcherService>.Instance),
            Options.Create(options ?? Configured()),
            NullLogger<DiscordInteractionService>.Instance);

    private static DiscordOptions Configured() => new()
    {
        Enabled = true,
        BotToken = "token",
        ApplicationIdentifier = "1",
        ApplicationPublicKey = KeyPair.PublicKey,
        ModeratorRoleIdentifier = ModeratorRole,
        ManagerRoleIdentifier = ManagerRole,
    };

    /// <summary>Key the test interactions are signed with, shared by the tests of this class.</summary>
    private static class KeyPair
    {
        public static readonly Key Key = Key.Create(
            SignatureAlgorithm.Ed25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        public static readonly string PublicKey =
            Convert.ToHexString(Key.PublicKey.Export(KeyBlobFormat.RawPublicKey));
    }
}
