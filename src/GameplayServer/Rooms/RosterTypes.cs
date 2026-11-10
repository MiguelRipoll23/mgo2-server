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
/// <param name="Level">
/// Character level the room's rows state, or <c>null</c> when the read could state
/// none. The settings body writes it as its half
/// (<see cref="PlayerGearBodiesUtils.LevelByteFromEnd"/>), which is the unit the
/// client's LEVEL column shows.
/// </param>
/// <param name="Skills">The player's equipped skills, in the order they load out.</param>
public sealed record LobbyLoadout(
    byte Slot,
    short? Side,
    byte[] Appearance,
    int? Level,
    IReadOnlyList<LobbySkill> Skills);

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
    /// Identifier of the player's clan, written at <c>0x1f</c> of its roster entry
    /// (<c>0x13</c> in the short layout), or zero for a player in no clan and for a
    /// bot.
    /// </summary>
    /// <remarks>
    /// <b>This is the field the row's clan emblem hangs off.</b> The record names the
    /// clan and the client fetches that clan's 768-byte block over TCP by this id
    /// (<c>0x4b4a</c>), so an entry left at zero draws no emblem even when the clan
    /// name beside it is right. The sampled hosts fill it — <c>pcapng1</c> and
    /// <c>pcapng2</c> hold <c>0x2284</c> on the two <c>gta6</c> entries and
    /// <c>0x04cb</c> once — and the value is the server's, not the peer's: a join
    /// request ends with the clan's name and never with its id.
    /// </remarks>
    public int ClanIdentifier { get; set; }

    /// <summary>
    /// The 23-byte appearance block the player announced in its join request, empty
    /// when the record carried none.
    /// </summary>
    /// <remarks>
    /// <b>The recorded host re-broadcasts the client's own block rather than reading
    /// the character's saved appearance.</b> <c>briefing-then-solo-game</c>'s join
    /// request carries <c>1e 45 2f 57 39 67 68 0e 0e 00 0e 0e 00 00 0b 16 00 0e 0e
    /// 07 0f 00 00</c> at offset <c>0x10</c> of its 140-byte body, and the loadout the
    /// host writes for that player carries those bytes as its trailing block
    /// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §8.4). A character whose saved row
    /// states no colours is therefore still announced with the face it plays as.
    /// </remarks>
    public byte[] AnnouncedAppearance { get; set; } = [];

    /// <summary>
    /// The peer's public/private endpoint pairs, copied from its handshake and
    /// written into the roster's twelve-byte endpoint field.
    /// </summary>
    public byte[] AddressData { get; set; } = [];

    /// <summary>
    /// High three bytes of the character id a bot's roster entry carries: the
    /// <c>0x7f</c> family the recorded hosts' bots are written in, whose low byte
    /// is the bot's own slot (<see cref="BuildRecord"/>).
    /// </summary>
    public const int BotCharacterIdBase = 0x7F000000;

    /// <summary>
    /// Whether this member is a bot this host added rather than a peer.
    /// </summary>
    /// <remarks>
    /// <b>A bot's entry is the short layout with a zero at <c>0x10</c>.</b> The
    /// recorded hosts' roster entries separate their own bots from their players
    /// by exactly that byte — <c>0x00</c> on the 51 <c>BOT-01</c>…<c>BOT-11</c>
    /// entries and on the ten name-less records, <c>0x01</c> or <c>0x02</c> on the
    /// rest (<c>docs/Shared/Utils/PlayerProfileRecordLayout.cs</c> and
    /// <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c>) — and a bot has no endpoint
    /// pairs to announce, so its record is the twelve-bytes-shorter layout.
    /// </remarks>
    public bool IsBot { get; init; }

    /// <summary>Builds the player-profile record that puts this player on a peer's roster.</summary>
    /// <remarks>
    /// <b>A bot's record carries the recorded hosts' own family at <c>0x08</c>.</b>
    /// Every captured bot entry writes a <c>0x7f</c> character id whose low byte is
    /// the bot's player-table slot (<c>pcapng1</c> t+0.562: <c>BOT-01</c> in slot 13
    /// with <c>0d 00 00 7f</c>), which no character row holds — and that is the
    /// point. A row id is one the client has no model for: announced that way, its
    /// netplayer cannot be built, and the run of 2026-11-09 13:08 failed exactly
    /// there (`netplayer/parts/pl_net_parts.cc line 936: skel not found`) with the
    /// bots absent from the map. The row is still what a bot plays *as* — the
    /// settings burst reads its appearance from <see cref="CharacterId"/> — but the
    /// entry names it in the family the client resolves without a row.
    /// </remarks>
    /// <param name="includeEndpointPairs">Whether to emit the twelve-byte endpoint field.</param>
    /// <returns>The record body.</returns>
    public byte[] BuildRecord(bool includeEndpointPairs = true)
    {
        var characterIdentifier = IsBot
            ? BotCharacterIdBase | SessionSlot
            : CharacterId;

        return PlayerProfileRecordUtility.Build(
            isHostEntry: false,
            RosterIndex,
            characterIdentifier,
            Name,
            ClanName,
            SessionSlot,
            clanIdentifier: IsBot ? 0 : ClanIdentifier,
            addressData: AddressData,
            // A bot's entry carries zero at 0x10, measured on the recorded hosts'
            // own BOT entries; a player's keeps the value the captures put there.
            unresolvedByte: IsBot
                ? (byte)0
                : PlayerProfileRecordUtility.PlayerRecordUnresolvedByte,
            includeEndpointPairs: includeEndpointPairs && !IsBot,
            // The entry names its player at offset 4 by the same key as 0x05 and
            // 0x07: the key the room's player table holds it under.
            rosterField: SessionSlot);
    }
}
