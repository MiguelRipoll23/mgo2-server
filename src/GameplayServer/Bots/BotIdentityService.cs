namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Hands out every bot's name, and is the only place a name is made.
/// </summary>
/// <remarks>
/// <para>
/// <b>The counter only moves forward, until one byte runs out.</b> A released
/// number is never handed out again, so two bots cannot hold one name however
/// often the population changes:
/// a re-used <c>BOT-01</c> is a name a client may still be showing for the bot
/// that left, and a room whose roster says one thing and whose array says another
/// is the same class of fault as a duplicated slot. The names therefore run
/// <c>BOT-01</c>, <c>BOT-02</c>, … across the process's whole life, and a restart
/// begins again at one because the room begins again too.
/// </para>
/// <para>
/// <b>The number is a wire byte, so the sequence stops at 255 and reuses free
/// ones.</b> A bot's roster entry carries its number at <c>0x04</c>, <c>0x05</c>,
/// <c>0x07</c> and in the low byte of its character id, and a number past
/// <c>0xff</c> would truncate into those bytes — the entry would key on a bot it
/// is not. Past 255 the sequence therefore takes the lowest number no active bot
/// holds (<see cref="NextFreeNumber"/>), which a room that resets when it empties
/// rarely reaches and which still never collides with a live bot.
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
            var number = NextFreeNumber();
            var name = NameForNumber(number);
            if (!activeNames.Add(name))
            {
                throw new InvalidOperationException(
                    $"The bot name '{name}' is already active; a name is handed out once.");
            }

            lastNumber = number;
            return new BotIdentity(name, number);
        }
    }

    /// <summary>
    /// The number the next bot takes: one past the last, or — once the sequence
    /// has run past the byte a roster entry carries — the lowest number no active
    /// bot holds.
    /// </summary>
    /// <returns>A number from 1 to 255 that no active bot's name carries.</returns>
    /// <exception cref="InvalidOperationException">Every number of the byte is held.</exception>
    private int NextFreeNumber()
    {
        if (lastNumber < byte.MaxValue)
        {
            return lastNumber + 1;
        }

        for (var number = 1; number <= byte.MaxValue; number++)
        {
            if (!activeNames.Contains(NameForNumber(number)))
            {
                return number;
            }
        }

        throw new InvalidOperationException(
            "Every bot number the roster entry can carry is held by an active bot.");
    }

    /// <summary>Starts the name sequence over for a room that has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            activeNames.Clear();
            lastNumber = 0;
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
    /// <returns>
    /// <c>true</c> for <c>BOT-</c> followed by a positive whole number written to
    /// at least two digits: <c>BOT-01</c>, <c>BOT-42</c>, <c>BOT-100</c>.
    /// </returns>
    public static bool IsBotName(string? name)
    {
        if (name is null || !name.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = name[NamePrefix.Length..];
        return digits.Length >= 2
            && (digits.Length == 2 || digits[0] != '0')
            && int.TryParse(digits, out var number)
            && number > 0;
    }

    /// <summary>The name a sequence number is written as, zero-padded to two digits.</summary>
    /// <param name="number">Sequence number, counting from one.</param>
    /// <returns>The bot name.</returns>
    public static string NameForNumber(int number) => $"{NamePrefix}{number:00}";
}
