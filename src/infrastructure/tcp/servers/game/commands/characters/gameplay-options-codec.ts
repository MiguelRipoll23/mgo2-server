/**
 * The gameplay-options codec: builds the 0x4120 payload and parses the 0x4110
 * write-back, which is the same structure truncated at 0x130 — the 48-byte
 * settings header plus the four 64-byte codec names, without the 32-byte
 * list-preferences trailer. That is read from the client's builder at
 * 0xD3BFC0 (one 48-byte blob write, then a four-pass loop of 64-byte writes,
 * then the seal), so the pair below is one structure written two ways and
 * must be changed together.
 *
 * Almost every setting shares a byte with another, and several are stored one
 * higher than they go on the wire — the speed sliders are 1-based to the
 * player and 0-based to the client, while the music volume runs the other
 * way. An off-by-one here moves a slider by one notch on every round trip,
 * which nothing would ever fail on.
 */

/** 0x4120. Header + codec names + the 32-byte list-preferences trailer. */
export const GAMEPLAY_OPTIONS_PAYLOAD_SIZE = 0x150;

/** 0x4110. The write-back: the same payload truncated at 0x130. */
export const GAMEPLAY_OPTIONS_WRITEBACK_SIZE = 0x130;

const CODEC_NAME_LENGTH = 64;

/**
 * The 32 bytes closing 0x4120 — the player's Filter Host List, Sort Host List
 * and Player Search preferences. All zeros: no filtering, sort by name
 * ascending, partial and case-insensitive search.
 *
 * This used to be an inherited constant and it was hiding games. The old
 * value (01 00 10 00 …) said, once the disc labels were extracted: Filter =
 * Enabled, Password Lock = "Display Only Disabled" — every password-locked
 * game hidden from every player's browser — and Match Case = Case Sensitive.
 * The client's own default for this whole region is zero (its validator
 * memsets 33 bytes at 0x9472E8), so zeros are the game's answer, not merely
 * ours. The client does not send these back in 0x4110 — its write-back is
 * 0x4120 minus exactly these 32 bytes.
 */
const LIST_PREFERENCES_TRAILER = new Uint8Array(32);

export const DEFAULT_GAMEPLAY_OPTIONS: Record<string, unknown> = {
  onlineStatusMode: 0,
  emailFriendsOnly: false,
  receiveNotices: true,
  receiveInvites: true,
  normalViewVerticalInvert: false,
  normalViewHorizontalInvert: false,
  normalViewSpeed: 5,
  shoulderViewVerticalInvert: false,
  shoulderViewHorizontalInvert: false,
  shoulderViewSpeed: 5,
  firstViewVerticalInvert: false,
  firstViewHorizontalInvert: false,
  firstViewSpeed: 5,
  // "Direction After View Change" — bit 2 of the firstView byte, set means
  // "Camera direction". The client's own default is set (0x947388).
  firstViewPlayerDirection: true,
  viewChangeSpeed: 5,
  firstViewMemory: false,
  radarLockNorth: false,
  radarFloorHide: false,
  hudDisplaySize: 0,
  hudHideNameTags: false,
  lockOnEnabled: false,
  weaponSwitchMode: 2,
  weaponSwitchA: 0,
  weaponSwitchB: 1,
  weaponSwitchC: 2,
  // Recall Mode's "Now"/"Before" categories and Toggle Mode's single weapon.
  // The validator requires "Now" and "Before" to differ and resets BOTH when
  // they collide (0x94792C-0x9479AC), so losing one silently reset the other.
  weaponSwitchNow: 0,
  weaponSwitchBefore: 1,
  weaponSwitchToggle: 2,
  itemSwitchMode: 2,
  // Audio output devices: 0 Standard (TV), 1 USB/Bluetooth. Both were
  // previously hardcoded to 1, forcing every player onto USB output.
  voiceChatOutputDevice: 0,
  codecOutputDevice: 0,
  codec1Name: "",
  codec1a: 1,
  codec1b: 3,
  codec1c: 4,
  codec1d: 2,
  codec2Name: "",
  codec2a: 10,
  codec2b: 12,
  codec2c: 13,
  codec2d: 11,
  codec3Name: "",
  codec3a: 14,
  codec3b: 16,
  codec3c: 17,
  codec3d: 15,
  codec4Name: "",
  codec4a: 5,
  codec4b: 7,
  codec4c: 8,
  codec4d: 6,
  voiceChatRecognitionLevel: 5,
  voiceChatVolume: 5,
  headsetVolume: 5,
  bgmVolume: 10,
};

