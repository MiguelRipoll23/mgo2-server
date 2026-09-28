using System.Text.Json;
using Mgo2Server.Http.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// The removal half of command registration.
/// <para>
/// Registering a command is an upsert, so a command dropped from the code is
/// simply never re-sent — and the copy Discord already holds stays in the guild
/// indefinitely, still offering a moderator an interaction this build can no
/// longer serve. The two that outlived their code were the event testing tools:
/// they created and filled teams in a running lobby, so a guild command left
/// behind is not a cosmetic leftover.
/// </para>
/// <para>
/// So every start also prunes. The list and the delete are both scoped to this
/// application, so nothing here can touch a command belonging to another bot in
/// the same guild.
/// </para>
/// </summary>
public sealed partial class DiscordRestClientService
{
    /// <summary>One registered command, as the listing answers it.</summary>
    /// <param name="Identifier">Identifier the delete is addressed by.</param>
    /// <param name="Name">Name the command answers to.</param>
    private sealed record RegisteredCommand(string Identifier, string Name);

    /// <summary>
    /// Removes the commands of this application that the current build does not
    /// define.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemoveUnknownCommandsAsync(CancellationToken cancellationToken)
    {
        var applicationIdentifier = DiscordApplicationIdentifierUtils.FromBotToken(options.BotToken);
        if (applicationIdentifier is null)
        {
            // The same reason the registrations refuse: without the identifier
            // there is no collection to list or delete from.
            return;
        }

        var commands = await ListCommandsAsync(applicationIdentifier, cancellationToken);
        if (commands is null)
        {
            return;
        }

        var stale = commands
            .Where(command => !DiscordOptions.RegisteredCommandNames.Contains(command.Name))
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Removing {Count} Discord command(s) this build no longer defines: {Names}",
            stale.Count,
            string.Join(", ", stale.Select(command => command.Name)));

        foreach (var command in stale)
        {
            using var response = await SendAsync(
                HttpMethod.Delete,
                $"{CommandsPath(applicationIdentifier)}/{command.Identifier}",
                null,
                $"remove the {command.Name} command",
                cancellationToken);
        }
    }

    /// <summary>
    /// Lists the commands of this application in the guild, or in the
    /// application when no guild is configured.
    /// </summary>
    /// <param name="applicationIdentifier">Identifier of the application.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The commands, or <c>null</c> when they could not be read.</returns>
    private async Task<List<RegisteredCommand>?> ListCommandsAsync(
        string applicationIdentifier,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            CommandsPath(applicationIdentifier),
            null,
            "list the registered commands",
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return
            [
                .. document.RootElement
                    .EnumerateArray()
                    .Where(command => command.ValueKind == JsonValueKind.Object)
                    .Select(command => new RegisteredCommand(
                        command.TryGetProperty("id", out var identifier) ? identifier.ToString() : string.Empty,
                        command.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                            ? name.GetString() ?? string.Empty
                            : string.Empty))
                    .Where(command => command.Identifier.Length > 0 && command.Name.Length > 0),
            ];
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The list of registered Discord commands could not be read");
            return null;
        }
    }
}
