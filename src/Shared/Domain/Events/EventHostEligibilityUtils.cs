using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rules that decide which room may host an event match.
/// <para>
/// A room hosts an event only if it was created to: it is named for the role and
/// it says it is dedicated. Without that, any player's room would be eligible and
/// a Survival match could be dropped into somebody's private game — which is why
/// the name and the flag are both required rather than either.
/// </para>
/// </summary>
public static class EventHostEligibilityUtils
{
    /// <summary>Name a room carries to be a Survival host.</summary>
    public const string SurvivalHostName = "SURVIVAL_HOST";

    /// <summary>Name a room carries to be a Tournament host.</summary>
    public const string TournamentHostName = "TOURNAMENT_HOST";

    /// <summary>Mode a room named for the Survival role runs.</summary>
    public const int SurvivalHostSubtype = EventConstants.SurvivalSelector;

    /// <summary>Mode a room named for the Tournament role runs.</summary>
    public const int TournamentHostSubtype = EventConstants.TournamentSelector;

    /// <summary>Mode a join request that stops before naming one is treated as naming.</summary>
    public const int UnnamedSubtype = -1;

    /// <summary>Players a Survival match itself may hold.</summary>
    public const int MatchPlayerCapacity = 16;

    /// <summary>Slots the dedicated host consumes beyond the players it hosts.</summary>
    public const int DedicatedHostPlayerSlots = 1;

    /// <summary>
    /// Whether a room name is one of the host-role names, so that it names a role
    /// rather than a room. The comparison is case-insensitive because a client
    /// types the name and sends it as typed.
    /// <para>
    /// The name is not reserved: any player may name a room with it, and the name
    /// on its own is not a claim to the role. Holding the role is still gated, by
    /// the dedicated flag rather than by the name. The room list is the one reader
    /// other than the eligibility rule: it leaves these rooms out, because a host
    /// is leased to a match rather than chosen by a player.
    /// </para>
    /// </summary>
    /// <param name="name">Name of the room.</param>
    public static bool IsReservedHostName(string? name) => HostSubtype(name) is not null;

    /// <summary>
    /// The mode a host-role name stands for, or null when the name names a room
    /// rather than a role. A dedicated room is created with the mode its name
    /// stands for, so the role decides the mode, not the lobby the room was opened
    /// in. The name is not reserved and is read only for a room that says it is
    /// dedicated.
    /// </summary>
    /// <param name="name">Name of the room.</param>
    public static int? HostSubtype(string? name)
    {
        if (string.Equals(name, SurvivalHostName, StringComparison.OrdinalIgnoreCase))
        {
            return SurvivalHostSubtype;
        }

        return string.Equals(name, TournamentHostName, StringComparison.OrdinalIgnoreCase)
            ? TournamentHostSubtype
            : null;
    }

    /// <summary>
    /// Whether a room is a dedicated event host: named for the role, and saying so
    /// in its host's own settings rather than having the name taken as the claim.
    /// </summary>
    /// <param name="name">Name of the room.</param>
    /// <param name="hostSettings">Settings of the character hosting it, or null when it has none.</param>
    public static bool IsDedicatedEventHost(string? name, CharacterHostSettings? hostSettings) =>
        IsReservedHostName(name) && IsDedicated(hostSettings);

    /// <summary>
    /// Whether a room is the Survival host role: named for it and saying so. It is
    /// narrower than <see cref="IsDedicatedEventHost"/> because the two roles are
    /// served by different rules — a Tournament host is handed its players by a
    /// draw, while a Survival host is leased a match, and only the Survival one may
    /// be entered by nobody but the two teams it was leased to.
    /// </summary>
    /// <param name="room">Room being asked about.</param>
    /// <param name="hostSettings">Settings of the character hosting it, or null when it has none.</param>
    public static bool IsSurvivalHost(Game room, CharacterHostSettings? hostSettings)
    {
        ArgumentNullException.ThrowIfNull(room);

        return string.Equals(room.Name, SurvivalHostName, StringComparison.OrdinalIgnoreCase)
            && IsDedicated(hostSettings);
    }

    /// <summary>
    /// Whether a room is the Tournament host role: named for it, saying so, and
    /// running the role's own mode. The mode is part of the definition here because
    /// a client reaches the Tournament host through the ordinary room screen and
    /// may name Free Battle as it joins, which is the one case a join is allowed to
    /// name a mode other than the room's — and the room has to be sure of its own
    /// mode before that exception can be granted.
    /// </summary>
    /// <param name="room">Room being asked about.</param>
    /// <param name="hostSettings">Settings of the character hosting it, or null when it has none.</param>
    public static bool IsTournamentHost(Game room, CharacterHostSettings? hostSettings)
    {
        ArgumentNullException.ThrowIfNull(room);

        return string.Equals(room.Name, TournamentHostName, StringComparison.OrdinalIgnoreCase)
            && IsDedicated(hostSettings)
            && room.LobbySubtype == TournamentHostSubtype;
    }

    /// <summary>
    /// Whether a host says it is hosting as a dedicated host.
    /// <para>
    /// The flag is the host's own claim about how it is hosting, read from its
    /// stored settings: a host that turns the toggle on after opening a room is
    /// read as dedicated rather than as it was when the room was created. A host
    /// that has never pushed its settings has no claim to make, so it is refused.
    /// </para>
    /// </summary>
    /// <param name="hostSettings">Settings of the hosting character, or null.</param>
    public static bool IsDedicated(CharacterHostSettings? hostSettings) =>
        hostSettings?.Dedicated is true;

