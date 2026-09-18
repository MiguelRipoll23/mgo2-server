using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// Which lobby a character is connected to right now. One row per character,
/// written when a session enters a lobby and removed when it leaves, so the
/// primary key is the character alone: a character is in exactly one lobby at a
/// time and the database enforces that rather than trusting every call site.
/// The rows describe a live connection rather than durable state, so losing
/// them costs a reconnect and nothing else.
/// <para>
/// Which game the character is in is deliberately not recorded here: the room
/// roster already says that, and a second answer to the same question is the
/// one that goes stale and gets believed.
/// </para>
/// </summary>
[Table("character_presence")]
public sealed class CharacterPresence
{
    /// <summary>Character the row is about.</summary>
    [Key]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Lobby the character is connected to.</summary>
    [Column("lobby_id")]
    public int LobbyIdentifier { get; set; }

    /// <summary>When the character entered this lobby. Reset by a lobby change.</summary>
    [Column("since")]
    public DateTimeOffset Since { get; set; }

    /// <summary>Timestamp of the last heartbeat. Only the reaper reads it.</summary>
    [Column("last_seen")]
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Character the row is about.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }

    /// <summary>Lobby the character is connected to.</summary>
    [ForeignKey(nameof(LobbyIdentifier))]
    public Lobby? Lobby { get; set; }
}
