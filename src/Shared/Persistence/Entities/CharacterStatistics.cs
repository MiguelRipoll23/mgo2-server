using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// Lifetime statistics of a character. Property names are the C# spelling of
/// the original <c>character_stats</c> columns, which are pinned by the
/// <see cref="ColumnAttribute"/> on each property.
/// </summary>
[Table("character_stats")]
public sealed class CharacterStatistics
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character these statistics belong to.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Total kills.</summary>
    [Column("kills")]
    public int Kills { get; set; }

    /// <summary>Total deaths.</summary>
    [Column("deaths")]
    public int Deaths { get; set; }

    /// <summary>Total team wins.</summary>
    [Column("wins")]
    public int Wins { get; set; }

    /// <summary>Accumulated score.</summary>
    [Column("score")]
    public int Score { get; set; }

    /// <summary>Rounds played.</summary>
    [Column("rounds")]
    public int Rounds { get; set; }

    /// <summary>Stuns delivered.</summary>
    [Column("stuns")]
    public int Stuns { get; set; }

    /// <summary>Stuns received.</summary>
    [Column("stuns_received")]
    public int StunsReceived { get; set; }

    /// <summary>Friendly-fire stuns delivered.</summary>
    [Column("stuns_friendly")]
    public int StunsFriendly { get; set; }

    /// <summary>Headshot kills.</summary>
    [Column("headshot_kills")]
    public int HeadshotKills { get; set; }

    /// <summary>Headshot deaths.</summary>
    [Column("headshot_deaths")]
    public int HeadshotDeaths { get; set; }

    /// <summary>Headshot stuns delivered.</summary>
    [Column("headshot_stuns")]
    public int HeadshotStuns { get; set; }

    /// <summary>Headshot stuns received.</summary>
    [Column("headshot_stuns_received")]
    public int HeadshotStunsReceived { get; set; }

    /// <summary>Lock-on kills.</summary>
    [Column("lock_kills")]
    public int LockKills { get; set; }

    /// <summary>Lock-on deaths.</summary>
    [Column("lock_deaths")]
    public int LockDeaths { get; set; }

    /// <summary>Lock-on stuns delivered.</summary>
    [Column("lock_stuns")]
    public int LockStuns { get; set; }

    /// <summary>Lock-on stuns received.</summary>
    [Column("lock_stuns_received")]
    public int LockStunsReceived { get; set; }

    /// <summary>Best consecutive kill streak.</summary>
    [Column("consecutive_kills")]
    public int ConsecutiveKills { get; set; }

    /// <summary>Best consecutive death streak.</summary>
    [Column("consecutive_deaths")]
    public int ConsecutiveDeaths { get; set; }

    /// <summary>Best consecutive headshot streak.</summary>
    [Column("consecutive_headshots")]
    public int ConsecutiveHeadshots { get; set; }

    /// <summary>Best consecutive team-deathmatch win streak.</summary>
    [Column("consecutive_tdm")]
    public int ConsecutiveTeamDeathmatch { get; set; }

    /// <summary>Enemies spotted.</summary>
    [Column("spotted")]
    public int Spotted { get; set; }

    /// <summary>Times the character was spotted.</summary>
    [Column("self_spotted")]
    public int SelfSpotted { get; set; }

    /// <summary>Enemies spotted while sneaking.</summary>
    [Column("snake_spotted")]
    public int SnakeSpotted { get; set; }

    /// <summary>Times the character was spotted while sneaking.</summary>
    [Column("snake_self_spotted")]
    public int SnakeSelfSpotted { get; set; }

    /// <summary>Suicides.</summary>
    [Column("suicides")]
    public int Suicides { get; set; }

    /// <summary>Salutes performed.</summary>
    [Column("salutes")]
    public int Salutes { get; set; }

    /// <summary>Radio messages sent.</summary>
    [Column("radio")]
    public int Radio { get; set; }

    /// <summary>Chat messages sent.</summary>
    [Column("chat")]
    public int Chat { get; set; }

    /// <summary>Close-quarters-combat holds applied.</summary>
    [Column("cqc_given")]
    public int CqcGiven { get; set; }

    /// <summary>Close-quarters-combat holds taken.</summary>
    [Column("cqc_taken")]
    public int CqcTaken { get; set; }

    /// <summary>Rolls performed.</summary>
    [Column("rolls")]
    public int Rolls { get; set; }

    /// <summary>Catapult launches performed.</summary>
    [Column("catapult")]
    public int Catapult { get; set; }

    /// <summary>Falls taken.</summary>
    [Column("falls")]
    public int Falls { get; set; }

    /// <summary>Times trapped.</summary>
    [Column("trapped")]
    public int Trapped { get; set; }

    /// <summary>Melee attacks delivered.</summary>
    [Column("melee")]
    public int Melee { get; set; }

    /// <summary>Melee attacks received, from the original <c>melee_rec</c> column.</summary>
    [Column("melee_rec")]
    public int MeleeReceived { get; set; }

    /// <summary>Seconds spent inside a box.</summary>
    [Column("box_time")]
    public int BoxTime { get; set; }

    /// <summary>Times a box was used.</summary>
    [Column("box_uses")]
    public int BoxUses { get; set; }

    /// <summary>Bases captured.</summary>
    [Column("bases_captured")]
    public int BasesCaptured { get; set; }

    /// <summary>Bases destroyed.</summary>
    [Column("bases_destroyed")]
    public int BasesDestroyed { get; set; }

    /// <summary>SOP destabilizations performed.</summary>
    [Column("sop_destab")]
    public int SopDestabilizations { get; set; }

    /// <summary>Rescue targets saved.</summary>
    [Column("gako_saved")]
    public int RescueTargetSaved { get; set; }

    /// <summary>Rescue targets defended.</summary>
    [Column("gako_defended")]
    public int RescueTargetDefended { get; set; }

    /// <summary>Times the rescue target was reached first.</summary>
    [Column("gako_first")]
    public int RescueTargetReachedFirst { get; set; }

    /// <summary>Rescue defenses performed.</summary>
    [Column("res_defend")]
    public int RescueDefends { get; set; }

    /// <summary>Seconds held with the rescue target.</summary>
    [Column("res_gako_time")]
    public int RescueHoldTime { get; set; }

    /// <summary>Times the rescue target was grabbed first.</summary>
    [Column("res_first_grab")]
    public int RescueFirstGrab { get; set; }

    /// <summary>Bombs disarmed.</summary>
    [Column("bomb_disarms")]
    public int BombDisarms { get; set; }

    /// <summary>Survivals achieved in survival-deathmatch.</summary>
    [Column("sdm_survivals")]
    public int SdmSurvivals { get; set; }

    /// <summary>Race checkpoints passed.</summary>
    [Column("race_checkpoints")]
    public int RaceCheckpoints { get; set; }

    /// <summary>Snake-mode wins.</summary>
    [Column("wins_snake")]
    public int SnakeWins { get; set; }

    /// <summary>Snake-mode kills.</summary>
    [Column("kills_snake")]
    public int SnakeKills { get; set; }

    /// <summary>Hold-ups performed in snake mode.</summary>
    [Column("snake_holdups")]
    public int SnakeHoldups { get; set; }

    /// <summary>Tags spawned in team-sneaking.</summary>
    [Column("snake_tags_spawned")]
    public int SnakeTagsSpawned { get; set; }

    /// <summary>Tags taken in team-sneaking.</summary>
    [Column("snake_tags_taken")]
    public int SnakeTagsTaken { get; set; }

    /// <summary>Times injured in snake mode.</summary>
    [Column("snake_injured")]
    public int SnakeInjured { get; set; }

    /// <summary>First team-sneaking objective grabs.</summary>
    [Column("tsne_grab1")]
    public int TeamSneakingFirstGrab { get; set; }

    /// <summary>Second team-sneaking objective grabs.</summary>
    [Column("tsne_grab2")]
    public int TeamSneakingSecondGrab { get; set; }

    /// <summary>Knife kills.</summary>
    [Column("knife_kills")]
    public int KnifeKills { get; set; }

    /// <summary>Knife stuns.</summary>
    [Column("knife_stuns")]
    public int KnifeStuns { get; set; }

    /// <summary>Boosts applied.</summary>
    [Column("boosts")]
    public int Boosts { get; set; }

    /// <summary>Scans performed.</summary>
    [Column("scans")]
    public int Scans { get; set; }

    /// <summary>Seconds spent with evasion gear, from the original <c>evg_time</c> column.</summary>
    [Column("evg_time")]
    public int EvasionTime { get; set; }

    /// <summary>Teammates woken up.</summary>
    [Column("wakeups")]
    public int Wakeups { get; set; }

    /// <summary>Team kills.</summary>
    [Column("team_kills")]
    public int TeamKills { get; set; }

    /// <summary>Times the character withdrew.</summary>
    [Column("withdrawals")]
    public int Withdrawals { get; set; }

    /// <summary>Points earned from assists.</summary>
    [Column("points_assist")]
    public int PointsAssist { get; set; }

    /// <summary>Points earned from bases.</summary>
    [Column("points_base")]
    public int PointsBase { get; set; }

    /// <summary>Soldiers trained.</summary>
    [Column("trained_soldiers")]
    public int TrainedSoldiers { get; set; }

    /// <summary>Seconds spent training.</summary>
    [Column("time_training")]
    public int TrainingTime { get; set; }

    /// <summary>Seconds spent as an instructor.</summary>
    [Column("time_instructor")]
    public int InstructorTime { get; set; }

    /// <summary>Seconds spent as a student.</summary>
    [Column("time_student")]
    public int StudentTime { get; set; }

    /// <summary>Total seconds played.</summary>
    [Column("time")]
    public int TotalTime { get; set; }

    /// <summary>Seconds played in snake mode.</summary>
    [Column("time_snake")]
    public int SnakeTime { get; set; }

    /// <summary>Seconds played on dedicated hosts.</summary>
    [Column("time_dedi")]
    public int DedicatedHostTime { get; set; }

    /// <summary>Serialized deathmatch statistics.</summary>
    [Column("stats_dm")]
    public string? DeathmatchStatistics { get; set; }

    /// <summary>Serialized team-deathmatch statistics.</summary>
    [Column("stats_tdm")]
    public string? TeamDeathmatchStatistics { get; set; }

    /// <summary>Serialized rescue statistics.</summary>
    [Column("stats_res")]
    public string? RescueStatistics { get; set; }

    /// <summary>Serialized capture statistics.</summary>
    [Column("stats_cap")]
    public string? CaptureStatistics { get; set; }

    /// <summary>Serialized base statistics.</summary>
    [Column("stats_base")]
    public string? BaseStatistics { get; set; }

    /// <summary>Serialized bomb statistics.</summary>
    [Column("stats_bomb")]
    public string? BombStatistics { get; set; }

    /// <summary>Serialized sneaking statistics.</summary>
    [Column("stats_sne")]
    public string? SneakingStatistics { get; set; }

    /// <summary>Serialized team-sneaking statistics.</summary>
    [Column("stats_tsne")]
    public string? TeamSneakingStatistics { get; set; }

    /// <summary>Serialized survival-deathmatch statistics.</summary>
    [Column("stats_sdm")]
    public string? SdmStatistics { get; set; }

    /// <summary>Serialized survival-capture statistics.</summary>
    [Column("stats_scap")]
    public string? ScapStatistics { get; set; }

    /// <summary>Serialized race statistics.</summary>
    [Column("stats_race")]
    public string? RaceStatistics { get; set; }

    /// <summary>Unix timestamp of the last update, or <c>null</c> when never updated.</summary>
    [Column("last_updated")]
    public int? LastUpdated { get; set; }

    /// <summary>Character these statistics belong to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
