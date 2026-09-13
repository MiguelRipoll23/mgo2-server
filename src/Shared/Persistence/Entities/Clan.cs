using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>A player clan.</summary>
[Table("clans")]
public sealed class Clan
{
    /// <summary>Identifier of the clan.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Name of the clan.</summary>
    [Column("name")]
    [MaxLength(15)]
    public required string Name { get; set; }

    /// <summary>Membership row of the clan leader.</summary>
    [Column("leader_id")]
    public int? LeaderIdentifier { get; set; }

    /// <summary>Short comment shown on the clan card.</summary>
    [Column("comment")]
    [MaxLength(128)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>Notice published by the clan leader.</summary>
    [Column("notice")]
    [MaxLength(512)]
    public string Notice { get; set; } = string.Empty;

    /// <summary>Unix timestamp the notice was published at.</summary>
    [Column("notice_time")]
    public long NoticeTime { get; set; }

    /// <summary>Membership row of the member that wrote the notice.</summary>
    [Column("notice_writer_id")]
    public int? NoticeWriterIdentifier { get; set; }

    /// <summary>Membership row of the member currently holding the emblem editor.</summary>
    [Column("emblem_editor_id")]
    public int? EmblemEditorIdentifier { get; set; }

    /// <summary>Published clan emblem.</summary>
    [Column("emblem")]
    public byte[]? Emblem { get; set; }

    /// <summary>Clan emblem currently being edited.</summary>
    [Column("emblem_wip")]
    public byte[]? EmblemWorkInProgress { get; set; }

    /// <summary>Whether the clan accepts open applications.</summary>
    [Column("open")]
    public int Open { get; set; } = 1;

    /// <summary>Timestamp without time zone the clan was created at.</summary>
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>Membership row of the clan leader.</summary>
    [ForeignKey(nameof(LeaderIdentifier))]
    public ClanMember? Leader { get; set; }

    /// <summary>Members of the clan.</summary>
    public ICollection<ClanMember> Members { get; set; } = [];
}

/// <summary>Membership of a character in a clan.</summary>
[Table("clans_members")]
public sealed class ClanMember
{
    /// <summary>Identifier of the membership row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Clan the member belongs to.</summary>
    [Column("clan_id")]
    public int ClanIdentifier { get; set; }

    /// <summary>Character that is a member.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Rank of the member inside the clan.</summary>
    [Column("rank")]
    public int Rank { get; set; }

    /// <summary>Clan the member belongs to.</summary>
    [ForeignKey(nameof(ClanIdentifier))]
    public Clan? Clan { get; set; }

    /// <summary>Character that is a member.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>
/// Pending application to join a clan. The leader accepts or declines it, which
/// consumes the row.
/// </summary>
[Table("clan_applications")]
public sealed class ClanApplication
{
    /// <summary>Identifier of the application.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Clan applied to.</summary>
    [Column("clan_id")]
    public int ClanIdentifier { get; set; }

    /// <summary>Character that applied.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Timestamp without time zone the application was submitted at.</summary>
    [Column("applied_at")]
    public DateTime AppliedAt { get; set; }

    /// <summary>Clan applied to.</summary>
    [ForeignKey(nameof(ClanIdentifier))]
    public Clan? Clan { get; set; }

    /// <summary>Character that applied.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
