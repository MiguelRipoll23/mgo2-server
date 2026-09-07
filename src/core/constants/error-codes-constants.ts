// Error codes from Java Error.java.
// Passed as-is to encodeErrorPacket, which ORs them with 0xC0FFEE00 before sending.
export const ERROR_GENERAL = 0x80000000;
export const ERROR_INVALID_SESSION = 0x80000201;
export const ERROR_CHAR_NAMETAKEN = 0x80000301;
export const ERROR_CHAR_NAMEINVALID = 0x80000302;
export const ERROR_CHAR_NAMEPREFIX = 0x80000303;
export const ERROR_CHAR_NAMERESERVED = 0x80000304;
export const ERROR_CHAR_CANTDELETEYET = 0x80000401;

//
// Ready-to-send u32 result codes for payloads read as a raw {u32 result} word
// (no masking). "Official" codes are the client's own literal values and must
// go out verbatim — masking them as 0xC0FFEExx would match nothing in the
// client's table and fall through to the generic error sentence.
//
export const RESULT_NONE = 0;
// GameError.GENERAL(0x1) / INVALID_SESSION(0x2) as sent by the reference
// (masked, since they are not marked official there).
export const RESULT_GENERAL = 0xc0ffee01;
export const RESULT_INVALID_SESSION = 0xc0ffee02;
// Official: LOBBY_LOGIN_AGAIN(-240) — "You must login again to connect to the
// lobby". Sent on the GAME check-session (0x3003) wait slot; the account-lobby
// chain keeps RESULT_INVALID_SESSION, which falls through to its own generic.
export const RESULT_LOBBY_LOGIN_AGAIN = 0xffffff10;
// Official: CHARACTER_NAME_TAKEN(-260)
export const RESULT_NAME_TAKEN = 0xfffffefc;
export const RESULT_NAME_INVALID = 0xc0ffee10;
export const RESULT_NAME_PREFIX = 0xc0ffee11;
export const RESULT_NAME_RESERVED = 0xc0ffee12;
// Official: GAME_PASSWORD_INCORRECT(-540)
export const RESULT_GAME_PASSWORD_INCORRECT = 0xfffffde4;
// Official: GAME_FULL(-503)
export const RESULT_GAME_FULL = 0xfffffe09;
// Official: personal-stats "character deleted"(-266)
export const RESULT_CHARACTER_GONE = 0xfffffef6;
// Official: mail "Unable to locate designated mail"(-800)
export const RESULT_MAIL_NOT_FOUND = 0xfffffce0;
// Official automatch codes (GameError.java) — a masked 0xC0FFEExx matches
// nothing in the client's discriminated set and would print the wrong sentence.
// Official: AUTOMATCH_CANNOT_START(-950)
export const RESULT_AUTOMATCH_CANNOT_START = 0xfffffc4a;
// Official: AUTOMATCH_CANCEL_TOO_LATE(-953) — unused until a queue exists
export const RESULT_AUTOMATCH_CANCEL_TOO_LATE = 0xfffffc47;
// Official: AUTOMATCH_NOT_OPEN(-970)
export const RESULT_AUTOMATCH_NOT_OPEN = 0xfffffc36;

//
// Clan application refusals (0x4b43/0x4b31/0x4b33) and mail send failures
// (0x4801) — official client-table codes, sent verbatim.
//
// Official: -1207 "Unable to locate designated clan, or clan may be disbanded."
export const RESULT_CLAN_NOT_FOUND = 0xfffffb49;
// Official: -1201 "You are already a member of another clan."
export const RESULT_ALREADY_IN_CLAN = 0xfffffb4f;
// Official: -1219 (unrenderable — the client's cooldown sentence has no result
// code binding, so this lands on the generic "Unable to apply to join clan.")
export const RESULT_CLAN_APPLY_TOO_SOON = 0xfffffb3d;
// Official: -801 "Improper address entered. Unable to send mail."
export const RESULT_MAIL_RECIPIENT_UNKNOWN = 0xfffffcdf;
// Official: -802 "Receiver's mailbox is full. Unable to send mail."
export const RESULT_MAIL_RECIPIENT_FULL = 0xfffffcde;
// Official: -830 "The receiver has blocked incoming mail." (no block system yet)
export const RESULT_MAIL_RECIPIENT_BLOCKED = 0xfffffcc2;
