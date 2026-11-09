using System.Security.Cryptography;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Gets or creates the character a bot plays as, so a bot is named on the wire by
/// a real character and has a real appearance to be drawn with.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bot is a roster entry with a character identifier, and the identifier is a
/// row.</b> A bot announced under an identifier no character holds is one the
/// settings burst can read no appearance for, so every bot fell back to the
/// recorded host's single face — the reason a room of eight showed one body. The
/// row is created once per bot name and reused by every later run: the names are
/// <c>BOT-1</c>, <c>BOT-2</c>, … across the process's life
/// (<see cref="BotIdentityService"/>), so a restarted host finds the characters it
/// made last time rather than a second set.
/// </para>
/// <para>
/// <b>The characters hang off one account of their own.</b> A character row needs
/// an owner, and attaching bots to the gameplay host's account would put them in
/// that account's character list. The account here is never signed into; its
/// password is a random digest no client can reproduce.
/// </para>
/// <para>
/// The appearance is written once, from the captured looks
/// (<see cref="BotAppearanceUtils"/>), and never overwritten: a bot whose face was
/// changed keeps it, which is the same rule the dedicated host's appearance row
/// follows (<see cref="CharacterService.EnsureDedicatedHostAppearanceAsync"/>).
/// </para>
/// </remarks>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="characters">The characters domain's service.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotCharacterService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    CharacterService characters,
    ILogger<BotCharacterService> logger)
{
    /// <summary>Account every bot character belongs to.</summary>
    public const string AccountName = "mgo2-bots";

    /// <summary>Comment written on a bot's character row.</summary>
    public const string CharacterComment = "Bot";

    /// <summary>Password of the bots' account, which no client knows.</summary>
    private static readonly string AccountPassword =
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// The identifier of the character a bot plays as, created when the name holds
    /// none yet.
    /// </summary>
    /// <param name="identity">The bot's identity, which carries its name and number.</param>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    /// <returns>The character identifier the bot is named by.</returns>
    public async Task<int> EnsureAsync(BotIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var existing = await characters.FindByNameAsync(identity.Name, cancellationToken);
        var characterIdentifier = existing?.Identifier ?? 0;

        if (characterIdentifier == 0)
        {
            characterIdentifier = await CreateAsync(identity.Name, cancellationToken);
            logger.LogInformation(
                "Bot {BotName} was given character {CharacterIdentifier}",
                identity.Name,
                characterIdentifier);
        }

        await EnsureAppearanceAsync(characterIdentifier, identity.Number, cancellationToken);
        return characterIdentifier;
    }

    /// <summary>
    /// Creates the character row under the bots' account, returning the identifier
    /// of the row the name ended up holding.
    /// </summary>
    /// <remarks>
    /// The insert is idempotent on both tables for the same reason the gameplay
    /// host's own account is (<see cref="Identity.AccountService"/>): two passes
    /// cannot seat one bot twice, but a name that already exists must resolve to
    /// its row rather than fail the pass.
    /// </remarks>
    /// <param name="name">The bot's character name.</param>
    /// <param name="cancellationToken">Token that cancels the writes.</param>
    /// <returns>The character identifier.</returns>
    private async Task<int> CreateAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO accounts (display_name, password)
             VALUES ({AccountName}, {AccountPassword})
             ON CONFLICT (display_name) DO NOTHING
             """,
            cancellationToken);

        var accountIdentifier = await context.Accounts
            .Where(account => account.DisplayName == AccountName)
            .Select(account => account.Identifier)
            .FirstAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO characters (account_id, name, comment, created_at)
             VALUES ({accountIdentifier}, {name}, {CharacterComment}, {DateTimeOffset.UtcNow})
             ON CONFLICT (name) DO NOTHING
             """,
            cancellationToken);

        var characterIdentifier = await context.Characters
            .Where(character => character.Name == name)
            .Select(character => character.Identifier)
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return characterIdentifier;
    }

    /// <summary>
    /// Gives a bot its captured appearance when it has no row, so the settings burst
    /// carries a face and a voice rather than a zeroed block.
    /// </summary>
    /// <param name="characterIdentifier">Character the bot plays as.</param>
    /// <param name="number">The bot's identity number, which chooses its look.</param>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    private async Task EnsureAppearanceAsync(
        int characterIdentifier,
        int number,
        CancellationToken cancellationToken)
    {
        var present = await characters.GetAppearanceAsync(characterIdentifier, cancellationToken);
        if (present is not null)
        {
            return;
        }

        await characters.UpdateAppearanceAsync(
            characterIdentifier,
            appearance => BotAppearanceUtils.Apply(appearance, number),
            cancellationToken);
        logger.LogDebug(
            "Bot character {CharacterIdentifier} was given captured appearance {Appearance}",
            characterIdentifier,
            ((number - 1) % BotAppearanceUtils.Count) + 1);
    }
}
