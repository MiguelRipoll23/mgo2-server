// Gate commands (0x2xxx)
export const GET_LOBBY_LIST = 0x2005;
export const GET_NEWS = 0x2008;

// Common commands (all servers)
export const DISCONNECT = 0x0003;
export const KEEP_ALIVE = 0x0005;

// Account commands (0x3xxx)
export const CHECK_SESSION = 0x3003;
export const GET_CHARACTER_LIST = 0x3048;
export const CREATE_CHARACTER = 0x3101;
export const SELECT_CHARACTER = 0x3103;
export const DELETE_CHARACTER = 0x3105;

// Game - Characters (0x41xx)
export const GET_CHARACTER_INFO = 0x4100;
export const GET_PERSONAL_STATS = 0x4102;
export const UPDATE_GAMEPLAY_OPTIONS = 0x4110;
export const UPDATE_UI_SETTINGS = 0x4112;
export const UPDATE_CHAT_MACROS = 0x4114;
export const GET_POST_GAME_INFO = 0x4128;
export const UPDATE_PERSONAL_INFO = 0x4130;
export const UPDATE_SKILL_SETS = 0x4141;
export const UPDATE_GEAR_SETS = 0x4143;
export const GET_LOBBY_DISCONNECT = 0x4150;
export const GET_CHARACTER_CARD = 0x4220;

// Game - Games (0x43xx-0x44xx)
export const GET_GAME_LIST = 0x4300;
export const GET_HOST_SETTINGS = 0x4304;
export const CHECK_HOST_SETTINGS = 0x4310;
export const GET_GAME_DETAILS = 0x4312;
export const CREATE_GAME = 0x4316;
export const JOIN_GAME = 0x4320;
export const JOIN_GAME_FAILED = 0x4322;
export const PLAYER_CONNECTED = 0x4340;
export const PLAYER_DISCONNECTED = 0x4342;
export const SET_PLAYER_TEAM = 0x4344;
export const KICK_PLAYER = 0x4346;
export const HOST_PASS = 0x4348;
export const UPDATE_STATS = 0x4350;
export const SET_GAME = 0x4392;
export const UPDATE_PINGS = 0x4398;
export const QUIT_GAME = 0x4380;
export const PASS_ROUND = 0x43a0;
export const TRAINING_CONNECT = 0x43d0;
export const START_ROUND = 0x43ca;
export const SEND_CHAT = 0x4400;

// Game - Friends/Search (0x45xx-0x46xx)
export const ADD_FRIENDS_BLOCKED = 0x4500;
export const REMOVE_FRIENDS_BLOCKED = 0x4510;
export const GET_FRIENDS_BLOCKED_LIST = 0x4580;
export const SEARCH_PLAYER = 0x4600;
export const GET_MATCH_HISTORY = 0x4680;

// Game - Hub (0x49xx)
export const GET_GAME_LOBBY_INFO = 0x4900;
export const GET_GAME_ENTRY_INFO = 0x4990;

// Game - Messages (0x48xx)
export const SEND_MESSAGE = 0x4800;
export const GET_MESSAGES = 0x4820;
export const GET_MESSAGE_CONTENTS = 0x4840;
export const ADD_SENT_MESSAGE = 0x4860;

// Game - Clans (0x4bxx)
export const CREATE_CLAN = 0x4b00;
export const DISBAND_CLAN = 0x4b04;
export const GET_CLAN_LIST = 0x4b10;
export const GET_CLAN_MEMBER_INFO = 0x4b20;
export const ACCEPT_CLAN_JOIN = 0x4b30;
export const DECLINE_CLAN_JOIN = 0x4b32;
export const BANISH_CLAN_MEMBER = 0x4b36;
export const LEAVE_CLAN = 0x4b40;
export const APPLY_TO_CLAN = 0x4b42;
export const UPDATE_CLAN_STATE = 0x4b46;
export const GET_CLAN_EMBLEM_LOBBY = 0x4b48;
export const GET_CLAN_EMBLEM = 0x4b4a;
export const GET_CLAN_EMBLEM_WIP = 0x4b4c;
export const SET_CLAN_EMBLEM = 0x4b50;
export const GET_CLAN_ROSTER = 0x4b52;
export const TRANSFER_CLAN_LEADERSHIP = 0x4b60;
export const SET_EMBLEM_EDITOR = 0x4b62;
export const UPDATE_CLAN_COMMENT = 0x4b64;
export const UPDATE_CLAN_NOTICE = 0x4b66;
export const GET_CLAN_STATS = 0x4b70;
export const GET_CLAN_INFO = 0x4b80;
export const SEARCH_CLAN = 0x4b90;

export { BLOWFISH_ENCRYPTED_INBOUND, BLOWFISH_ENCRYPTED_OUTBOUND } from "../tcp/types/tcp-constants-type.ts";
