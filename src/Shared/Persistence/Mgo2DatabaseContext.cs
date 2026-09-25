using Mgo2Server.Shared.Persistence.Configurations;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence;

/// <summary>
/// Entity Framework Core context over the Metal Gear Online 2 database. The
/// table and column names are pinned to the original schema so the schema can
/// be created from this model and still be read by any other component.
/// </summary>
public sealed class Mgo2DatabaseContext(DbContextOptions<Mgo2DatabaseContext> options)
    : DbContext(options)
{
    /// <summary>Accounts.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Characters.</summary>
    public DbSet<Character> Characters => Set<Character>();

    /// <summary>Character appearances.</summary>
    public DbSet<CharacterAppearance> CharacterAppearances => Set<CharacterAppearance>();

    /// <summary>Login sessions.</summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>Listening endpoints published to clients.</summary>
    public DbSet<Lobby> Lobbies => Set<Lobby>();

    /// <summary>Game types a lobby can be configured with.</summary>
    public DbSet<LobbyGameType> LobbyGameTypes => Set<LobbyGameType>();

    /// <summary>Game rooms.</summary>
    public DbSet<Game> Games => Set<Game>();

    /// <summary>Room rosters.</summary>
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();

    /// <summary>Round-start roster snapshots.</summary>
    public DbSet<GameRound> GameRounds => Set<GameRound>();

    /// <summary>Clans.</summary>
    public DbSet<Clan> Clans => Set<Clan>();

    /// <summary>Clan memberships.</summary>
    public DbSet<ClanMember> ClanMembers => Set<ClanMember>();

    /// <summary>Pending clan applications.</summary>
    public DbSet<ClanApplication> ClanApplications => Set<ClanApplication>();

    /// <summary>News articles.</summary>
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();

    /// <summary>Mail deliveries.</summary>
    public DbSet<MailMessage> MailMessages => Set<MailMessage>();

    /// <summary>Mail addressed to the game masters.</summary>
    public DbSet<GameMasterMail> GameMasterMail => Set<GameMasterMail>();

    /// <summary>Host-rating votes.</summary>
    public DbSet<HostReview> HostReviews => Set<HostReview>();

    /// <summary>Round reports behind the match history.</summary>
    public DbSet<RoundReport> RoundReports => Set<RoundReport>();

    /// <summary>End-of-round weapon statistics.</summary>
    public DbSet<RoundWeaponStat> RoundWeaponStats => Set<RoundWeaponStat>();

    /// <summary>Character friends and blocked entries.</summary>
    public DbSet<CharacterFriend> CharacterFriends => Set<CharacterFriend>();

    /// <summary>Saved skill sets.</summary>
    public DbSet<CharacterSkillSet> CharacterSkillSets => Set<CharacterSkillSet>();

    /// <summary>Equipped skills.</summary>
    public DbSet<CharacterEquippedSkill> CharacterEquippedSkills => Set<CharacterEquippedSkill>();

    /// <summary>Titles a character has latched.</summary>
    public DbSet<CharacterTitle> CharacterTitles => Set<CharacterTitle>();

    /// <summary>Instructor reviews cast by students.</summary>
    public DbSet<InstructorReview> InstructorReviews => Set<InstructorReview>();

    /// <summary>The instructor each character has saved.</summary>
    public DbSet<CharacterInstructor> CharacterInstructors => Set<CharacterInstructor>();

    /// <summary>Presence-accumulated training seconds of a character.</summary>
    public DbSet<TrainingTime> TrainingTimes => Set<TrainingTime>();

    /// <summary>Saved gear sets.</summary>
    public DbSet<CharacterGearSet> CharacterGearSets => Set<CharacterGearSet>();

    /// <summary>Saved host settings.</summary>
    public DbSet<CharacterHostSettings> CharacterHostSettings => Set<CharacterHostSettings>();

    /// <summary>Chat macros.</summary>
    public DbSet<CharacterChatMacro> CharacterChatMacros => Set<CharacterChatMacro>();

    /// <summary>Peer-to-peer endpoints registered by clients.</summary>
    public DbSet<CharacterConnection> CharacterConnections => Set<CharacterConnection>();

    /// <summary>Gameplay options a character has stored.</summary>
    public DbSet<CharacterGameplayOptions> CharacterGameplayOptions => Set<CharacterGameplayOptions>();

    /// <summary>Which lobby each character is in right now.</summary>
    public DbSet<CharacterPresence> CharacterPresence => Set<CharacterPresence>();

    /// <summary>Teams formed on the event screens.</summary>
    public DbSet<EventTeam> EventTeams => Set<EventTeam>();

    /// <summary>Roster slots of the event teams.</summary>
    public DbSet<EventTeamMember> EventTeamMembers => Set<EventTeamMember>();

    /// <summary>Pairings of two event teams.</summary>
    public DbSet<EventMatch> EventMatches => Set<EventMatch>();

    /// <summary>Gameplay rooms leased to event matches.</summary>
    public DbSet<EventHostLease> EventHostLeases => Set<EventHostLease>();

    /// <summary>Rewards paid for completed event matches.</summary>
    public DbSet<EventRoundReward> EventRoundRewards => Set<EventRoundReward>();

    /// <summary>Per-player reports of a played event match.</summary>
    public DbSet<EventStatReport> EventStatReports => Set<EventStatReport>();

    /// <summary>Scheduled events a client may name when it enters one.</summary>
    public DbSet<EventSchedule> EventSchedules => Set<EventSchedule>();

    /// <summary>Reserved and registered Tournament places.</summary>
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();

    /// <summary>Seeded Tournament brackets.</summary>
    public DbSet<TournamentBracket> TournamentBrackets => Set<TournamentBracket>();

    /// <summary>Entrants of a Tournament bracket.</summary>
    public DbSet<TournamentSeed> TournamentSeeds => Set<TournamentSeed>();

    /// <summary>Played Tournament fixtures.</summary>
    public DbSet<TournamentResult> TournamentResults => Set<TournamentResult>();

    /// <summary>Maps every entity onto its table.</summary>
    /// <param name="modelBuilder">Model being built.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EntityModelConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