function num(data: Record<string, unknown>, key: string, fallback: number): number {
  const value = data[key];
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function bool(data: Record<string, unknown>, key: string, fallback: boolean): boolean {
  const value = data[key];
  return typeof value === "boolean" ? value : fallback;
}

function str(data: Record<string, unknown>, key: string, fallback: string): string {
  const value = data[key];
  return typeof value === "string" ? value : fallback;
}

/** Speeds are shown 1-based but sent 0-based. */
function speed(value: number): number {
  return (value - 1) & 0b1111;
}

/** The inverse: the wire's 0-based nibble back to the stored 1-based value. */
function speedFromWire(wire: number): number {
  return (wire & 0b1111) + 1;
}

/**
 * Builds the 0x4120 payload (0x150 bytes) from the stored settings.
 */
export function buildGameplayOptionsPayload(
  stored: Record<string, unknown>,
): Uint8Array {
  const data = { ...DEFAULT_GAMEPLAY_OPTIONS, ...stored };

  const privacyA =
    1 |
    ((num(data, "onlineStatusMode", 0) & 0b11) << 4) |
    (bool(data, "emailFriendsOnly", false) ? 0b01000000 : 0);

  const normalView =
    (bool(data, "normalViewVerticalInvert", false) ? 0b1 : 0) |
    (bool(data, "normalViewHorizontalInvert", false) ? 0b10 : 0) |
    (speed(num(data, "normalViewSpeed", 5)) << 4);

  const shoulderView =
    (bool(data, "shoulderViewVerticalInvert", false) ? 0b1 : 0) |
    (bool(data, "shoulderViewHorizontalInvert", false) ? 0b10 : 0) |
    (speed(num(data, "shoulderViewSpeed", 5)) << 4);

  // Bit 2 is "Direction After View Change"; set means "Camera direction".
  const firstView =
    (bool(data, "firstViewVerticalInvert", false) ? 0b1 : 0) |
    (bool(data, "firstViewHorizontalInvert", false) ? 0b10 : 0) |
    (bool(data, "firstViewPlayerDirection", true) ? 0b100 : 0) |
    (speed(num(data, "firstViewSpeed", 5)) << 4);

  const switchModes =
    (num(data, "weaponSwitchMode", 2) & 0b1111) |
    ((num(data, "itemSwitchMode", 2) & 0b1111) << 4);

  // Three fields, one byte: voice-chat output device (bits 0-1), codec output
  // device (bits 2-3), recognition level (bits 4-7).
  const voiceChatA =
    (num(data, "voiceChatOutputDevice", 0) & 0b11) |
    ((num(data, "codecOutputDevice", 0) & 0b11) << 2) |
    ((num(data, "voiceChatRecognitionLevel", 5) & 0b1111) << 4);

  const voiceChatB =
    (num(data, "voiceChatVolume", 5) & 0b1111) |
    ((num(data, "headsetVolume", 5) & 0b1111) << 4);

  const weaponSwitchAB =
    (num(data, "weaponSwitchA", 0) & 0b1111) |
    ((num(data, "weaponSwitchB", 1) & 0b1111) << 4);

  // Cycle-mode slot C, plus "Now" — the first of Recall Mode's two
  // categories — in the high nibble.
  const weaponSwitchC =
    (num(data, "weaponSwitchC", 2) & 0b1111) |
    ((num(data, "weaponSwitchNow", 0) & 0b1111) << 4);

  // "Before" — Recall Mode's second category — plus, in the high nibble, the
  // single weapon Toggle Mode equips and unequips.
  const weaponSwitchRecall =
    (num(data, "weaponSwitchBefore", 1) & 0b1111) |
    ((num(data, "weaponSwitchToggle", 2) & 0b1111) << 4);

  // First Person View Memory, low nibble: 0 Disabled, 1 Enabled. This wrote
  // bit 1 (0b10) once and the setting never worked — the client reads the
  // whole low nibble and rewrites anything above 1 to 1.
  const firstViewMemory = bool(data, "firstViewMemory", false) ? 1 : 0;

  const privacyB =
    (bool(data, "receiveNotices", true) ? 0b1 : 0) |
    (bool(data, "receiveInvites", true) ? 0b10000 : 0);

  // Lock-on shares a byte with the music volume, which travels one HIGHER
  // than it is stored — the inverse subtracts one. Getting this backwards
  // drifts the volume by one on every single round trip.
  const lockOnAndBgm =
    (bool(data, "lockOnEnabled", false) ? 0b1 : 0) |
    (((num(data, "bgmVolume", 10) + 1) & 0b1111) << 4);

  const radar =
    (bool(data, "radarLockNorth", false) ? 0b1 : 0) |
    (bool(data, "radarFloorHide", false) ? 0b10000 : 0);

  const hudDisplay =
    (num(data, "hudDisplaySize", 0) & 0b11) |
    (bool(data, "hudHideNameTags", false) ? 0b10000 : 0);

  const bytes = new Uint8Array(GAMEPLAY_OPTIONS_PAYLOAD_SIZE);
  const view = new DataView(bytes.buffer);
  let at = 0;
  const put = (value: number) => {
    bytes[at++] = value & 0xff;
  };

  put(privacyA);
  put(normalView);
  put(shoulderView);
  put(firstView);
  put(speed(num(data, "viewChangeSpeed", 5)));
  at += 6; // +5..+10 unmapped; sent as zero
  put(switchModes);
  at += 1; // +12 unmapped
  put(voiceChatA);
  put(voiceChatB);
  put(weaponSwitchAB);
  put(weaponSwitchC);
  put(weaponSwitchRecall);
  put(firstViewMemory);
  put(privacyB);
  put(lockOnAndBgm);
  put(radar);
  put(hudDisplay);
  at += 9; // +23..+31 unmapped

  // Codec shortcut entries: four bytes each, at +32.
  put(num(data, "codec1a", 1));
  put(num(data, "codec1b", 3));
  put(num(data, "codec1c", 4));
  put(num(data, "codec1d", 2));
  put(num(data, "codec2a", 10));
  put(num(data, "codec2b", 12));
  put(num(data, "codec2c", 13));
  put(num(data, "codec2d", 11));
  put(num(data, "codec3a", 14));
  put(num(data, "codec3b", 16));
  put(num(data, "codec3c", 17));
  put(num(data, "codec3d", 15));
  put(num(data, "codec4a", 5));
  put(num(data, "codec4b", 7));
  put(num(data, "codec4c", 8));
  put(num(data, "codec4d", 6));
  void view;

  writeFixedString(bytes, at, str(data, "codec1Name", ""), CODEC_NAME_LENGTH);
  at += CODEC_NAME_LENGTH;
  writeFixedString(bytes, at, str(data, "codec2Name", ""), CODEC_NAME_LENGTH);
  at += CODEC_NAME_LENGTH;
  writeFixedString(bytes, at, str(data, "codec3Name", ""), CODEC_NAME_LENGTH);
  at += CODEC_NAME_LENGTH;
  writeFixedString(bytes, at, str(data, "codec4Name", ""), CODEC_NAME_LENGTH);
  at += CODEC_NAME_LENGTH;

  bytes.set(LIST_PREFERENCES_TRAILER, at);

  return bytes;
}

/**
 * Parses the 0x4110 write-back into named settings.
 *
 * Returns null and should change nothing when the payload is short — a
 * truncated write-back is a protocol violation, and half-applying it would
 * persist a mixture of the player's settings and whatever the buffer ran out
 * on. Unmapped bytes (+5..+10, +12, +23..+31) are read into nowhere rather
 * than given invented meanings.
 */
export function parseGameplayOptionsPayload(
  payload: Uint8Array,
): Record<string, unknown> | null {
  if (payload.length < GAMEPLAY_OPTIONS_WRITEBACK_SIZE) {
    return null;
  }
  const at = (offset: number) => payload[offset];

  const settings: Record<string, unknown> = {};

  const privacyA = at(0);
  settings.onlineStatusMode = (privacyA >> 4) & 0b11;
  settings.emailFriendsOnly = (privacyA & 0b01000000) !== 0;

  const normalView = at(1);
  settings.normalViewVerticalInvert = (normalView & 0b1) !== 0;
  settings.normalViewHorizontalInvert = (normalView & 0b10) !== 0;
  settings.normalViewSpeed = speedFromWire(normalView >> 4);

  const shoulderView = at(2);
  settings.shoulderViewVerticalInvert = (shoulderView & 0b1) !== 0;
  settings.shoulderViewHorizontalInvert = (shoulderView & 0b10) !== 0;
  settings.shoulderViewSpeed = speedFromWire(shoulderView >> 4);

  const firstView = at(3);
  settings.firstViewVerticalInvert = (firstView & 0b1) !== 0;
  settings.firstViewHorizontalInvert = (firstView & 0b10) !== 0;
  // Bit 2: "Direction After View Change" — set means "Camera direction".
  settings.firstViewPlayerDirection = (firstView & 0b100) !== 0;
  settings.firstViewSpeed = speedFromWire(firstView >> 4);

  settings.viewChangeSpeed = speedFromWire(at(4));

  const switchModes = at(11);
  settings.weaponSwitchMode = switchModes & 0b1111;
  settings.itemSwitchMode = (switchModes >> 4) & 0b1111;

  // Three fields share this byte; reading only the top one discarded both
  // audio-output-device choices on every write-back.
  const voiceChatA = at(13);
  settings.voiceChatOutputDevice = voiceChatA & 0b11;
  settings.codecOutputDevice = (voiceChatA >> 2) & 0b11;
  settings.voiceChatRecognitionLevel = (voiceChatA >> 4) & 0b1111;

  const voiceChatB = at(14);
  settings.voiceChatVolume = voiceChatB & 0b1111;
  settings.headsetVolume = (voiceChatB >> 4) & 0b1111;

  const weaponSwitchAB = at(15);
  settings.weaponSwitchA = weaponSwitchAB & 0b1111;
  settings.weaponSwitchB = (weaponSwitchAB >> 4) & 0b1111;

  const cycleC = at(16);
  settings.weaponSwitchC = cycleC & 0b1111;
  settings.weaponSwitchNow = (cycleC >> 4) & 0b1111;

  const recall = at(17);
  settings.weaponSwitchBefore = recall & 0b1111;
  settings.weaponSwitchToggle = (recall >> 4) & 0b1111;

  // Low nibble compared against nonzero: 0 is Disabled, 1 is Enabled (row
  // descriptor at 0x105E1D4). This was read from the absent bit 1 once,
  // which discarded the player's choice every session.
  settings.firstViewMemory = (at(18) & 0b1111) !== 0;

  const privacyB = at(19);
  settings.receiveNotices = (privacyB & 0b1) !== 0;
  settings.receiveInvites = (privacyB & 0b10000) !== 0;

  // Lock-on shares a byte with the music volume, which is sent one HIGHER
  // than stored — so the inverse subtracts one.
  const lockOnAndBgm = at(20);
  settings.lockOnEnabled = (lockOnAndBgm & 0b1) !== 0;
  settings.bgmVolume = Math.max(0, ((lockOnAndBgm >> 4) & 0b1111) - 1);

  const radar = at(21);
  settings.radarLockNorth = (radar & 0b1) !== 0;
  settings.radarFloorHide = (radar & 0b10000) !== 0;

  const hud = at(22);
  settings.hudDisplaySize = hud & 0b11;
  settings.hudHideNameTags = (hud & 0b10000) !== 0;

  // Codec shortcut entries are four bytes each, at +32.
  settings.codec1a = at(32);
  settings.codec1b = at(33);
  settings.codec1c = at(34);
  settings.codec1d = at(35);
  settings.codec2a = at(36);
  settings.codec2b = at(37);
  settings.codec2c = at(38);
  settings.codec2d = at(39);
  settings.codec3a = at(40);
  settings.codec3b = at(41);
  settings.codec3c = at(42);
  settings.codec3d = at(43);
  settings.codec4a = at(44);
  settings.codec4b = at(45);
  settings.codec4c = at(46);
  settings.codec4d = at(47);

  settings.codec1Name = readNulString(payload, 48);
  settings.codec2Name = readNulString(payload, 48 + CODEC_NAME_LENGTH);
  settings.codec3Name = readNulString(payload, 48 + CODEC_NAME_LENGTH * 2);
  settings.codec4Name = readNulString(payload, 48 + CODEC_NAME_LENGTH * 3);

  return settings;
}

/** Copies a string into a fixed-width, NUL-padded ISO-8859-1 field. */
function writeFixedString(
  bytes: Uint8Array,
  offset: number,
  value: string,
  length: number,
): void {
  for (let i = 0; i < length; i++) {
    bytes[offset + i] = i < value.length ? value.charCodeAt(i) & 0xff : 0;
  }
}

/** A NUL-terminated ISO-8859-1 string out of a fixed 64-byte field. */
function readNulString(payload: Uint8Array, offset: number): string {
  const chars: string[] = [];
  for (let i = 0; i < CODEC_NAME_LENGTH; i++) {
    const byte = payload[offset + i];
    if (byte === 0) break;
    chars.push(String.fromCharCode(byte));
  }
  return chars.join("");
}
