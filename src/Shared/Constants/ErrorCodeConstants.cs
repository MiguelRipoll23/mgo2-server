namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Error codes of the protocol. They are passed
/// to <c>PacketCodecService.EncodeErrorPacket</c>, which ORs non-zero values
/// with <see cref="ErrorMask"/> before sending.
/// </summary>
public static class ErrorCodeConstants
{
    /// <summary>Mask ORed onto non-zero error codes by <c>EncodeErrorPacket</c>.</summary>
    public const uint ErrorMask = 0xc0ffee00;

    /// <summary>Generic failure (0x80000000).</summary>
    public const uint ErrorGeneral = 0x80000000;

    /// <summary>The presented session is invalid (0x80000201).</summary>
    public const uint ErrorInvalidSession = 0x80000201;

    /// <summary>The requested character name is already taken (0x80000301).</summary>
    public const uint ErrorCharacterNameTaken = 0x80000301;

    /// <summary>The requested character name contains invalid characters (0x80000302).</summary>
    public const uint ErrorCharacterNameInvalid = 0x80000302;

    /// <summary>The requested character name starts with a reserved prefix (0x80000303).</summary>
    public const uint ErrorCharacterNamePrefix = 0x80000303;

    /// <summary>The requested character name is reserved (0x80000304).</summary>
    public const uint ErrorCharacterNameReserved = 0x80000304;

    /// <summary>The character cannot be deleted yet (0x80000401).</summary>
    public const uint ErrorCharacterCannotDeleteYet = 0x80000401;

    // Ready-to-send u32 result codes for payloads read as a raw result word
    // (no masking). The "official" codes are the client's own literal values and
    // must go out verbatim; masking them as 0xC0FFEExx would match nothing in
    // the client's table and fall through to the generic error sentence.

    /// <summary>No result (0).</summary>
    public const uint ResultNone = 0;

    /// <summary>Generic failure (masked).</summary>
    public const uint ResultGeneral = 0xc0ffee01;

    /// <summary>Invalid session (masked).</summary>
    public const uint ResultInvalidSession = 0xc0ffee02;

    /// <summary>
    /// Official LOBBY_LOGIN_AGAIN(-240): "You must login again to connect to
    /// the lobby". Sent on the game check-session wait slot; the account-lobby
    /// chain keeps <see cref="ResultInvalidSession"/> instead.
    /// </summary>
    public const uint ResultLobbyLoginAgain = 0xffffff10;

    /// <summary>Official CHARACTER_NAME_TAKEN(-260).</summary>
    public const uint ResultNameTaken = 0xfffffefc;

    /// <summary>Invalid character name (masked, not an official code).</summary>
    public const uint ResultNameInvalid = 0xc0ffee10;

    /// <summary>Reserved character name prefix (masked, not an official code).</summary>
    public const uint ResultNamePrefix = 0xc0ffee11;

    /// <summary>Reserved character name (masked, not an official code).</summary>
    public const uint ResultNameReserved = 0xc0ffee12;

    /// <summary>Official GAME_PASSWORD_INCORRECT(-540).</summary>
    public const uint ResultGamePasswordIncorrect = 0xfffffde4;

    /// <summary>Official GAME_FULL(-503).</summary>
    public const uint ResultGameFull = 0xfffffe09;

    /// <summary>Official personal-stats "character deleted"(-266).</summary>
    public const uint ResultCharacterGone = 0xfffffef6;

    /// <summary>Official mail "Unable to locate designated mail"(-800).</summary>
    public const uint ResultMailNotFound = 0xfffffce0;

    /// <summary>Official AUTOMATCH_CANNOT_START(-950).</summary>
    public const uint ResultAutomatchCannotStart = 0xfffffc4a;

    /// <summary>Official AUTOMATCH_CANCEL_TOO_LATE(-953); unused until a queue exists.</summary>
    public const uint ResultAutomatchCancelTooLate = 0xfffffc47;

    /// <summary>Official AUTOMATCH_NOT_OPEN(-970).</summary>
    public const uint ResultAutomatchNotOpen = 0xfffffc36;

    // Clan application refusals (0x4b43/0x4b31/0x4b33) and mail send failures
    // (0x4801) — official client-table codes, sent verbatim.

    /// <summary>Official -1207: "Unable to locate designated clan, or clan may be disbanded."</summary>
    public const uint ResultClanNotFound = 0xfffffb49;

    /// <summary>Official -1201: "You are already a member of another clan."</summary>
    public const uint ResultAlreadyInClan = 0xfffffb4f;

    /// <summary>
    /// Official -1219. The client's cooldown sentence has no result-code
    /// binding, so it lands on the generic "Unable to apply to join clan."
    /// </summary>
    public const uint ResultClanApplyTooSoon = 0xfffffb3d;

    /// <summary>Official -801: "Improper address entered. Unable to send mail."</summary>
    public const uint ResultMailRecipientUnknown = 0xfffffcdf;

    /// <summary>Official -802: "Receiver's mailbox is full. Unable to send mail."</summary>
    public const uint ResultMailRecipientFull = 0xfffffcde;

    /// <summary>Official -830: "The receiver has blocked incoming mail."</summary>
    public const uint ResultMailRecipientBlocked = 0xfffffcc2;

    /// <summary>Official -1229: "Unable to create a clan. The name is already used."</summary>
    public const uint ErrorClanNameTaken = 0x80000501;

    /// <summary>Clan-specific failure code the emblem replies carry (0x40).</summary>
    public const uint ErrorClanDoesNotExist = 0x40;
}