    /// <summary>
    /// Whether every player in the room is the host and the host is there. A
    /// host that has already collected players is running a game, and a host that
    /// is absent cannot start one.
    /// </summary>
    /// <param name="hostIdentifier">Character hosting the room.</param>
    /// <param name="playerCharacterIdentifiers">Characters in the room.</param>
    public static bool IsIdle(int hostIdentifier, IEnumerable<int> playerCharacterIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(playerCharacterIdentifiers);

        var hostPresent = false;
        foreach (var characterIdentifier in playerCharacterIdentifiers)
        {
            if (characterIdentifier == hostIdentifier)
            {
                hostPresent = true;
                continue;
            }

            if (characterIdentifier != 0)
            {
                return false;
            }
        }

        return hostPresent;
    }

    /// <summary>
    /// Whether the room has room for the match, counting the dedicated host's own
    /// slot. A match that fits the players but not the host would start and then
    /// find nowhere to seat the host.
    /// </summary>
    /// <param name="maximumPlayers">Players the room holds.</param>
    /// <param name="participantCount">Players the match brings.</param>
    public static bool HasCapacity(int maximumPlayers, int participantCount) =>
        participantCount >= 0
        && participantCount <= MatchPlayerCapacity
        && participantCount + DedicatedHostPlayerSlots <= maximumPlayers;

    /// <summary>
    /// Whether a room may take a specific match: the mode has to agree, because a
    /// host is only dedicated to one, and the room has to seat everyone including
    /// itself.
    /// </summary>
    /// <param name="gameLobbySubtype">Mode of the room's lobby.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="maximumPlayers">Players the room holds.</param>
    /// <param name="participantCount">Players the match brings.</param>
    public static bool AcceptsMatch(
        int gameLobbySubtype,
        int matchType,
        int maximumPlayers,
        int participantCount) =>
        gameLobbySubtype == matchType && HasCapacity(maximumPlayers, participantCount);

    /// <summary>
    /// Whether a room may be entered by a join that names a mode. The request says
    /// which mode it is for and the room has to be running it, because a room
    /// entered under another mode leaves the client unable to state which lobby it
    /// is in.
    /// <para>
    /// Two things are not refused on this rule. A request that stops before the
    /// byte makes no claim about a mode, and a room created before the mode column
    /// existed cannot be asked for its own. The one mismatch that is allowed is the
    /// reference's: the Tournament host is reached through the ordinary room
    /// screen, which may name Free Battle as it joins.
    /// </para>
    /// </summary>
    /// <param name="room">Room being asked about.</param>
    /// <param name="hostSettings">Settings of the character hosting it, or null when it has none.</param>
    /// <param name="requestedSubtype">Mode the request named, or <see cref="UnnamedSubtype"/>.</param>
    public static bool AcceptsJoinMode(Game room, CharacterHostSettings? hostSettings, int requestedSubtype)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (requestedSubtype == UnnamedSubtype || room.LobbySubtype == 0)
        {
            return true;
        }

        return requestedSubtype == room.LobbySubtype
            || (requestedSubtype == LobbySubtypeConstants.FreeBattle && IsTournamentHost(room, hostSettings));
    }

    /// <summary>
    /// Whether a room may host a specific match: a dedicated event host, idle
    /// with its own host in it, in the match's own mode and sized for everyone.
    /// <para>
    /// The three rules are asked as one because they are one answer, which is
    /// whether the room can take the match. The assignment that leases a room and
    /// the readers that report which room is free both ask it here, so they cannot
    /// disagree about which rooms qualify.
    /// </para>
    /// <para>
    /// The mode that has to agree is the room's own, not the mode of the lobby the
    /// match happens to be in. A dedicated host is a room rented for a role, and
    /// the role sets the mode when the room is created, so a host opened anywhere
    /// can still serve the mode it is named for.
    /// </para>
    /// <para>
    /// A fourth rule joins the three when an environment is passed: the room's
    /// host has to have the settings it was asked for saved, read out of the
    /// host's own settings row. It is asked only when the event requires the room
    /// to be running that environment, so a deployment that does not ask it is
    /// not narrowed by a rule it never stated.
    /// </para>
    /// </summary>
    /// <param name="room">Room being asked about.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="participantCount">Players the match brings.</param>
    /// <param name="hostSettings">Settings of the character hosting it, or null when it has none.</param>
    /// <param name="requiredSettings">Environment the room has to be running, or null when any will do.</param>
    public static bool IsEligibleHost(
        Game room,
        int matchType,
        int participantCount,
        CharacterHostSettings? hostSettings,
        EventHostEnvironment? requiredSettings = null)
    {
        ArgumentNullException.ThrowIfNull(room);

        return IsDedicatedEventHost(room.Name, hostSettings)
            && IsIdle(
                room.HostIdentifier,
                room.Players.Select(player => player.CharacterIdentifier))
            && AcceptsMatch(room.LobbySubtype, matchType, room.MaximumPlayers, participantCount)
            && (requiredSettings is null
                || (hostSettings is not null
                    && EventHostSettingsEnvironmentUtils.HasSameStaticSettings(
                        EventHostSettingsEnvironmentUtils.ToEnvironment(hostSettings),
                        requiredSettings)));
    }
}
