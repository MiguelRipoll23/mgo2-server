using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// The gameplay-server half of <see cref="ServerOptions"/>: the room the
/// dedicated host opens and the settings its bodies state.
/// </summary>
public sealed partial class ServerOptions
{
    /// <summary>
    /// SNE dog-tag count of the room: the number Snake must collect, written at body
    /// <c>0x8e</c>. The working capture carries <c>2</c>; a room that plays SNE states
    /// its own.
    /// </summary>
    public int GameplayServerSneakingDogTags { get; set; } = 2;

    /// <summary>
    /// Team-kill kick of the room: how many team kills a player may make before the
    /// room removes them, body <c>0x4e</c> (block <c>+0xB6</c>, the <c>0x4313</c>
    /// word at <c>0x147</c>). The body states one byte; the client's setting is a
    /// <c>u16</c>.
    /// </summary>
    public int GameplayServerTeamKillKick { get; set; } = 3;

    /// <summary>
    /// Sneaking Mission SNAKE count: how many times Snake must be defeated for Red and
    /// Blue to win, body <c>0x96</c> (block <c>+0xBD</c>, wire <c>0x165</c>). The
    /// client clamps it to 1–5.
    /// </summary>
    public int GameplayServerSnakeDefeats { get; set; } = 3;

    /// <summary>
    /// Whole-minute limit of the room, in minutes, body <c>0x53</c>–<c>0x54</c>. The
    /// body states seconds; the captured rooms state <c>0</c>, <c>3</c>, <c>4</c> or
    /// <c>5</c> minutes.
    /// </summary>
    public int GameplayServerMinuteLimitMinutes { get; set; } = 3;

    /// <summary>
    /// The host's stance, body <c>0x60</c>: the client's <c>HOST_STANCE_*</c> value,
    /// <c>0</c>–<c>9</c> with <c>8</c> an unnamed gap. <c>0</c> is <i>Casual</i>;
    /// <c>2</c> is <i>Newbies Welcome</i> and is the default here.
    /// </summary>
    public int GameplayServerHostStance { get; set; } = 2;

    /// <summary>
    /// Capture's extra time, body <c>0x97</c>: extend a Capture round until a victor
    /// emerges.
    /// </summary>
    public bool GameplayServerCaptureExtraTime { get; set; }

    /// <summary>
    /// The first unnamed host flag, body <c>0x98</c>. It is bit 0 of the settings
    /// block's byte at <c>+0xC7</c>; the client neither reads nor writes that byte.
    /// </summary>
    public bool GameplayServerHostFlag0 { get; set; } = true;

    /// <summary>
    /// The second unnamed host flag, body <c>0x99</c>: bit 2 of the same byte.
    /// </summary>
    public bool GameplayServerHostFlag2 { get; set; } = true;

    /// <summary>
    /// Deathmatch rounds, body <c>0x8f</c>. The create-game selector offers 1–5;
    /// <c>2</c> is the default here.
    /// </summary>
    public int GameplayServerDeathmatchRounds { get; set; } = 2;

    /// <summary>
    /// Stealth Deathmatch rounds, body <c>0x90</c>. The default is <c>4</c>, the
    /// value the settings block's tail carries.
    /// </summary>
    public int GameplayServerStealthDeathmatchRounds { get; set; } = 4;

    /// <summary>
    /// Sneaking Capture rounds, body <c>0x91</c>. The default is <c>2</c>.
    /// </summary>
    public int GameplayServerSneakingCaptureRounds { get; set; } = 2;

    /// <summary>
    /// Race rounds, body <c>0x92</c>. The default is <c>2</c>.
    /// </summary>
    public int GameplayServerRaceRounds { get; set; } = 2;

    /// <summary>
    /// Deathmatch ticket count written at body <c>0x94</c> when the room's rule bank
    /// states none. A bank's own DM tickets (<c>GAMEPLAY_SERVER_RULE_TIMERS</c> word
    /// 11) take precedence.
    /// </summary>
    public int GameplayServerDeathmatchTickets { get; set; } = 99;

    /// <summary>
    /// The room's tail word, body <c>0x9e</c>–<c>0x9f</c>: the counter that holds
    /// across a room's own bodies and names no known room fact.
    /// </summary>
    public int GameplayServerRoomTailWord { get; set; } = 0x0ee5;

    /// <summary>
    /// Checkpoint count of the room: the number a checkpoint rule requires, written at
    /// body <c>0x95</c>. The working capture carries <c>5</c>.
    /// </summary>
    public int GameplayServerCheckpoints { get; set; } = 5;

