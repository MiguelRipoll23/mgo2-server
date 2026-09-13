using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Mail;

/// <summary>One listed letter, as the mail handlers render it.</summary>
/// <param name="Identifier">Identifier of the letter.</param>
/// <param name="Index">Position within the queried list, the per-category wire index.</param>
/// <param name="Counterparty">The other end of the letter: sender for the inbox, recipient for Sent.</param>
/// <param name="Subject">Subject line of the letter.</param>
/// <param name="Body">Body of the letter.</param>
/// <param name="SentAtEpoch">Unix seconds the letter was sent at.</param>
/// <param name="Read">Whether this end has read the letter.</param>
/// <param name="SystemSender">Whether the letter was sent by the system.</param>
public sealed record MailEntry(
    int Identifier,
    int Index,
    string Counterparty,
    string Subject,
    string Body,
    long SentAtEpoch,
    bool Read,
    bool SystemSender);

/// <summary>Result of a delivery attempt, per recipient.</summary>
/// <param name="Name">Name of the recipient.</param>
/// <param name="FailureCode">Official failure code, or <c>null</c> when the letter was delivered.</param>
public sealed record RecipientOutcome(string Name, int? FailureCode);

/// <summary>
/// Owns the mail. The recipient's inbox and the sender's sent list are the same
/// row, split by per-side read and deleted flags.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class MailService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>The client's own per-category cap; a seventeenth letter would silently vanish.</summary>
    public const int MailboxMaximum = 16;

    /// <summary>Official failure code for an unknown recipient (-801).</summary>
    private const int RecipientUnknownFailureCode = -801;

    /// <summary>Official failure code for a full mailbox (-802).</summary>
    private const int RecipientMailboxFullFailureCode = -802;

    /// <summary>Delivers one letter to a named recipient.</summary>
    /// <param name="senderCharacterIdentifier">Character that sends the letter.</param>
    /// <param name="senderName">Name of the sender as shown in the inbox.</param>
    /// <param name="recipientName">Name of the recipient.</param>
    /// <param name="subject">Subject line of the letter.</param>
    /// <param name="body">Body of the letter.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<RecipientOutcome> SendAsync(
        int senderCharacterIdentifier,
        string senderName,
        string recipientName,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var targetIdentifier = await context.Characters
            .AsNoTracking()
            .Where(character => character.Name == recipientName)
            .Select(character => (int?)character.Identifier)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetIdentifier is null)
        {
            return new RecipientOutcome(recipientName, RecipientUnknownFailureCode);
        }

        // Serialize deliveries to the same mailbox on its character row, so two
        // simultaneous senders cannot both see room and overshoot the cap.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"SELECT id FROM characters WHERE id = {targetIdentifier.Value} FOR UPDATE",
            cancellationToken);

        var size = await MailboxSizeAsync(context, targetIdentifier.Value, cancellationToken);
        if (size >= MailboxMaximum)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RecipientOutcome(recipientName, RecipientMailboxFullFailureCode);
        }

        context.MailMessages.Add(new MailMessage
        {
            SenderCharacterIdentifier = senderCharacterIdentifier,
            RecipientCharacterIdentifier = targetIdentifier.Value,
            SenderName = senderName,
            RecipientName = recipientName,
            Subject = subject,
            Body = body,
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecipientOutcome(recipientName, null);
    }

    /// <summary>Stores a letter addressed to the game masters.</summary>
    /// <param name="senderCharacterIdentifier">Character that sends the letter.</param>
    /// <param name="senderName">Name of the sender.</param>
    /// <param name="subject">Subject line of the letter.</param>
    /// <param name="body">Body of the letter.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendGameMasterMailAsync(
        int senderCharacterIdentifier,
        string senderName,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        context.GameMasterMail.Add(new GameMasterMail
        {
            SenderCharacterIdentifier = senderCharacterIdentifier,
            SenderName = senderName,
            Subject = subject,
            Body = body,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Returns the undeleted inbox size of a character.</summary>
    /// <param name="characterIdentifier">Character whose mailbox is measured.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> GetMailboxSizeAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await MailboxSizeAsync(context, characterIdentifier, cancellationToken);
    }

    /// <summary>Returns the inbox of a character, newest first, indexed from zero.</summary>
    /// <param name="characterIdentifier">Character whose inbox is listed.</param>
    /// <param name="limit">Maximum number of letters to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<MailEntry>> GetMailboxAsync(
        int characterIdentifier,
        int limit = MailboxMaximum,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.MailMessages
            .AsNoTracking()
            .Where(message => message.RecipientCharacterIdentifier == characterIdentifier &&
                !message.RecipientDeleted)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Identifier)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. rows.Select((row, index) => ToEntry(row, index, row.SenderName, sent: false))];
    }

    /// <summary>Returns the sent list of a character, newest first, indexed from zero.</summary>
    /// <param name="characterIdentifier">Character whose sent list is listed.</param>
    /// <param name="limit">Maximum number of letters to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<MailEntry>> GetSentMailAsync(
        int characterIdentifier,
        int limit = MailboxMaximum,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.MailMessages
            .AsNoTracking()
            .Where(message => message.SenderCharacterIdentifier == characterIdentifier &&
                !message.SenderDeleted)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Identifier)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. rows.Select((row, index) => ToEntry(row, index, row.RecipientName, sent: true))];
    }

    /// <summary>Marks one side of a letter as read.</summary>
    /// <param name="mailIdentifier">Identifier of the letter.</param>
    /// <param name="sent"><c>true</c> to mark the sender's side, <c>false</c> the recipient's.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task MarkReadAsync(
        int mailIdentifier,
        bool sent,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var query = context.MailMessages.Where(message => message.Identifier == mailIdentifier);

        if (sent)
        {
            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.SenderRead, true),
                cancellationToken);
        }
        else
        {
            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.RecipientRead, true),
                cancellationToken);
        }
    }

    /// <summary>Deletes one side of a letter.</summary>
    /// <param name="mailIdentifier">Identifier of the letter.</param>
    /// <param name="sent"><c>true</c> to delete from the sender's list, <c>false</c> the recipient's.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task DeleteAsync(
        int mailIdentifier,
        bool sent,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var query = context.MailMessages.Where(message => message.Identifier == mailIdentifier);

        if (sent)
        {
            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.SenderDeleted, true),
                cancellationToken);
        }
        else
        {
            await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.RecipientDeleted, true),
                cancellationToken);
        }
    }

    /// <summary>Returns how many letters the game-master mailbox holds.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> GetGameMasterMailCountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GameMasterMail.CountAsync(cancellationToken);
    }

    private static Task<int> MailboxSizeAsync(
        Mgo2DatabaseContext context,
        int characterIdentifier,
        CancellationToken cancellationToken) =>
        context.MailMessages
            .AsNoTracking()
            .CountAsync(
                message => message.RecipientCharacterIdentifier == characterIdentifier &&
                    !message.RecipientDeleted,
                cancellationToken);

    private static MailEntry ToEntry(MailMessage row, int index, string counterparty, bool sent) =>
        new(
            row.Identifier,
            index,
            counterparty,
            row.Subject,
            row.Body,
            new DateTimeOffset(DateTime.SpecifyKind(row.SentAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            sent ? row.SenderRead : row.RecipientRead,
            row.SenderCharacterIdentifier is null);
}
