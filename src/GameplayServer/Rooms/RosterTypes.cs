using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>One record of the roster run, with the type it travels as.</summary>
/// <param name="Type">Message type the record is sent under.</param>
/// <param name="Body">Record body.</param>
/// <param name="Ordinal">
/// Fourth byte: the record's position in the run. The recorded host fills it
/// for every record (head and host = 0, first player = 1, close = player count);
/// the repeat drops the head and keeps the rest. Later frames continue the count
/// rather than restart it.
/// </param>
public sealed record RosterRecord(ushort Type, byte[] Body, byte Ordinal = 0);

/// <summary>
/// One player of the room as the lobby-slot arrays name it: the key its session
/// holds in the room's 24-entry player table, and the side the room's own rows
/// put it on.
/// </summary>
/// <param name="Slot">
/// Key in the room's 24-entry player table, 1 to 23. It is the same byte the
/// player's roster entry carries at <c>0x05</c>/<c>0x07</c>
/// (<see cref="RosterMember.SessionSlot"/>), which is why the pair can name a
/// player at all: slot 0 is the host's own.
/// </param>
/// <param name="Team">
/// Team slot the room's rows hold for the player, or <c>null</c> when they hold
/// none — a player in the third role, one the client reported as 0xFE, or one the
/// room has a row for not at all.
/// </param>
public sealed record LobbyTeamSlot(byte Slot, short? Team);

/// <summary>
/// One skill a player's loadout record carries: the identifier the replay
/// parser's list names and the level it is equipped at, both base 1.
/// </summary>
/// <param name="Identifier">Skill identifier, 1 to 25 in the parser's list.</param>
/// <param name="Level">Level the skill is equipped at, 1 to 3.</param>
public sealed record LobbySkill(byte Identifier, byte Level);

/// <summary>
/// One player of the room as the settings burst names it: the slot its loadout
/// record travels under, the side the room's rows put it on, and the appearance
/// and skills its own character saved.
/// </summary>
/// <param name="Slot">
/// Key in the room's 24-entry player table, host zero. The record's slot byte is
/// this value plus one, which is the byte rpdt.js reads a loadout's slot from; the
/// captured bodies carry <c>0x01</c> for the host's own entry and the joiners'
/// keys (<c>0x05</c>, <c>0x07</c>, <c>0x0c</c>, <c>0x10</c>, <c>0x15</c>) after it.
/// </param>
/// <param name="Side">
/// Team slot the room's rows hold for the player, or <c>null</c> when they hold
/// none. It is coded onto the record as <c>2 × side</c>, which is the mapping the
/// replay console measured over 323 players that carry both a lobby-array group
/// and this byte: group 0 is byte <c>0</c> and group 1 is byte <c>2</c>.
/// </param>
/// <param name="Appearance">The 23-byte appearance block, in wire order.</param>
/// <param name="Skills">The player's equipped skills, in the order they load out.</param>
public sealed record LobbyLoadout(byte Slot, short? Side, byte[] Appearance, IReadOnlyList<LobbySkill> Skills);

/// <summary>One joining player on the room roster.</summary>
/// <param name="RemoteAddress">Endpoint the player is reached at ("address:port").</param>
/// <param name="RosterIndex">Slot the player fills, counted from zero.</param>
public sealed record RosterMember(string RemoteAddress, sbyte RosterIndex)
{
    /// <summary>
    /// The player's slot in the room's 24-entry network player table (offsets 0x05
    /// and 0x07 of its roster entry), the byte the replay console's <c>rpdt.js</c>
    /// calls <c>slot</c>. The host allocates it; the captured host does not echo
    /// the value its client announced (game1's client sent <c>0x05</c> and the
    /// host wrote <c>0x14</c>), and the values it hands out are distinct per live
    /// player and released when they leave. How the host picks it is unresolved,
    /// so this server takes the lowest free one.
    /// </summary>
    public byte SessionSlot { get; init; }

    /// <summary>
    /// Character identifier of the player, written at offset <c>0x08</c> of its
    /// roster entry. It is the identifier from the player's handshake, which is
    /// the character id; the captures confirm both sides (game1: the joiner's
    /// handshake <c>0x0001232f</c> is written as <c>74543</c>, the host's own as
    /// <c>40961</c>).
    /// </summary>
    public int CharacterId { get; set; }

    /// <summary>Character name the player announced.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Clan name the player announced; empty when it announced none.</summary>
    public string ClanName { get; set; } = string.Empty;

    /// <summary>
    /// The peer's public/private endpoint pairs, copied from its handshake and
    /// written into the roster's twelve-byte endpoint field.
    /// </summary>
    public byte[] AddressData { get; set; } = [];

    /// <summary>Builds the player-profile record that puts this player on a peer's roster.</summary>
    /// <returns>The record body.</returns>
    public byte[] BuildRecord() =>
        PlayerProfileRecordUtility.Build(
            isHostEntry: false,
            RosterIndex,
            CharacterId,
            Name,
            ClanName,
            SessionSlot,
            addressData: AddressData,
            unresolvedByte: PlayerProfileRecordUtility.PlayerRecordUnresolvedByte);
}
