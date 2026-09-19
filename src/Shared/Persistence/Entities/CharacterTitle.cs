using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A title a character has earned, latched once and kept forever.
/// <para>
/// Titles are stored rather than derived because their requirements are ratios,
/// and a ratio falls: a derived title would vanish the moment a player had a bad
/// week, after the client had already announced it. The row is inserted once and
/// never deleted, so the latching is the database's job rather than something
/// every read has to remember.
/// </para>
/// </summary>
[Table("characters_titles")]
public sealed class CharacterTitle
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character that earned the title.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>
    /// Rank identifier of the title, which is also its position in the client's
    /// badge table: 1 is the best rank a character can hold and 22 the worst the
    /// table carries. A character's worn title is the lowest rank it has latched.
    /// </summary>
    [Column("rank")]
    public int Rank { get; set; }

    /// <summary>Timestamp with time zone the title was unlocked at.</summary>
    [Column("unlocked_at")]
    public DateTimeOffset UnlockedAt { get; set; }

    /// <summary>Character that earned the title.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
