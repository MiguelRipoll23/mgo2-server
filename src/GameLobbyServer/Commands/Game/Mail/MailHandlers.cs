using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Domain.Mail;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Mail;

/// <summary>Sends a mail message, reporting the recipients that failed.</summary>
/// <param name="mailService">Service that owns the mail.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SendMessageHandler(
    MailService mailService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of a name field.</summary>
    private const int NameLength = 16;

    /// <summary>Maximum number of recipients of one letter.</summary>
    private const int MaximumRecipients = 8;

    /// <summary>Length of the subject field.</summary>
    private const int SubjectLength = 128;

    /// <summary>Length of the body field.</summary>
    private const int BodyLength = 708;

    /// <summary>Offset of the recipient names.</summary>
    private const int RecipientsOffset = 1;

    /// <summary>Offset of the subject.</summary>
    private const int SubjectOffset = RecipientsOffset + MaximumRecipients * NameLength;

    /// <summary>Offset of the body.</summary>
    private const int BodyOffset = SubjectOffset + SubjectLength;

    /// <summary>Offset of the destination byte.</summary>
    private const int DestinationOffset = BodyOffset + BodyLength;

    /// <summary>Destination value meaning "to the game masters".</summary>
    private const int GameMasterDestination = 3;

    /// <summary>Bit of the result flags byte that must be set.</summary>
    private const int ResultFlags = 0x01;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.SendMessageResult, ErrorCodeConstants.ResultMailRecipientUnknown, cancellationToken);
            return;
        }

        var payload = packet.Payload;
        if (payload.Length < BodyOffset + BodyLength)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.SendMessageResult, ErrorCodeConstants.ResultMailRecipientUnknown, cancellationToken);
            return;
        }

        var sender = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var senderName = sender?.Name ?? string.Empty;
        var subject = ReadField(payload, SubjectOffset, SubjectLength);
        var body = ReadField(payload, BodyOffset, BodyLength);

        var destination = payload.Length > DestinationOffset ? payload[DestinationOffset] : 0;
        if (destination == GameMasterDestination)
        {
            await mailService.SendGameMasterMailAsync(characterIdentifier, senderName, subject, body, cancellationToken);
            await SendSuccessAsync(session, cancellationToken);
            return;
        }

        var count = Math.Min(payload.Length > 0 ? payload[0] : 0, MaximumRecipients);
        var failed = new List<RecipientOutcome>();
        for (var slot = 0; slot < count; slot++)
        {
            var name = ReadField(payload, RecipientsOffset + slot * NameLength, NameLength);
            if (name.Length == 0)
            {
                continue;
            }

            var outcome = await mailService.SendAsync(characterIdentifier, senderName, name, subject, body, cancellationToken);
            if (outcome.FailureCode is not null)
            {
                failed.Add(outcome);
            }
        }

        if (failed.Count == 0)
        {
            await SendSuccessAsync(session, cancellationToken);
            return;
        }

        // A nonzero status makes the client read the per-recipient list.
        var hasFull = failed.Any(outcome => outcome.FailureCode == ErrorCodeConstants.ResultMailRecipientFull);
        var status = hasFull
            ? ErrorCodeConstants.ResultMailRecipientFull
            : ErrorCodeConstants.ResultMailRecipientUnknown;

        var writer = new PacketWriter();
        writer.WriteUInt32(status);
        writer.WriteUInt8(ResultFlags);
        writer.WriteUInt32((uint)failed.Count);
        foreach (var outcome in failed)
        {
            writer.WriteFixedString(outcome.Name, NameLength);
            writer.WriteUInt32((uint)(outcome.FailureCode ?? unchecked((int)ErrorCodeConstants.ResultMailRecipientUnknown)));
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.SendMessageResult, writer.Build(), cancellationToken);
    }

    private Task SendSuccessAsync(TcpSession session, CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt8(ResultFlags);
        return sessionHelper.SendPacketAsync(session, CommandConstants.SendMessageResult, writer.Build(), cancellationToken);
    }

    private static string ReadField(byte[] payload, int offset, int maximumLength)
    {
        if (offset >= payload.Length)
        {
            return string.Empty;
        }

        return StringUtility.ReadFixedString(payload, offset, Math.Min(maximumLength, payload.Length - offset)).Trim();
    }
}

