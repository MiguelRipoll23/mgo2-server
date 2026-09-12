namespace Mgo2Server.Shared.Constants;

/// <summary>Command identifiers of the gameplay lobby, grouped by screen.</summary>
public static partial class CommandConstants
{
    // Common ----------------------------------------------------------------

    /// <summary>Echoes its payload back; part of the pre-lobby handshake.</summary>
    public const ushort Echo = 0x0001;

    /// <summary>In-lobby session check.</summary>
    public const ushort GameCheckSession = 0x3003;

    /// <summary>Carries the result of the in-lobby session check.</summary>
    public const ushort GameCheckSessionResult = 0x3004;

    // Characters (0x41xx) ---------------------------------------------------

    /// <summary>Returns the full character record.</summary>
    public const ushort GetCharacterInfo = 0x4100;

    /// <summary>Carries the full character record.</summary>
    public const ushort GetCharacterInfoResult = 0x4101;

    /// <summary>Returns the per-mode statistics of a character.</summary>
    public const ushort GetPersonalStats = 0x4102;

    /// <summary>Carries the personal-statistics header.</summary>
    public const ushort GetPersonalStatsHeader = 0x4103;

    /// <summary>Carries one per-mode statistics page.</summary>
    public const ushort GetPersonalStatsPage = 0x4105;

    /// <summary>Carries the personal-statistics tail and releases the client.</summary>
    public const ushort GetPersonalStatsTail = 0x4107;

    /// <summary>Stores the client's gameplay options.</summary>
    public const ushort UpdateGameplayOptions = 0x4110;

    /// <summary>Carries the result of a gameplay-options write.</summary>
    public const ushort UpdateGameplayOptionsResult = 0x4111;

    /// <summary>Stores the client's user-interface settings.</summary>
    public const ushort UpdateUiSettings = 0x4112;

    /// <summary>Carries the result of a user-interface settings write.</summary>
    public const ushort UpdateUiSettingsResult = 0x4113;

    /// <summary>Stores the client's chat macros.</summary>
    public const ushort UpdateChatMacros = 0x4114;

    /// <summary>Carries the result of a chat-macro write.</summary>
    public const ushort UpdateChatMacrosResult = 0x4115;

    /// <summary>Returns the chat macros.</summary>
    public const ushort GetChatMacros = 0x411a;

    /// <summary>Carries one page of chat macros.</summary>
    public const ushort GetChatMacrosResult = 0x4121;

    /// <summary>Returns the gameplay options and user-interface settings.</summary>
    public const ushort GetGameplayOptions = 0x411b;

    /// <summary>Carries the gameplay options.</summary>
    public const ushort GetGameplayOptionsResult = 0x4120;

    /// <summary>Returns the personal information screen data.</summary>
    public const ushort GetPersonalInfo = 0x4122;

    /// <summary>Returns the gear catalogue.</summary>
    public const ushort GetGear = 0x4124;

    /// <summary>Returns the skill catalogue.</summary>
    public const ushort GetSkills = 0x4125;

    /// <summary>Returns the post-game statistics screen data.</summary>
    public const ushort GetPostGameInfo = 0x4128;

    /// <summary>Carries the post-game statistics screen data.</summary>
    public const ushort GetPostGameInfoResult = 0x4129;

    /// <summary>Stores the client's personal information fields.</summary>
    public const ushort UpdatePersonalInfo = 0x4130;

    /// <summary>Carries the result of a personal-information write.</summary>
    public const ushort UpdatePersonalInfoResult = 0x4131;

    /// <summary>Commits the outfit the client has been editing.</summary>
    public const ushort CommitOutfit = 0x4132;

    /// <summary>Carries the gear catalogue after an outfit commit.</summary>
    public const ushort CommitOutfitResult = 0x4133;

    /// <summary>Returns the saved skill sets.</summary>
    public const ushort GetSkillSets = 0x4140;

    /// <summary>Stores the client's skill sets.</summary>
    public const ushort UpdateSkillSets = 0x4141;

    /// <summary>Returns the saved gear sets.</summary>
    public const ushort GetGearSets = 0x4142;

    /// <summary>Stores the client's gear sets.</summary>
    public const ushort UpdateGearSets = 0x4143;

    /// <summary>Leaves the gameplay lobby.</summary>
    public const ushort GetLobbyDisconnect = 0x4150;

    /// <summary>Carries the result of leaving the lobby.</summary>
    public const ushort GetLobbyDisconnectResult = 0x4151;

    /// <summary>Returns another character's card.</summary>
    public const ushort GetCharacterCard = 0x4220;

    /// <summary>Carries another character's card.</summary>
    public const ushort GetCharacterCardResult = 0x4221;

    /// <summary>Registers the client's peer-to-peer endpoint.</summary>
    public const ushort GetPlayerData = 0x4700;

    /// <summary>Carries the result of registering a peer-to-peer endpoint.</summary>
    public const ushort GetPlayerDataResult = 0x4701;

    // Friends and search (0x45xx-0x46xx) ------------------------------------

    /// <summary>Adds a character to the friends or blocked list.</summary>
    public const ushort AddFriendsBlocked = 0x4500;

