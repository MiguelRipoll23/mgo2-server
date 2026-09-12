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
    public DbSet<User> Users => Set<User>();

    /// <summary>Characters.</summary>
    public DbSet<Character> Characters => Set<Character>();

    /// <summary>Character appearances.</summary>
    public DbSet<CharacterAppearance> CharacterAppearances => Set<CharacterAppearance>();

    /// <summary>Character statistics.</summary>
    public DbSet<CharacterStatistics> CharacterStatistics => Set<CharacterStatistics>();

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

    /// <summary>End-of-round weapon tallies.</summary>
    public DbSet<WeaponTally> WeaponTallies => Set<WeaponTally>();

    /// <summary>Character friends and blocked entries.</summary>
    public DbSet<CharacterFriend> CharacterFriends => Set<CharacterFriend>();

    /// <summary>Saved skill sets.</summary>
    public DbSet<CharacterSkillSet> CharacterSkillSets => Set<CharacterSkillSet>();

    /// <summary>Equipped skills.</summary>
    public DbSet<CharacterEquippedSkill> CharacterEquippedSkills => Set<CharacterEquippedSkill>();

    /// <summary>Saved gear sets.</summary>
    public DbSet<CharacterGearSet> CharacterGearSets => Set<CharacterGearSet>();

    /// <summary>Saved host settings.</summary>
    public DbSet<CharacterHostSettings> CharacterHostSettings => Set<CharacterHostSettings>();

    /// <summary>Chat macros.</summary>
    public DbSet<CharacterChatMacro> CharacterChatMacros => Set<CharacterChatMacro>();

    /// <summary>Peer-to-peer endpoints registered by clients.</summary>
    public DbSet<CharacterConnection> CharacterConnections => Set<CharacterConnection>();

    /// <summary>
    /// Pins the plain timestamps to <c>timestamp without time zone</c>. The
    /// provider would otherwise store them as <c>timestamp with time zone</c>,
    /// which changes both the column type and the meaning of the stored value.
    /// </summary>
    /// <param name="configurationBuilder">Conventions being built.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>Maps every entity onto its table.</summary>
    /// <param name="modelBuilder">Model being built.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EntityModelConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