/// <summary>Lists the mailbox or the pending clan applications.</summary>
/// <param name="mailService">Service that owns the mail.</param>
/// <param name="clanService">Service that owns the clan applications.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetMessagesHandler(
    MailService mailService,
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Mailbox selector.</summary>
    private const int MailSelector = 0x0f;

    /// <summary>Clan-applications selector.</summary>
    private const int ClanApplicationsSelector = 0x10;

    /// <summary>Wire category of the inbox.</summary>
    private const int CategoryInbox = 0;

    /// <summary>Wire category of the sent list.</summary>
    private const int CategorySent = 1;

    /// <summary>Wire category of system announcements.</summary>
    private const int CategoryAnnouncement = 3;

    /// <summary>Name count every entry declares.</summary>
    private const int EntryNameCount = 1;

    /// <summary>Length of the subject field in an entry.</summary>
    private const int SubjectLength = 128;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var selector = reader.Remaining > 0 ? reader.ReadUInt8() : MailSelector;
        var characterIdentifier = session.CharacterIdentifier;

        // Both the opening and the closing marker carry a result word, not a
        // count: the client counts the entries itself.
        await sessionHelper.SendResultAsync(session, CommandConstants.GetMessagesStart, ErrorCodeConstants.ResultNone, cancellationToken);

        var entries = new List<(int Category, MailEntry Letter)>();

        if (selector == MailSelector && characterIdentifier is not null)
        {
            var received = await mailService.GetMailboxAsync(characterIdentifier.Value, MailService.MailboxMaximum, cancellationToken);
            var sent = await mailService.GetSentMailAsync(characterIdentifier.Value, MailService.MailboxMaximum, cancellationToken);

            entries.AddRange(received.Select(letter => (letter.SystemSender ? CategoryAnnouncement : CategoryInbox, letter)));
            entries.AddRange(sent.Select(letter => (CategorySent, letter)));
        }
        else if (selector == ClanApplicationsSelector && characterIdentifier is not null)
        {
            var membership = await clanService.FindMembershipByCharacterAsync(characterIdentifier.Value, cancellationToken);
            if (membership is { IsLeader: true })
            {
                var applications = await clanService.GetApplicantsAsync(membership.ClanIdentifier, cancellationToken);
                entries.AddRange(applications.Select((application, index) => (CategoryInbox, new MailEntry(
                    -application.CharacterIdentifier,
                    index,
                    application.Name,
                    string.Empty,
                    string.Empty,
                    new DateTimeOffset(DateTime.SpecifyKind(application.AppliedAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                    false,
                    false))));
            }
        }

        foreach (var (category, letter) in entries)
        {
            var writer = new PacketWriter();
            writer.WriteUInt8(category);
            writer.WriteUInt8(letter.Index);
            writer.WriteUInt8(EntryNameCount);
            writer.WriteFixedString(letter.Counterparty, SubjectLength);
            writer.WriteFixedString(letter.Subject, SubjectLength);
            writer.WriteUInt32((uint)letter.SentAtEpoch);
            writer.WritePadding(2);
            writer.WriteUInt8(letter.Read ? 1 : 0);
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetMessagesPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.GetMessagesEnd, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Returns the contents of one letter and marks it read.</summary>
/// <param name="mailService">Service that owns the mail.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetMessageContentsHandler(
    MailService mailService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of the body field.</summary>
    private const int BodyLength = 708;

    /// <summary>Wire category of the sent list.</summary>
    private const int CategorySent = 1;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.GetMessageContentsResult, ErrorCodeConstants.ResultMailNotFound, cancellationToken);
            return;
        }

        var (category, index) = ParseCategoryIndex(packet.Payload);
        var letter = await FindLetterAsync(mailService, characterIdentifier, category, index, cancellationToken);

        if (letter is null)
        {
            // "Unable to locate designated mail."
            await sessionHelper.SendResultAsync(session, CommandConstants.GetMessageContentsResult, ErrorCodeConstants.ResultMailNotFound, cancellationToken);
            return;
        }

        // Opening is the only mark-as-read signal the protocol has.
        await mailService.MarkReadAsync(letter.Identifier, category == CategorySent, cancellationToken);

        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteFixedString(letter.Body, BodyLength);
        await sessionHelper.SendPacketAsync(session, CommandConstants.GetMessageContentsResult, writer.Build(), cancellationToken);
    }

    /// <summary>Resolves the category and index pair carried by a read or delete request.</summary>
    public static (int Category, int Index) ParseCategoryIndex(byte[] payload) =>
        (payload.Length >= 1 ? (sbyte)payload[0] : 0, payload.Length >= 2 ? payload[1] : 0);

    /// <summary>Resolves one letter out of the list the request points at.</summary>
    public static async Task<MailEntry?> FindLetterAsync(
        MailService mailService,
        int characterIdentifier,
        int category,
        int index,
        CancellationToken cancellationToken)
    {
        const int sentCategory = 1;
        var list = category == sentCategory
            ? await mailService.GetSentMailAsync(characterIdentifier, MailService.MailboxMaximum, cancellationToken)
            : await mailService.GetMailboxAsync(characterIdentifier, MailService.MailboxMaximum, cancellationToken);

        return index >= 0 && index < list.Count ? list[index] : null;
    }
}

/// <summary>Deletes one letter from the asking end's list.</summary>
/// <param name="mailService">Service that owns the mail.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class DeleteMessageHandler(
    MailService mailService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Wire category of the sent list.</summary>
    private const int CategorySent = 1;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.DeleteMessageResult, ErrorCodeConstants.ResultMailNotFound, cancellationToken);
            return;
        }

        var (category, index) = GetMessageContentsHandler.ParseCategoryIndex(packet.Payload);
        var letter = await GetMessageContentsHandler.FindLetterAsync(mailService, characterIdentifier, category, index, cancellationToken);

        if (letter is null)
        {
            // Nothing was removed, so success would claim a deletion that did
            // not happen.
            await sessionHelper.SendResultAsync(session, CommandConstants.DeleteMessageResult, ErrorCodeConstants.ResultMailNotFound, cancellationToken);
            return;
        }

        await mailService.DeleteAsync(letter.Identifier, category == CategorySent, cancellationToken);
        await sessionHelper.SendResultAsync(session, CommandConstants.DeleteMessageResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}
