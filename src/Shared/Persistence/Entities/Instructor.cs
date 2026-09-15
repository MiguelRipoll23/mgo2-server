using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// One instructor review: the star rating a student gave the host of a combat
/// training session, and whether the student recognised that host as their
/// instructor.
/// <para>
/// Append-only, so it is both the source of the instructor score gauge and the
/// career count of students trained. It deliberately outlives the relationship:
/// a student who re-graduates under someone else leaves the earlier reviews in
/// place, which is what keeps a career counter from going down.
/// </para>
/// </summary>
[Table("instructor_reviews")]
public sealed class InstructorReview
{
    /// <summary>Identifier of the review.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character that hosted the session and was reviewed.</summary>
    [Column("instructor_character_id")]
    public int InstructorCharacterIdentifier { get; set; }

    /// <summary>Character that was trained and cast the review.</summary>
    [Column("student_character_id")]
    public int StudentCharacterIdentifier { get; set; }

    /// <summary>Star rating the student awarded.</summary>
    [Column("rating")]
    public short Rating { get; set; }

    /// <summary>Whether the student recognised the instructor, which is what writes the relationship.</summary>
    [Column("recognised")]
    public bool Recognised { get; set; }

    /// <summary>
    /// Raw answer byte the client sent: bit 0 is the answer and bit 5 says the
    /// prompt was never shown. Kept because the two are not distinguishable from
    /// the boolean alone, and the value is the only record of what was asked.
    /// </summary>
    [Column("answer_byte")]
    public short AnswerByte { get; set; }

    /// <summary>Timestamp with time zone the review was cast at.</summary>
    [Column("reviewed_at")]
    public DateTimeOffset ReviewedAt { get; set; }

    /// <summary>Character that was reviewed.</summary>
    [ForeignKey(nameof(InstructorCharacterIdentifier))]
    public Character? Instructor { get; set; }

    /// <summary>Character that cast the review.</summary>
    [ForeignKey(nameof(StudentCharacterIdentifier))]
    public Character? Student { get; set; }
}

/// <summary>
/// The instructor a character has permanently saved, as the stats screen and the
/// join announcement both show it.
/// <para>
/// One row per student, because the client offers the recognition prompt exactly
/// once — its own copy of the saved instructor suppresses every later prompt — and
/// the prompt says the name cannot be erased. Re-graduating with a different
/// instructor re-parents the row.
/// </para>
/// </summary>
[Table("characters_instructors")]
public sealed class CharacterInstructor
{
    /// <summary>Character that was trained; one relationship per student.</summary>
    [Key]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Character the student recognised as their instructor.</summary>
    [Column("instructor_character_id")]
    public int InstructorCharacterIdentifier { get; set; }

    /// <summary>Name of the instructor at the moment of graduation.</summary>
    [Column("instructor_name")]
    [MaxLength(16)]
    public string InstructorName { get; set; } = string.Empty;

    /// <summary>Generation of the relationship: a first-generation instructor's students are second.</summary>
    [Column("generation")]
    public int Generation { get; set; }

    /// <summary>Rating the student gave at graduation.</summary>
    [Column("rating")]
    public short Rating { get; set; }

    /// <summary>Timestamp without time zone the relationship was written at.</summary>
    [Column("graduated_at")]
    public DateTime GraduatedAt { get; set; }

    /// <summary>
    /// When the instructor skill was awarded on the strength of this relationship,
    /// or <c>null</c> while the award is still pending.
    /// <para>
    /// The skill itself is served to every character at its maximum level, so this
    /// is not an ownership record: it is the latch that makes the award letter be
    /// sent once. Without it, every end-of-round report after the requirements are
    /// met would deliver another copy of the same letter.
    /// </para>
    /// </summary>
    [Column("instructor_skill_awarded_at")]
    public DateTime? InstructorSkillAwardedAt { get; set; }

    /// <summary>Character that was trained.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }

    /// <summary>Character that was recognised.</summary>
    [ForeignKey(nameof(InstructorCharacterIdentifier))]
    public Character? Instructor { get; set; }
}

/// <summary>
/// Presence-accumulated seconds a character spent in the training lobbies, split
/// the three ways the stats tail reports them.
/// <para>
/// Presence is not a shortcut here, it is the only signal that exists: a training
/// session reports nothing at all — the client sends no end-of-round frame for it —
/// so the interval between joining a room and leaving it is the whole measurement.
/// The split is by the hosting lobby: basic training counts as training mode, and
/// combat training counts as instructor time for whoever hosted and student time
/// for everyone else.
/// </para>
/// </summary>
[Table("character_training_times")]
public sealed class TrainingTime
{
    /// <summary>Character the totals belong to.</summary>
    [Key]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Seconds spent in basic training.</summary>
    [Column("training_mode_seconds")]
    public long TrainingModeSeconds { get; set; }

    /// <summary>Seconds spent instructing, which is hosting a combat training session.</summary>
    [Column("instructor_seconds")]
    public long InstructorSeconds { get; set; }

    /// <summary>Seconds spent as a student, which is everyone else in combat training.</summary>
    [Column("student_seconds")]
    public long StudentSeconds { get; set; }

    /// <summary>Character the totals belong to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
