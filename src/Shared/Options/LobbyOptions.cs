namespace Mgo2Server.Shared.Options;

/// <summary>
/// Identity and attributes a game lobby server publishes for the one gameplay
/// lobby it hosts. Every value comes from the environment, so a lobby exists
/// because a container was started for it rather than because a row was
/// seeded into the database beforehand.
/// </summary>
public sealed class LobbyOptions
{
    /// <summary>Display name of the lobby, at most sixteen characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Game type of the lobby, written as text: the name of a row of the
    /// <c>lobby_game_types</c> table, matched without regard to case and with
    /// spaces, underscores and hyphens treated alike. Freed battle is
    /// <c>FREE BATTLE</c> and training is <c>TRAINING</c>.
    /// </summary>
    public string Subtype { get; set; } = string.Empty;

    /// <summary>Port the lobby listens on.</summary>
    public int Port { get; set; }

    /// <summary>Whether the lobby only accepts beginners.</summary>
    public bool BeginnerOnly { get; set; }

    /// <summary>Whether the lobby only accepts expansion owners.</summary>
    public bool ExpansionOnly { get; set; }

    /// <summary>Whether the lobby disables headshots.</summary>
    public bool NoHeadshot { get; set; }

    /// <summary>Whether the lobby only accepts replays.</summary>
    public bool ReplaysOnly { get; set; }

    /// <summary>Maximum length of a lobby name in the wire format.</summary>
    private const int MaximumNameLength = 16;

    /// <summary>Whether an identity was configured at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Name);

    /// <summary>Rejects a configuration that cannot be published.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is incomplete.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException(
                "LOBBY_NAME is required: it names the gameplay lobby this server hosts.");
        }

        if (Name.Length > MaximumNameLength)
        {
            throw new InvalidOperationException(
                $"LOBBY_NAME must be at most {MaximumNameLength} characters, because the lobby " +
                $"list carries it in a fixed-width field. Received '{Name}'.");
        }

        if (string.IsNullOrWhiteSpace(Subtype))
        {
            throw new InvalidOperationException(
                "LOBBY_SUBTYPE is required: it selects the game type of the lobby from the " +
                "lobby_game_types table.");
        }

        if (Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException(
                $"LOBBY_PORT must be a TCP port between 1 and 65535. Received '{Port}'.");
        }
    }
}
