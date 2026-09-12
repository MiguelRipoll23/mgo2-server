using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>
/// Declares the column defaults of the schema. They are part of the observable
/// behaviour: the seed, the peer-to-peer endpoint upsert, the roster insert and
/// the host-vote insert do not name every column and rely on the database to
/// fill the rest.
/// </summary>
internal static class ColumnDefaultConfiguration
{
    /// <summary>Suffix of the key and reference properties, which never carry a default.</summary>
    private const string IdentifierSuffix = "Identifier";

    /// <summary>Applies the defaults of every table.</summary>
    /// <param name="modelBuilder">Model being built.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureCharacters(modelBuilder);
        ConfigureLobbies(modelBuilder);
        ConfigureClans(modelBuilder);
        ConfigureGames(modelBuilder);
        ConfigureMatchHistory(modelBuilder);
        ConfigureMailAndNews(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        ApplyDefault<User>(
            modelBuilder,
            0,
            nameof(User.Role),
            nameof(User.MainExperience),
            nameof(User.AlternateExperience));
        ApplyDefault<User>(modelBuilder, 3, nameof(User.Slots));
    }

    private static void ConfigureCharacters(ModelBuilder modelBuilder)
    {
        ApplyDefault<Character>(
            modelBuilder,
            0,
            nameof(Character.Rank),
            nameof(Character.HostScore),
            nameof(Character.HostVotes),
            nameof(Character.Experience),
            nameof(Character.CreationTime));
        ApplyDefault<Character>(modelBuilder, 1, nameof(Character.Active));
        ApplyDefault<Character>(modelBuilder, string.Empty, nameof(Character.Comment));

        ZeroEveryCounter<CharacterAppearance>(modelBuilder);
        ApplyDefault<CharacterFriend>(modelBuilder, 0, nameof(CharacterFriend.Type));

        ZeroEveryCounter<CharacterSkillSet>(modelBuilder);
        ApplyDefault<CharacterSkillSet>(modelBuilder, string.Empty, nameof(CharacterSkillSet.Name));

        ZeroEveryCounter<CharacterEquippedSkill>(modelBuilder);

        ZeroEveryCounter<CharacterGearSet>(modelBuilder);
        ApplyDefault<CharacterGearSet>(modelBuilder, string.Empty, nameof(CharacterGearSet.Name));

        ApplyDefault<CharacterChatMacro>(
            modelBuilder,
            0,
            nameof(CharacterChatMacro.Type),
            nameof(CharacterChatMacro.Index));
        ApplyDefault<CharacterChatMacro>(modelBuilder, string.Empty, nameof(CharacterChatMacro.Text));

        ZeroEveryCounter<CharacterStatistics>(modelBuilder);
        ApplyNow<CharacterConnection>(modelBuilder, nameof(CharacterConnection.UpdatedAt));
    }

    private static void ConfigureLobbies(ModelBuilder modelBuilder)
    {
        ApplyDefault<Lobby>(modelBuilder, 0, nameof(Lobby.PlayersCount));
        ApplyDefault<Lobby>(
            modelBuilder,
            false,
            nameof(Lobby.BeginnerOnly),
            nameof(Lobby.ExpansionOnly),
            nameof(Lobby.NoHeadshot),
            nameof(Lobby.ReplaysOnly));        ApplyNow<Lobby>(
            modelBuilder,
            nameof(Lobby.CreatedAt),
            nameof(Lobby.UpdatedAt));
}

    private static void ConfigureClans(ModelBuilder modelBuilder)
    {
        ApplyDefault<Clan>(modelBuilder, string.Empty, nameof(Clan.Comment), nameof(Clan.Notice));
        ApplyDefault<Clan>(modelBuilder, 0, nameof(Clan.NoticeTime));
        ApplyDefault<Clan>(modelBuilder, 1, nameof(Clan.Open));
        ApplyNow<Clan>(modelBuilder, nameof(Clan.CreatedAt));

        ApplyDefault<ClanMember>(modelBuilder, 0, nameof(ClanMember.Rank));
        ApplyNow<ClanApplication>(modelBuilder, nameof(ClanApplication.AppliedAt));
    }

    private static void ConfigureGames(ModelBuilder modelBuilder)
    {
        ApplyDefault<Game>(
            modelBuilder,
            string.Empty,
            nameof(Game.Password),
            nameof(Game.Comment));
        ApplyDefault<Game>(
            modelBuilder,
            0,
            nameof(Game.CurrentGame),
            nameof(Game.Stance),
            nameof(Game.Ping),
            nameof(Game.Status));
        ApplyDefault<Game>(modelBuilder, 8, nameof(Game.MaximumPlayers));
        ApplyDefault<Game>(modelBuilder, "[]", nameof(Game.Games));
        ApplyDefault<Game>(modelBuilder, "{}", nameof(Game.Common), nameof(Game.Rules));
        ApplyNow<Game>(modelBuilder, nameof(Game.CreatedAt), nameof(Game.UpdatedAt));

        ApplyDefault<GamePlayer>(modelBuilder, (short)0, nameof(GamePlayer.Team));
        ApplyDefault<GamePlayer>(modelBuilder, 0, nameof(GamePlayer.Ping));
        ApplyNow<GamePlayer>(modelBuilder, nameof(GamePlayer.JoinedAt));
    }

