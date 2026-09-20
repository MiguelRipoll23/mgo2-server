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
/// There is no "in this lobby since" column: nothing on the wire carries one,
/// and no reader wanted it, which is the same test the character row's own
/// write-only columns failed.
/// </para>
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

    /// <summary>
    /// Timestamp of the last heartbeat. It is this table's <c>updated_at</c>, under
    /// the name that says what it means: the readers drop a row that has left
    /// <c>CharacterPresenceService.StaleAfter</c> for themselves, and the daily sweep
    /// deletes the rows past the same window.
    /// </summary>
    [Column("last_seen")]
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Character the row is about.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }

    /// <summary>Lobby the character is connected to.</summary>
    [ForeignKey(nameof(LobbyIdentifier))]
    public Lobby? Lobby { get; set; }
}
