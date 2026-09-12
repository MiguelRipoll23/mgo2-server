using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the Entity Framework model. Building the model runs the same
/// validation the server runs on its first query, so a mapping mistake fails
/// here instead of in a container at runtime.
/// </summary>
public sealed class PersistenceModelTests
{
    /// <summary>
    /// Table names of the schema, so a rename in the model cannot pass unnoticed.
    /// </summary>
    private static readonly string[] ExpectedTables =
    [
        "character_connections",
        "character_stats",
        "characters",
        "characters_appearance",
        "characters_chatmacros",
        "characters_equipped_skills",
        "characters_friends",
        "characters_hostsettings",
        "characters_sets_gear",
        "characters_sets_skills",
        "clan_applications",
        "clans",
        "clans_members",
        "game_players",
        "game_rounds",
        "games",
        "gm_mail",
        "host_reviews",
        "lobbies",
        "lobby_game_types",
        "mail",
        "news",
        "round_reports",
        "sessions",
        "users",
        "weapon_tallies",
    ];

    [Fact]
    public void Every_mapped_entity_has_a_primary_key()
    {
        using var context = CreateContext();

        var keyless = context.Model
            .GetEntityTypes()
            .Where(entity => entity.FindPrimaryKey() is null)
            .Select(entity => entity.DisplayName())
            .ToList();

        Assert.Empty(keyless);
    }

    [Fact]
    public void Every_mapped_entity_is_stored_in_the_original_table()
    {
        using var context = CreateContext();

        var tables = context.Model
            .GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .ToList();

        Assert.Equal(ExpectedTables.OrderBy(name => name), tables.OrderBy(name => name));
    }

    private static Mgo2DatabaseContext CreateContext()
    {
        // The model is built from the options alone, so no connection is made.
        var options = new DbContextOptionsBuilder<Mgo2DatabaseContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=mgo2;Username=postgres")
            .Options;

        return new Mgo2DatabaseContext(options);
    }
}