    private static void ConfigureMatchHistory(ModelBuilder modelBuilder)
    {
        ApplyNow<HostReview>(modelBuilder, nameof(HostReview.ReviewedAt));

        ApplyDefault<RoundReport>(
            modelBuilder,
            0,
            nameof(RoundReport.Seconds),
            nameof(RoundReport.Experience));
        ApplyDefault<RoundReport>(
            modelBuilder,
            (short)0,
            nameof(RoundReport.TeamWin),
            nameof(RoundReport.LobbySubtype));
        ApplyDefault<RoundReport>(modelBuilder, false, nameof(RoundReport.Aborted));
        ApplyNow<RoundReport>(modelBuilder, nameof(RoundReport.CreatedAt));

        ApplyDefault<WeaponTally>(
            modelBuilder,
            (short)0,
            nameof(WeaponTally.ValueA),
            nameof(WeaponTally.ValueB),
            nameof(WeaponTally.ValueC));
    }

    private static void ConfigureMailAndNews(ModelBuilder modelBuilder)
    {
        ApplyDefault<MailMessage>(
            modelBuilder,
            string.Empty,
            nameof(MailMessage.SenderName),
            nameof(MailMessage.RecipientName),
            nameof(MailMessage.Subject),
            nameof(MailMessage.Body));
        ApplyDefault<MailMessage>(
            modelBuilder,
            false,
            nameof(MailMessage.RecipientRead),
            nameof(MailMessage.RecipientDeleted),
            nameof(MailMessage.SenderRead),
            nameof(MailMessage.SenderDeleted));
        ApplyNow<MailMessage>(modelBuilder, nameof(MailMessage.SentAt));

        ApplyDefault<GameMasterMail>(
            modelBuilder,
            string.Empty,
            nameof(GameMasterMail.SenderName),
            nameof(GameMasterMail.Subject),
            nameof(GameMasterMail.Body));
        ApplyNow<GameMasterMail>(modelBuilder, nameof(GameMasterMail.SentAt));

        ApplyDefault<NewsArticle>(modelBuilder, false, nameof(NewsArticle.Important));
    }

    /// <summary>Applies a store default to each named property of an entity.</summary>
    /// <typeparam name="TRow">Entity the properties belong to.</typeparam>
    /// <param name="modelBuilder">Model being built.</param>
    /// <param name="value">Value the database fills in when the column is omitted.</param>
    /// <param name="propertyNames">Names of the properties to give the default to.</param>
    private static void ApplyDefault<TRow>(ModelBuilder modelBuilder, object value, params string[] propertyNames)
        where TRow : class
    {
        var entity = modelBuilder.Entity<TRow>();

        foreach (var propertyName in propertyNames)
        {
            entity.Property(propertyName).HasDefaultValue(value);
        }
    }

    /// <summary>Applies a <c>now()</c> store default to each named property of an entity.</summary>
    /// <typeparam name="TRow">Entity the properties belong to.</typeparam>
    /// <param name="modelBuilder">Model being built.</param>
    /// <param name="propertyNames">Names of the timestamp properties.</param>
    private static void ApplyNow<TRow>(ModelBuilder modelBuilder, params string[] propertyNames)
        where TRow : class
    {
        var entity = modelBuilder.Entity<TRow>();

        foreach (var propertyName in propertyNames)
        {
            entity.Property(propertyName).HasDefaultValueSql("now()");
        }
    }

    /// <summary>
    /// Applies a zero default to every integer column of an entity apart from its
    /// keys and its references, which is how the counter, slot and equipment
    /// tables are declared: a new row starts from nothing.
    /// </summary>
    /// <typeparam name="TRow">Entity to configure.</typeparam>
    /// <param name="modelBuilder">Model being built.</param>
    private static void ZeroEveryCounter<TRow>(ModelBuilder modelBuilder)
        where TRow : class
    {
        var entity = modelBuilder.Entity<TRow>();

        foreach (var property in typeof(TRow)
                     .GetProperties()
                     .Where(property => property.PropertyType == typeof(int))
                     .Where(property => !property.Name.EndsWith(IdentifierSuffix, StringComparison.Ordinal)))
        {
            entity.Property(property.Name).HasDefaultValue(0);
        }
    }
}
