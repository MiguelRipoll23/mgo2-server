// Error codes from Java Error.java.
// Passed as-is to encodeErrorPacket, which ORs them with 0xC0FFEE00 before sending.
export const ERROR_GENERAL = 0x80000000;
export const ERROR_INVALID_SESSION = 0x80000201;
export const ERROR_CHAR_NAMETAKEN = 0x80000301;
export const ERROR_CHAR_NAMEINVALID = 0x80000302;
export const ERROR_CHAR_NAMEPREFIX = 0x80000303;
export const ERROR_CHAR_NAMERESERVED = 0x80000304;
export const ERROR_CHAR_CANTDELETEYET = 0x80000401;
