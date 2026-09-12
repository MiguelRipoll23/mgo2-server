namespace Mgo2Server.Shared.Constants;

/// <summary>Command identifiers of the room browser, the room and automatching.</summary>
public static partial class CommandConstants
{
    // Room browser (0x43xx) -------------------------------------------------

    /// <summary>Lists the rooms in the lobby.</summary>
    public const ushort GetGameList = 0x4300;

    /// <summary>Opens the room-list reply stream.</summary>
    public const ushort GetGameListStart = 0x4301;

    /// <summary>Carries one room-list entry.</summary>
    public const ushort GetGameListPage = 0x4302;

    /// <summary>Closes the room-list reply stream.</summary>
    public const ushort GetGameListEnd = 0x4303;

    /// <summary>Returns the host settings saved by the client.</summary>
    public const ushort GetHostSettings = 0x4304;

    /// <summary>Carries the host settings saved by the client.</summary>
    public const ushort GetHostSettingsResult = 0x4305;

    /// <summary>Stores the host settings block submitted by the client.</summary>
    public const ushort CheckHostSettings = 0x4310;

    /// <summary>Carries the result of a host-settings write.</summary>
    public const ushort CheckHostSettingsResult = 0x4311;

    /// <summary>Returns the details of a room.</summary>
    public const ushort GetGameDetails = 0x4312;

    /// <summary>Carries the details of a room.</summary>
    public const ushort GetGameDetailsResult = 0x4313;

    /// <summary>Creates a room from the last settings the client pushed.</summary>
    public const ushort CreateGame = 0x4316;

    /// <summary>Carries the identifier of the created room.</summary>
    public const ushort CreateGameResult = 0x4317;

    /// <summary>Joins a room.</summary>
    public const ushort JoinGame = 0x4320;

    /// <summary>Carries the room's peer-to-peer endpoint.</summary>
    public const ushort JoinGameResult = 0x4321;

    /// <summary>Reports that joining a room failed.</summary>
    public const ushort JoinGameFailed = 0x4322;

    /// <summary>Carries the result of a failed join.</summary>
    public const ushort JoinGameFailedResult = 0x4323;

    /// <summary>Notifies the host that a peer connected.</summary>
    public const ushort HostPlayerConnected = 0x4340;

    /// <summary>Carries the host's peer-table index after a connect.</summary>
    public const ushort HostPlayerConnectedResult = 0x4341;

    /// <summary>Notifies the host that a peer disconnected.</summary>
    public const ushort HostPlayerDisconnected = 0x4342;

    /// <summary>Carries the host's peer-table index after a disconnect.</summary>
    public const ushort HostPlayerDisconnectedResult = 0x4343;

    /// <summary>Registers a peer's team with the host.</summary>
    public const ushort HostSetPlayerTeam = 0x4344;

    /// <summary>Carries the host's peer-table index after a team registration.</summary>
    public const ushort HostSetPlayerTeamResult = 0x4345;

    /// <summary>Completes a peer's connection registration.</summary>
    public const ushort HostPlayerConnectFinish = 0x4346;

    /// <summary>Carries the host's peer-table index after a finished connect.</summary>
    public const ushort HostPlayerConnectFinishResult = 0x4347;

    /// <summary>Hands host ownership to another player.</summary>
    public const ushort HostPass = 0x4348;

    /// <summary>Carries the result of a host hand-off.</summary>
    public const ushort HostPassResult = 0x4349;

    /// <summary>Stores the statistics a client reports for itself.</summary>
    public const ushort UpdateStats = 0x4350;

    /// <summary>Carries the result of a statistics report.</summary>
    public const ushort UpdateStatsResult = 0x4351;

    /// <summary>Leaves the current room.</summary>
    public const ushort QuitGame = 0x4380;

    /// <summary>Carries the result of leaving a room.</summary>
    public const ushort QuitGameResult = 0x4381;

    /// <summary>Stores the statistics the host reports for one player.</summary>
    public const ushort HostUpdateStats = 0x4390;

    /// <summary>Carries the result of a host statistics report.</summary>
    public const ushort HostUpdateStatsResult = 0x4391;

    /// <summary>Selects the rotation entry the room is staging.</summary>
    public const ushort SetGame = 0x4392;

    /// <summary>Carries the result of a rotation selection.</summary>
    public const ushort SetGameResult = 0x4393;

    /// <summary>Stores the round-trip times the host reports.</summary>
    public const ushort UpdatePings = 0x4398;

    /// <summary>Carries the result of a ping report.</summary>
    public const ushort UpdatePingsResult = 0x4399;

    /// <summary>Migrates the room to a new host when the host leaves.</summary>
    public const ushort PassRound = 0x43a0;

    /// <summary>Carries the result of a host migration.</summary>
    public const ushort PassRoundResult = 0x43a1;

    /// <summary>Stores the per-skill experience the host reports.</summary>
    public const ushort HostSkillExperience = 0x43a4;

    /// <summary>Carries the result of a skill-experience report.</summary>
    public const ushort HostSkillExperienceResult = 0x43a5;

    /// <summary>Stores a single client-side setting.</summary>
    public const ushort PutClientSetting = 0x43a6;

    /// <summary>Carries the result of a client-setting write.</summary>
    public const ushort PutClientSettingResult = 0x43a7;

    /// <summary>Edits the details of the caller's room in place.</summary>
    public const ushort HostInGameInfo = 0x43c0;

    /// <summary>Carries the result of an in-game details edit.</summary>
    public const ushort HostInGameInfoResult = 0x43c1;

    /// <summary>Records a host-rating vote.</summary>
    public const ushort RateHost = 0x43c4;

    /// <summary>Carries the result of a host-rating vote.</summary>
    public const ushort RateHostResult = 0x43c5;

    /// <summary>Starts a round.</summary>
    public const ushort StartRound = 0x43c8;

    /// <summary>Carries the result of starting a round.</summary>
    public const ushort StartRoundResult = 0x43c9;

    /// <summary>Legacy alias of <see cref="StartRound"/>.</summary>
    public const ushort StartRoundAlias = 0x43ca;

    /// <summary>Carries the result of the legacy start-round alias.</summary>
    public const ushort StartRoundAliasResult = 0x43cb;

    // Automatching (0x43ex) -------------------------------------------------

    /// <summary>Starts an automatch search.</summary>
    public const ushort StartAutomatch = 0x43e0;

    /// <summary>Carries the searcher's band and shortfall.</summary>
    public const ushort StartAutomatchResult = 0x43e1;

    /// <summary>Cancels an automatch search.</summary>
    public const ushort CancelAutomatch = 0x43e2;

    /// <summary>Carries the result of cancelling a search.</summary>
    public const ushort CancelAutomatchResult = 0x43e3;

    /// <summary>Pushes the automatch search panel to a searcher.</summary>
    public const ushort AutomatchSearchPanel = 0x43e4;

    /// <summary>Pushes a formed match to a group.</summary>
    public const ushort AutomatchMatchFound = 0x43f1;

    /// <summary>Pushes the game a formed match produced.</summary>
    public const ushort AutomatchMatchGame = 0x43f2;

    /// <summary>Tells a group that its host never created the game.</summary>
    public const ushort AutomatchMatchFailed = 0x43f3;

    // Chat (0x44xx) ---------------------------------------------------------

    /// <summary>Sends a chat message into the room.</summary>
    public const ushort SendChat = 0x4400;

    /// <summary>Carries a chat message.</summary>
    public const ushort SendChatResult = 0x4401;
}
