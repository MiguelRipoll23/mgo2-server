using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// One mail delivery seen from both ends: the recipient's inbox and the
/// sender's sent list are the same row, with per-side read and deleted flags,
/// because the wire format carries neither.
/// </summary>
[Table("mail")]
public sealed class MailMessage
{
    /// <summary>Identifier of the message.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Sending character, or <c>null</c> for system letters.</summary>
    [Column("sender_character_id")]
    public int? SenderCharacterIdentifier { get; set; }

    /// <summary>Receiving character, or <c>null</c> for mail addressed to the game masters.</summary>
    [Column("recipient_character_id")]
    public int? RecipientCharacterIdentifier { get; set; }

    /// <summary>Name of the sender as shown in the inbox.</summary>
    [Column("sender_name")]
    [MaxLength(16)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Name of the recipient as shown in the sent list.</summary>
    [Column("recipient_name")]
    [MaxLength(16)]
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Subject line of the message.</summary>
    [Column("subject")]
    [MaxLength(128)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Body of the message.</summary>
    [Column("body")]
    [MaxLength(708)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Whether the recipient has read the message.</summary>
    [Column("recipient_read")]
    public bool RecipientRead { get; set; }

    /// <summary>Whether the recipient has deleted the message.</summary>
    [Column("recipient_deleted")]
    public bool RecipientDeleted { get; set; }

    /// <summary>Whether the sender has read the message.</summary>
    [Column("sender_read")]
    public bool SenderRead { get; set; }

    /// <summary>Whether the sender has deleted the message.</summary>
    [Column("sender_deleted")]
    public bool SenderDeleted { get; set; }

    /// <summary>Timestamp without time zone the message was sent at.</summary>
    [Column("sent_at")]
    public DateTime SentAt { get; set; }

    /// <summary>Sending character.</summary>
    [ForeignKey(nameof(SenderCharacterIdentifier))]
    public Character? Sender { get; set; }

    /// <summary>Receiving character.</summary>
    [ForeignKey(nameof(RecipientCharacterIdentifier))]
    public Character? Recipient { get; set; }
}

/// <summary>
/// Mail addressed to the game masters. The client sends no recipient for these
/// letters, so there is no delivery row; this table is an administrative inbox
/// that is never served to a game client.
/// </summary>
[Table("gm_mail")]
public sealed class GameMasterMail
{
    /// <summary>Identifier of the message.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Sending character.</summary>
    [Column("sender_character_id")]
    public int? SenderCharacterIdentifier { get; set; }

    /// <summary>Name of the sender.</summary>
    [Column("sender_name")]
    [MaxLength(16)]
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Subject line of the message.</summary>
    [Column("subject")]
    [MaxLength(128)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Body of the message.</summary>
    [Column("body")]
    [MaxLength(708)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Timestamp without time zone the message was sent at.</summary>
    [Column("sent_at")]
    public DateTime SentAt { get; set; }

    /// <summary>Sending character.</summary>
    [ForeignKey(nameof(SenderCharacterIdentifier))]
    public Character? Sender { get; set; }
}