    /// <summary>Carries the added friends or blocked entry.</summary>
    public const ushort AddFriendsBlockedResult = 0x4502;

    /// <summary>Removes a character from the friends or blocked list.</summary>
    public const ushort RemoveFriendsBlocked = 0x4510;

    /// <summary>Carries the removed friends or blocked entry.</summary>
    public const ushort RemoveFriendsBlockedResult = 0x4512;

    /// <summary>Returns the friends and blocked list.</summary>
    public const ushort GetFriendsBlockedList = 0x4580;

    /// <summary>Opens the friends and blocked list reply stream.</summary>
    public const ushort GetFriendsBlockedListStart = 0x4581;

    /// <summary>Carries one friends and blocked list page.</summary>
    public const ushort GetFriendsBlockedListPage = 0x4582;

    /// <summary>Closes the friends and blocked list reply stream.</summary>
    public const ushort GetFriendsBlockedListEnd = 0x4583;

    /// <summary>Searches for a player by name.</summary>
    public const ushort SearchPlayer = 0x4600;

    /// <summary>Opens the player-search reply stream.</summary>
    public const ushort SearchPlayerStart = 0x4601;

    /// <summary>Carries one player-search result.</summary>
    public const ushort SearchPlayerPage = 0x4602;

    /// <summary>Closes the player-search reply stream.</summary>
    public const ushort SearchPlayerEnd = 0x4603;

    /// <summary>Returns the met-players history.</summary>
    public const ushort GetMatchHistory = 0x4680;

    /// <summary>Opens the match-history reply stream.</summary>
    public const ushort GetMatchHistoryStart = 0x4681;

    /// <summary>Carries one match-history page.</summary>
    public const ushort GetMatchHistoryPage = 0x4682;

    /// <summary>Closes the match-history reply stream.</summary>
    public const ushort GetMatchHistoryEnd = 0x4683;

    /// <summary>Returns the details of one recorded match.</summary>
    public const ushort GetMatchDetails = 0x4684;

    /// <summary>Opens the match-details reply stream.</summary>
    public const ushort GetMatchDetailsStart = 0x4685;

    /// <summary>Carries one match-details page.</summary>
    public const ushort GetMatchDetailsPage = 0x4686;

    /// <summary>Closes the match-details reply stream.</summary>
    public const ushort GetMatchDetailsEnd = 0x4687;

    // Mail (0x48xx) ---------------------------------------------------------

    /// <summary>Sends a mail message.</summary>
    public const ushort SendMessage = 0x4800;

    /// <summary>Carries the result of sending a mail message.</summary>
    public const ushort SendMessageResult = 0x4801;

    /// <summary>Lists the mail message headers.</summary>
    public const ushort GetMessages = 0x4820;

    /// <summary>Opens the message-list reply stream.</summary>
    public const ushort GetMessagesStart = 0x4821;

    /// <summary>Carries one message-list entry.</summary>
    public const ushort GetMessagesPage = 0x4822;

    /// <summary>Closes the message-list reply stream.</summary>
    public const ushort GetMessagesEnd = 0x4823;

    /// <summary>Returns the contents of one message.</summary>
    public const ushort GetMessageContents = 0x4840;

    /// <summary>Carries the contents of one message.</summary>
    public const ushort GetMessageContentsResult = 0x4841;

    /// <summary>Deletes one message.</summary>
    public const ushort DeleteMessage = 0x4880;

    /// <summary>Carries the result of deleting a message.</summary>
    public const ushort DeleteMessageResult = 0x4881;

    // Lobby select (0x49xx) -------------------------------------------------

    /// <summary>Returns the lobby information shown on the lobby-select screen.</summary>
    public const ushort GetGameLobbyInfo = 0x4900;

    /// <summary>Opens the lobby-information reply stream.</summary>
    public const ushort GetGameLobbyInfoStart = 0x4901;

    /// <summary>Carries one lobby-information page.</summary>
    public const ushort GetGameLobbyInfoPage = 0x4902;

    /// <summary>Closes the lobby-information reply stream.</summary>
    public const ushort GetGameLobbyInfoEnd = 0x4903;

    /// <summary>Returns the game-entry information shown on the lobby-select screen.</summary>
    public const ushort GetGameEntryInfo = 0x4990;

    /// <summary>Carries the game-entry information.</summary>
    public const ushort GetGameEntryInfoResult = 0x4991;

    /// <summary>Connects a training-session client.</summary>
    public const ushort TrainingConnect = 0x43d0;

    /// <summary>Carries the training-session reply.</summary>
    public const ushort TrainingConnectResult = 0x43d1;

    /// <summary>Round-end weapon tallies for one player.</summary>
    public const ushort HostWeaponTallies = 0x43a2;

    /// <summary>Carries the result of the weapon-tally upload.</summary>
    public const ushort HostWeaponTalliesResult = 0x43a3;

    /// <summary>Echoes a chat-family request.</summary>
    public const ushort ChatEcho = 0x4440;

    /// <summary>Carries the result of the chat-family request.</summary>
    public const ushort ChatEchoResult = 0x4441;
}
