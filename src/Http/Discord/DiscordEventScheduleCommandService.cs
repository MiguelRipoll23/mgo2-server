using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static Mgo2Server.Http.Discord.DiscordInteractionOptionUtils;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the staff event scheduling command.
/// <para>
/// The command is the moderators' way to publish an event without an operator
/// opening the database, which is the same reason the API exposes the schedules:
/// a schedule is data somebody has to be able to write, and a table nothing can
/// write is a table that drifts.
/// </para>
/// <para>
/// It is a separate service from the message commands because it is the only one
/// that writes: everything it does goes through the same service the API uses, so
/// the two surfaces cannot disagree about what a schedule means.
/// </para>
/// <para>
/// Every option is something a person can read. An event is named rather than
/// numbered, a lobby is "Survival" rather than 4, and the window is a clock time
/// rather than an epoch second. The epoch seconds the row stores are a detail of
/// the storage; a moderator who has to produce one will get it wrong, and the
/// wrong one is silent — the event simply does not open.
/// </para>
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="scheduleService">Service that owns the schedules.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordEventScheduleCommandService(
    IDiscordInteractionResponder responder,
    EventScheduleService scheduleService,
    IOptions<DiscordOptions> options,
    ILogger<DiscordEventScheduleCommandService> logger)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Option naming what the command should do.</summary>
    private const string ActionOptionName = "action";

    /// <summary>Option naming the event.</summary>
    private const string EventOptionName = "event";

    /// <summary>Option naming the lobby mode.</summary>
    private const string ModeOptionName = "mode";

    /// <summary>Option naming the field size.</summary>
    private const string TeamsOptionName = "teams";

    /// <summary>Option naming the opening moment of the window.</summary>
    private const string FromOptionName = "from";

    /// <summary>Option naming the closing moment of the window.</summary>
    private const string UntilOptionName = "until";

    /// <summary>Option naming whether the event is published at all.</summary>
    private const string EnabledOptionName = "enabled";

    private readonly DiscordOptions options = options.Value;

    /// <summary>
    /// Zone the clock times are read in. A schedule is announced in the lobby's
    /// own clock, so this is where the times a moderator typed belong.
    /// </summary>
    private static TimeZoneInfo EventTimeZone =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Madrid", out var zone)
            ? zone
            : TimeZoneInfo.Utc;

    /// <summary>Handles one interaction of the scheduling command.</summary>
    /// <param name="interaction">Interaction to answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var interactionIdentifier = interaction.Identifier;
        var interactionToken = interaction.Token;
        if (string.IsNullOrWhiteSpace(interactionIdentifier) || string.IsNullOrWhiteSpace(interactionToken))
        {
            logger.LogWarning(
                "The {Command} command arrived without a way to answer it",
                DiscordOptions.ScheduleCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.ScheduleCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "You need the Moderator or Manager role to use this command.",
                cancellationToken);
            return;
        }

        var action = Text(interaction, ActionOptionName);
        var reply = action?.ToLowerInvariant() switch
        {
            "list" => await ListAsync(cancellationToken),
            "create" => await CreateAsync(interaction, cancellationToken),
            "update" => await UpdateAsync(interaction, cancellationToken),
            "withdraw" => await WithdrawAsync(interaction, cancellationToken),
            _ => "The action must be list, create, update or withdraw.",
        };

        if (reply.StartsWith("Scheduled", StringComparison.Ordinal)
            || reply.StartsWith("Updated", StringComparison.Ordinal)
            || reply.StartsWith("Withdrew", StringComparison.Ordinal))
        {
            logger.LogInformation(
                "The {Command} command ran a {Action} action",
                DiscordOptions.ScheduleCommandName,
                action);
        }

        await ReplyAsync(interactionIdentifier, interactionToken, reply, cancellationToken);
    }

    private async Task<string> ListAsync(CancellationToken cancellationToken)
    {
        var schedules = await scheduleService.ListAllAsync(cancellationToken);
        if (schedules.Count == 0)
        {
            // An empty table is the normal state of a deployment that has not
            // scheduled anything, so it is reported as a fact rather than as a
            // failure the moderator has to interpret.
            return "No events are scheduled.";
        }

        var now = DateTimeOffset.UtcNow;
        var lines = new List<string>(schedules.Count);
        foreach (var schedule in schedules)
        {
            lines.Add(
                $"**{schedule.Name}** — {EventScheduleService.ModeName(schedule.LobbySubtype)}, "
                + $"{DescribeWindow(schedule, now)}, "
                + $"{EventScheduleService.TeamCapacityOf(schedule)} teams, "
                + $"{(EventScheduleService.IsPublished(schedule, now.ToUnixTimeSeconds()) ? "open" : "closed")}");
        }

        return string.Join('\n', lines);
    }

    private async Task<string> CreateAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (!TryReadMode(interaction, out var mode))
        {
            return "The mode is required: Survival, Tournament or Registration.";
        }

        var name = Text(interaction, EventOptionName)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            // A name is the only handle the event will have, so it is asked for
            // rather than invented: an operator who did not choose one will not
            // remember which of "Survival 4" and "Survival 5" they meant.
            return "Give the event a name, for example `Survival Night 3`.";
        }

        if (!TryReadWindow(interaction, out var from, out var until))
        {
            return "The times must look like `20:00` and `23:00`, or `never` for an event that never closes.";
        }

        try
        {
            var schedule = await scheduleService.ScheduleAsync(
                mode,
                Number(interaction, TeamsOptionName) ?? EventConstants.BracketMaximumEntrants,
                from,
                until,
                Flag(interaction, EnabledOptionName) ?? true,
                name,
                cancellationToken);

            logger.LogInformation(
                "Event {EventName} was scheduled in mode {Mode}",
                schedule.Name,
                mode);
            return $"Scheduled **{schedule.Name}** ({EventScheduleService.ModeName(mode)}), "
                + $"{DescribeWindow(schedule, DateTimeOffset.UtcNow)}.";
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private async Task<string> UpdateAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var existing = await FindNamedAsync(interaction, cancellationToken);
        if (existing is null)
        {
            return await NoSuchEventAsync(interaction, cancellationToken);
        }

        // A change states the whole window, so the parts the moderator left out
        // are read from the schedule rather than defaulted: a default would
        // silently close an event somebody meant to leave running.
        var enabled = Flag(interaction, EnabledOptionName) ?? existing.Enabled;
        var teams = Number(interaction, TeamsOptionName) ?? EventScheduleService.TeamCapacityOf(existing);

        var from = existing.PublishStart;
        if (Text(interaction, FromOptionName) is { Length: > 0 } fromText)
        {
            if (!EventClockTimeUtils.TryParse(fromText, DateTimeOffset.UtcNow, EventTimeZone, true, out from))
            {
                return "The opening time must look like `20:00`.";
            }
        }

        var until = existing.PublishEnd;
        if (Text(interaction, UntilOptionName) is { Length: > 0 } untilText)
        {
            if (!EventClockTimeUtils.TryParse(untilText, DateTimeOffset.UtcNow, EventTimeZone, false, out until))
            {
                return "The closing time must look like `23:00`, or `never`.";
            }
        }

        var outcome = await scheduleService.UpdateAsync(
            existing.Identifier,
            enabled,
            from,
            until,
            teams,
            cancellationToken);

        return outcome switch
        {
            ScheduleWriteOutcome.Written =>
                $"Updated **{existing.Name}**, {DescribeWindow(existing, DateTimeOffset.UtcNow)}, {teams} teams.",
            ScheduleWriteOutcome.InvalidWindow =>
                "The event has to close after it opens. Give a closing time later than the opening one.",
            _ => $"There is no event called {Quote(EventOptionName)}.",
        };
    }

    private async Task<string> WithdrawAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var existing = await FindNamedAsync(interaction, cancellationToken);
        if (existing is null)
        {
            return await NoSuchEventAsync(interaction, cancellationToken);
        }

        return await scheduleService.WithdrawAsync(existing.Identifier, cancellationToken)
            ? $"Withdrew **{existing.Name}**."
            : $"There is no event called {Quote(EventOptionName)}.";
    }

    /// <summary>
    /// Resolves the event the moderator named, and says so when they named none.
    /// </summary>
    private async Task<EventSchedule?> FindNamedAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var name = Text(interaction, EventOptionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return await scheduleService.FindByNameAsync(name, cancellationToken);
    }

    private async Task<string> NoSuchEventAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var name = Text(interaction, EventOptionName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Say which event: run `/event list` to see the names.";
        }

        // The list is the answer to "I do not know what to call it", so it is
        // sent along rather than leaving the moderator to ask again.
        var schedules = await scheduleService.ListAllAsync(cancellationToken);
        return schedules.Count == 0
            ? $"There is no event called **{name.Trim()}**, and nothing is scheduled yet."
            : $"There is no event called **{name.Trim()}**.\n{string.Join('\n', schedules.Select(schedule => $"• **{schedule.Name}**"))}";
    }

    /// <summary>
    /// Reads the lobby as the word the moderator chose, rather than as the
    /// number the row stores.
    /// </summary>
    private static bool TryReadMode(Contracts.DiscordInteraction interaction, out int mode)
    {
        mode = 0;
        var text = Text(interaction, ModeOptionName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        mode = text.Trim().ToLowerInvariant() switch
        {
            "survival" => EventConstants.SurvivalSelector,
            "tournament" => EventConstants.TournamentSelector,
            "registration" => EventConstants.TournamentRegistrationSelector,
            _ => 0,
        };

        return mode != 0;
    }

    /// <summary>
    /// Reads the window as the two clock times it was stated as, and refuses a
    /// pair that cannot describe a window rather than writing one that cannot be
    /// entered.
    /// </summary>
    private static bool TryReadWindow(
        Contracts.DiscordInteraction interaction,
        out long from,
        out long until)
    {
        from = 0;
        until = 0;
        var now = DateTimeOffset.UtcNow;

        if (!EventClockTimeUtils.TryParse(
                Text(interaction, FromOptionName),
                now,
                EventTimeZone,
                rollToTomorrow: true,
                out from))
        {
            return false;
        }

        // A closing time does not roll forward: an event that closes at 23:00
        // and is asked for at 23:30 has closed, and moving it to tomorrow would
        // turn a finished event into a running one.
        if (!EventClockTimeUtils.TryParse(
                Text(interaction, UntilOptionName),
                now,
                EventTimeZone,
                rollToTomorrow: false,
                out until))
        {
            return false;
        }

        return EventScheduleService.IsWindowEnterable(from, until);
    }

    /// <summary>Writes a window back as the clock times it was stated as.</summary>
    private static string DescribeWindow(EventSchedule schedule, DateTimeOffset now) =>
        schedule.PublishEnd == 0
            ? $"open from {EventClockTimeUtils.Describe(schedule.PublishStart, EventTimeZone)}, never closes"
            : $"{EventClockTimeUtils.Describe(schedule.PublishStart, EventTimeZone)}"
                + $" to {EventClockTimeUtils.Describe(schedule.PublishEnd, EventTimeZone)}";

    private static string Quote(string optionName) => $"`{optionName}`";

    private Task ReplyAsync(
        string interactionIdentifier,
        string interactionToken,
        string message,
        CancellationToken cancellationToken) =>
        responder.RespondAsync(interactionIdentifier, interactionToken, message, cancellationToken);

    /// <summary>Reports whether the interaction carries this command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    public static bool IsEventCommand(Contracts.DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.ScheduleCommandName,
            StringComparison.OrdinalIgnoreCase);
}
