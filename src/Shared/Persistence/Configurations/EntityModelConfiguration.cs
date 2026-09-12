using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>
/// Maps the entities of the database onto the tables, keys and relationships of
/// the schema. The mappings are split per domain so each file stays small enough
/// to read in one go.
/// </summary>
internal static partial class EntityModelConfiguration
{
    /// <summary>Applies every entity mapping.</summary>
    /// <param name="modelBuilder">Model being built.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureCharacters(modelBuilder);
        ConfigureLobbies(modelBuilder);
        ConfigureGames(modelBuilder);
        ConfigureClans(modelBuilder);
        ConfigureMail(modelBuilder);
        ConfigureMatchHistory(modelBuilder);
        ColumnDefaultConfiguration.Apply(modelBuilder);
    }
}
