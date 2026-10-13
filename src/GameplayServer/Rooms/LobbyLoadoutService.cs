using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Reads the room's own loadouts: for the host and every player in it, the slot
/// its record travels under, the side the room's rows put it on, and the
/// appearance and skills its own character saved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The read is of characters, and the settings burst used to send one
/// character's own appearance to everyone.</b> A loadout record is the player's
/// equipment, its colours and its skills (<c>rpdt.js</c>'s appearance block and
/// skill pairs, laid out in <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c>), and
/// the recorded host wrote its own character's into the burst — so every joiner
/// was shown the same face, the same gear and the same skills. Those fields are
/// rows this server already holds per character
/// (<see cref="CharacterService.GetAppearanceAsync"/>,
/// <see cref="CharacterService.GetEquippedSkillsAsync"/>), which is what makes
/// the burst answerable with the room's own players.
/// </para>
/// <para>
/// <b>The room's own key is the slot, and the host is one of the players.</b> The
/// captured bodies name the host's entry at slot byte <c>0x01</c> — the dedicated
/// host holds key zero in its own player table and the joiners hold the keys its
/// roster handed out — so the host is read like a player rather than left out. It
/// is in the room's own rows too: the host character is a row of
/// <c>game_players</c>, which is what gives it a side at all.
/// </para>
/// <para>
/// <b>A player the read cannot answer for is left out rather than guessed.</b> A
/// character with no appearance row, a field outside a byte, a skill outside the
/// parser's list: each is logged and the player simply is not named by the burst,
/// which then falls back to the recorded body for its own players. Announcing a
/// player with another character's face is the failure this replaces.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, which holds every player's key.</param>
/// <param name="match">This host's own room, whose rows name the sides.</param>
/// <param name="hostIdentity">Identity of the character this host plays as.</param>
/// <param name="gameService">The games domain's service, which owns the sides.</param>
/// <param name="characterService">The characters domain's service, which owns the appearances and skills.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyLoadoutService(
    RoomRosterService roster,
    MatchService match,
    HostIdentityService hostIdentity,
    GameService gameService,
    CharacterService characterService,
    ILogger<LobbyLoadoutService> logger)
{
    /// <summary>
    /// The room's players as loadouts, the host first.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    /// <returns>
    /// One entry per player the read could answer for, or an empty list while this
    /// host has published no room — in which case the burst writes the recorded
    /// bodies, as it did before this read existed.
    /// </returns>
    public async Task<List<LobbyLoadout>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var matchIdentifier = match.MatchIdentifier;
        if (matchIdentifier == 0)
        {
            logger.LogWarning(
                "This host has published no room yet, so the settings burst can name no player's own "
                + "loadout; the recorded bodies stay in it");
            return [];
        }

        var sides = await gameService.GetPlayerTeamsAsync(matchIdentifier, cancellationToken);
        var members = roster.Snapshot();
        var loadouts = new List<LobbyLoadout>(members.Count + 1);

        await AddAsync(
            loadouts,
            RoomRosterService.HostSessionSlot,
            (int)hostIdentity.PeerIdentifier,
            sides,
            cancellationToken);

        foreach (var member in members)
        {
            await AddAsync(loadouts, member.SessionSlot, member.CharacterId, sides, cancellationToken);
        }

        logger.LogDebug(
            "Match {GameIdentifier}: {LoadoutCount} of {PlayerCount} player(s) of the room have their own "
            + "loadout read for the settings burst",
            matchIdentifier,
            loadouts.Count,
            members.Count + 1);
        return loadouts;
    }

    /// <summary>
    /// Reads one player and files its loadout, or logs why it cannot be read.
    /// </summary>
    /// <param name="loadouts">List the loadout is added to.</param>
    /// <param name="slot">Key the player holds in the room's player table.</param>
    /// <param name="characterIdentifier">Character whose rows are read.</param>
    /// <param name="sides">The room's side per character.</param>
    /// <param name="cancellationToken">Token that cancels the reads.</param>
    private async Task AddAsync(
        List<LobbyLoadout> loadouts,
        byte slot,
        int characterIdentifier,
        Dictionary<int, short> sides,
        CancellationToken cancellationToken)
    {
        var appearance = await characterService.GetAppearanceAsync(characterIdentifier, cancellationToken);
        if (appearance is null || !LoadoutFramesUtils.TryAppearanceBlock(appearance, out var block))
        {
            logger.LogWarning(
                "Character {CharacterIdentifier}, in the room's slot {Slot}, has no appearance this record can "
                + "carry; the burst leaves it out rather than announcing another character's",
                characterIdentifier,
                slot);
            return;
        }

        var equipped = await characterService.GetEquippedSkillsAsync(characterIdentifier, cancellationToken);
        if (!LoadoutFramesUtils.TrySkillsOf(equipped, out var skills))
        {
            logger.LogWarning(
                "Character {CharacterIdentifier}, in the room's slot {Slot}, has equipped skills the loadout "
                + "record cannot carry; the burst leaves it out",
                characterIdentifier,
                slot);
            return;
        }

        var side = sides.TryGetValue(characterIdentifier, out var stored) ? stored : (short?)null;
        loadouts.Add(new LobbyLoadout(slot, side, block, skills));
        logger.LogDebug(
            "Character {CharacterIdentifier} is announced in slot {Slot}, on side {Side}, with {SkillCount} "
            + "skill(s)",
            characterIdentifier,
            slot,
            side is null ? "none" : side,
            skills.Count);
    }
}