    /// <summary>
    /// Whether the room assigns the unique characters — Snake and Metal Gear Mk.II —
    /// at the start of every Sneaking or Team Sneaking round.
    /// </summary>
    /// <remarks>
    /// The setting is body <c>0x57</c>/<c>0xa0</c> and the assigned pair body
    /// <c>0x9a</c>/<c>0x9b</c>. The client's own picker (<c>P2P_AssignRoleSlots</c>,
    /// <c>0x77b628</c>) advances an LCG and writes two distinct player slots; this
    /// server makes the same draw over the room's player slots.
    /// </remarks>
    public bool GameplayServerUniqueCharacters { get; set; }

    /// <summary>
    /// Slots the gameplay server's room admits, the host's own seat among them. It is
    /// the figure the room row and the host's settings row both state, and the one a
    /// browsing client draws as the room's capacity.
    /// </summary>
    /// <remarks>
    /// <b>The host holds a seat the row does not count.</b> This figure is what the
    /// room list states and what a browsing client draws, and it is the players' —
    /// the host's seat is added on the gameplay channel's side, as one more than the
    /// row states (`GameplayServer.Match.MatchService.RoomCapacity`). A room of
    /// sixteen therefore seats fifteen players and the host, which is the working
    /// room's shape: `.pcapngs/actions/actions.pcapng`'s roster lists a nameless host
    /// entry and `BOT-01` to `BOT-15`, and its kill feeds name the human in the
    /// sixteenth player slot — fifteen bots, one player, one host. A room advertised
    /// at sixteen that sized the fill from the row alone seated fourteen
    /// (2026-11-11 13:52), the one bot short.
    /// </remarks>
    public int GameplayServerMaxPlayers { get; set; } = 16;

    /// <summary>
    /// Level-limit base the host's settings row states: the level the room's limit
    /// is centred on, which the recorded room stated as 22.
    /// </summary>
    /// <remarks>
    /// It is written whether or not the limit is in force, because the client keeps the
    /// chosen band while the option is switched off — the working room's body carries a
    /// band with its enable bit clear.
    /// </remarks>
    public int GameplayServerLevelLimitBase { get; set; } = 22;

    /// <summary>
    /// Whether the room's level band is in force: the bit the client tests before it
    /// refuses a player whose level is outside the band.
    /// </summary>
    /// <remarks>
    /// The band is body <c>0x63</c>/<c>0x64</c> and this is the <c>0x400000</c> bit of
    /// the switch word at <c>0x4f</c>–<c>0x52</c>. Clear is what every captured room
    /// that leaves the option off carries, with its band retained.
    /// </remarks>
    public bool GameplayServerLevelLimitEnabled { get; set; }

    /// <summary>
    /// Half-width of the room's level band, in levels: the <c>±</c> around
    /// <see cref="GameplayServerLevelLimitBase"/>.
    /// </summary>
    /// <remarks>
    /// Body <c>0x63</c>. The capture that switches the limit on and fills the Create
    /// Game box with level 8 ± 2 carries <c>2</c> there and <c>8</c> at <c>0x64</c>, so
    /// the two bytes are the width and then the centre.
    /// </remarks>
    public int GameplayServerLevelLimitTolerance { get; set; } = 2;

    /// <summary>
    /// Briefing time of the gameplay server's room, in minutes: how long the room
    /// waits between two rounds.
    /// <para>
    /// The client pushes its own when a player hosts a room
    /// (<c>characters_host_settings.briefing_time</c>, the lobby's
    /// <c>0x4312</c> push), and a dedicated host has no such push: what its room
    /// briefs for is this setting. The gameplay body carries it at
    /// <c>0x61</c>–<c>0x62</c> as a sixteen-bit minute count, and the same number
    /// drives the briefing countdown.
    /// </para>
    /// </summary>
    public int GameplayServerBriefingMinutes { get; set; } = 1;

    /// <summary>
    /// Whether the room puts joiners on a side itself. Clear writes the room's
    /// <c>Auto Assign Teams</c> switch off, which is what makes the client show its
    /// Red / Blue / Auto selection screen; a player's Red or Blue choice is then
    /// stored from <c>0x4440</c>, and a player who leaves the screen on Auto keeps
    /// the balanced side the room assigned on join.
    /// </summary>
    public bool GameplayServerAutoAssignTeams { get; set; } = true;

    /// <summary>Display name of the account the gameplay server logs in with.</summary>
    public string GameplayServerAccountName { get; set; } = "server";

