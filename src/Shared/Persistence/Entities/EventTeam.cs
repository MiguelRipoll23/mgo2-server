using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A team formed on an event screen. It serves Survival and Tournament alike;
/// the event identifier and the match type say which one it belongs to.
/// <para>
/// The row is authoritative. The active-game snapshot the client receives is a
/// projection built from it at send time, so a disconnecting client never frees
/// team state and a reconnecting one is re-attached by identifier.
/// </para>
/// </summary>
[Table("event_teams")]
public sealed class EventTeam
{
    /// <summary>Identifier of the team.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character that leads the team; slot zero of the roster.</summary>
    [Column("owner_character_id")]
    public int OwnerCharacterIdentifier { get; set; }

    /// <summary>Lobby the team belongs to.</summary>
    [Column("lobby_id")]
    public int LobbyIdentifier { get; set; }

    /// <summary>Event the team entered, or the transient event correlation.</summary>
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Match type of the team, which is the lobby selector it was formed under.</summary>
    [Column("match_type")]
    public int MatchType { get; set; }

    /// <summary>Display name of the team.</summary>
    [Column("name")]
    [MaxLength(16)]
    public required string Name { get; set; }

    /// <summary>Free-form comment shown by the team screens.</summary>
    [Column("comment")]
    [MaxLength(128)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>Option bits of the team, of which bit zero marks a password.</summary>
    [Column("flag_bits")]
    public int FlagBits { get; set; }

    /// <summary>Join password; empty when the team is open.</summary>
    [Column("password")]
    [MaxLength(16)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Lifecycle state of the team.</summary>
    [Column("state")]
    public int State { get; set; }

    /// <summary>
    /// The serial of the team's record, which every client caches from the last
    /// team-record reply and echoes in the head of every later notification.
    /// <para>
    /// It is not a revision counter. The client compares the u16 it is given
    /// against its own copy and discards the packet with <c>-1018</c>, silently
    /// and with no dialog, when the two differ — so a value that moves here
    /// without the client being told drops every notification the team is sent
    /// from that point on, the roster additions included. The one packet that
    /// moves a client's copy is <c>0x49A8</c>, which carries the current serial
    /// in its head and the new one in its body, and nothing here sends it.
    /// </para>
    /// </summary>
    [Column("sequence")]
    public int Sequence { get; set; }

    /// <summary>Consecutive wins the team carries into its next match.</summary>
    [Column("consecutive_wins")]
    public int ConsecutiveWins { get; set; }

    /// <summary>Reward accumulated by the team so far.</summary>
    [Column("paid_reward")]
    public int PaidReward { get; set; }

    /// <summary>Timestamp the team was created at.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the team was last changed at.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Roster slots of the team.</summary>
    public ICollection<EventTeamMember> Members { get; set; } = [];
}

/// <summary>
/// One roster slot of an event team. Slot zero is the leader; the rest are
/// members that joined through an invitation or the joinable-team list.
/// </summary>
[Table("event_team_members")]
public sealed class EventTeamMember
{
    /// <summary>Team the slot belongs to.</summary>
    [Column("team_id")]
    public int TeamIdentifier { get; set; }

    /// <summary>Index of the slot inside the roster.</summary>
    [Column("slot")]
    public int Slot { get; set; }

    /// <summary>Character in the slot.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Name of the character, cached so a roster push need not join.</summary>
    [Column("name")]
    [MaxLength(16)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Participant state shown by the client for this slot.</summary>
    [Column("state")]
    public int State { get; set; }

    /// <summary>Experience used to derive the level the list screens show.</summary>
    [Column("experience")]
    public int Experience { get; set; }

    /// <summary>Team the slot belongs to.</summary>
    [ForeignKey(nameof(TeamIdentifier))]
    public EventTeam? Team { get; set; }
}
