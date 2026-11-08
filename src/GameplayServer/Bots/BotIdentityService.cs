namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Hands out every bot's name, and is the only place a name is made.
/// </summary>
/// <remarks>
/// <para>
/// <b>The counter only moves forward.</b> A released number is never handed out
/// again, so two bots cannot hold one name however often the population changes:
/// a re-used <c>BOT-1</c> is a name a client may still be showing for the bot that
/// left, and a room whose roster says one thing and whose array says another is
/// the same class of fault as a duplicated slot. The names therefore run
/// <c>BOT-1</c>, <c>BOT-2</c>, … across the process's whole life, and a restart
/// begins again at one because the room begins again too.
/// </para>
/// <para>
/// <b>Uniqueness is still checked rather than assumed.</b> <see cref="Allocate"/>
/// refuses a name that is active, which can only happen if a caller hands a name
/// back wrongly, and that refusal is a bug made visible instead of a duplicate
/// bot on the wire.
/// </para>
/// <para>
/// The mechanism is centralized here rather than in a controller so that no bot
/// can be created without passing through it: <see cref="BotPopulationService"/>
/// is the only caller, and it allocates before it registers anything.
/// </para>
/// </remarks>
public sealed class BotIdentityService
{
    /// <summary>Prefix every bot name starts with.</summary>
    public const string NamePrefix = "BOT-";

    private readonly Lock gate = new();
    private readonly HashSet<string> activeNames = [];
    private int lastNumber;

    /// <summary>
    /// Names of the bots that currently hold one, in no order.
    /// </summary>
    public IReadOnlyList<string> ActiveNames
    {
        get
        {
            lock (gate)
            {
                return [.. activeNames];
            }
        }
    }

    /// <summary>How many names are handed out and not yet released.</summary>
    public int ActiveCount
    {
        get
        {
            lock (gate)
            {
                return activeNames.Count;
            }
        }
    }

    /// <summary>
    /// The next identity, as <c>BOT-</c> and the next number.
    /// </summary>
    /// <returns>A name no active bot holds.</returns>
    /// <exception cref="InvalidOperationException">The next name is somehow active.</exception>
    public BotIdentity Allocate()
    {
        lock (gate)
        {
            lastNumber++;
            var name = NameForNumber(lastNumber);
            if (!activeNames.Add(name))
            {
                lastNumber--;
                throw new InvalidOperationException(
                    $"The bot name '{name}' is already active; a name is handed out once.");
            }

            return new BotIdentity(name, lastNumber);
        }
    }

    /// <summary>
    /// Releases a bot's name when the bot leaves.
    /// </summary>
    /// <param name="name">Name the bot held.</param>
    /// <returns><c>true</c> when the name was active.</returns>
    public bool Release(string name)
    {
        lock (gate)
        {
            return activeNames.Remove(name);
        }
    }

    /// <summary>Whether a name is one this service's format produces.</summary>
    /// <param name="name">Name to test.</param>
    /// <returns><c>true</c> for <c>BOT-</c> followed by a positive whole number.</returns>
    public static bool IsBotName(string? name)
    {
        if (name is null || !name.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = name[NamePrefix.Length..];
        return digits.Length > 0
            && digits[0] != '0'
            && int.TryParse(digits, out var number)
            && number > 0;
    }

    /// <summary>The name a sequence number is written as.</summary>
    /// <param name="number">Sequence number, counting from one.</param>
    /// <returns>The bot name.</returns>
    public static string NameForNumber(int number) => $"{NamePrefix}{number}";
}