    /// <summary>
    /// Password of the gameplay server account. When unset, a random one is
    /// generated per process, so the account cannot be logged into by anyone
    /// who knows the default.
    /// </summary>
    public string? GameplayServerAccountPassword { get; set; }

    /// <summary>
    /// Name of the character the gameplay server plays as. It is
    /// <c>server-&lt;port&gt;</c> when it is not configured, which keeps every
    /// dedicated host on a character of its own without an index being handed
    /// out: the port is already unique and stable, and the registered
    /// peer-to-peer endpoint is filed per character. It is the same shape and the
    /// same value as <see cref="GameplayServerRoomName"/>'s fallback, so a host
    /// told nothing plays as the character its room is named after.
    /// </summary>
    public string GameplayServerCharacterName
    {
        get => gameplayServerCharacterName
            ?? DedicatedHostNameUtils.HostCharacterName(GameplayServerPort);
        set => gameplayServerCharacterName = value;
    }

    /// <summary>
    /// Clan name the gameplay server announces in its peer-to-peer
    /// player-profile record. A recorded record carries an empty clan name when
    /// the player has no clan, which is the case for a dedicated host.
    /// </summary>
    public string? GameplayServerClanName { get; set; }

    /// <summary>
    /// Address published to clients in place of each lobby's stored address.
    /// Only set when the advertised address is a routable address rather than a
    /// wildcard or loopback address.
    /// </summary>
    public string? OverrideIpAddress =>
        string.IsNullOrWhiteSpace(AdvertisedAddress) ||
        AdvertisedAddress is "0.0.0.0" or "127.0.0.1" or "localhost"
            ? null
            : AdvertisedAddress;

    /// <summary>
    /// Address this instance announces to clients and writes to the lobby rows:
    /// the advertised address when one is configured, and the wildcard address
    /// otherwise, which only a client on the same machine can reach.
    /// </summary>
    public string AnnouncedIpAddress => OverrideIpAddress ?? "0.0.0.0";

    /// <summary>
    /// Name of the room the gameplay server publishes. It is the configured game
    /// name, and the host prefix and this instance's own port when none is
    /// configured: the port is unique and stable, so two dedicated hosts never
    /// publish a room under one name. A blank configured name reads as none, so a
    /// room is never published under a name no client can show.
    /// </summary>
    public string GameplayServerRoomName =>
        string.IsNullOrWhiteSpace(GameplayServerGameName)
            ? $"server-{GameplayServerPort}"
            : GameplayServerGameName;

    /// <summary>Interval in seconds between two heartbeats of an owned row.</summary>
    public int LobbyHeartbeatIntervalSeconds => LobbiesRefreshIntervalMinutes * 60;

    /// <summary>
    /// Threshold in seconds after which a gameplay lobby is considered stale:
    /// twice <see cref="LobbyHeartbeatIntervalSeconds"/>, which is two hours at the
    /// default beat. One window answers two questions, and they are the same
    /// question asked twice: a lobby whose <c>updated_at</c> is older than it is
    /// not listed — so a client never sees a lobby whose server stopped, however
    /// long ago that was — and it is what the daily cleanup deletes.
    /// </summary>
    public int LobbyStaleSeconds => LobbyHeartbeatIntervalSeconds * 2;

    /// <summary>
    /// Threshold in seconds after which a room is considered stale: a minute. A
    /// populated room is kept alive by its host's ping report, which the client
    /// sends every half minute and which stamps <c>games.updated_at</c> through
    /// <c>UpdatePingsAsync</c>; a room hosted by a dedicated server by its own
    /// <see cref="MatchHeartbeatIntervalSeconds"/> beat. A minute of silence is
    /// therefore a host that has missed both of its beats, whatever kind it was,
    /// and the room is dropped from every list the moment it is that old — a
    /// client is never shown a room whose host is gone.
    /// <para>
    /// It is deliberately two client beats and not more. The room list is what a
    /// client joins from, so a room that is still listed is a room that can be
    /// joined, and a window any wider than the beat that fills it lists hosts that
    /// are no longer answering. The deletes are a different question with a
    /// different answer: they are a daily batch, so a stale row lingers in the
    /// table until midnight and is only ever invisible, never joined.
    /// </para>
    /// </summary>
    public int GameStaleSeconds { get; set; } = 60;

    /// <summary>
    /// Interval in seconds between two beats of a dedicated host's own match row,
    /// which is half of <see cref="GameStaleSeconds"/> so the beat lands once
    /// inside the window the room is listed within. A player-hosted room needs no
    /// such beat: its host's ping report is the beat.
    /// </summary>
    public int MatchHeartbeatIntervalSeconds => GameStaleSeconds / 2;
}
