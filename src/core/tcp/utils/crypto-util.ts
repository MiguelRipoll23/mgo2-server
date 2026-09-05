import {
  HMAC_MD5_KEY,
  XOR_KEY_BYTES,
  BLOWFISH_KEY_PACKET,
  BLOWFISH_KEY_AUTH,
  SESSION_FIELD_IV,
} from "../../constants/crypto-keys-constants.ts";

// ---------------------------------------------------------------------------
// XOR
// ---------------------------------------------------------------------------

export function xorBuffer(buffer: Uint8Array): void {
  for (let index = 0; index < buffer.length; index++) {
    buffer[index] ^= XOR_KEY_BYTES[index & 3];
  }
}

export function xorBufferWithKey(
  buffer: Uint8Array,
  xorKeyBytes: Uint8Array,
): void {
  for (let index = 0; index < buffer.length; index++) {
    buffer[index] ^= xorKeyBytes[index & (xorKeyBytes.length - 1)];
  }
}

// ---------------------------------------------------------------------------
// MD5 (RFC 1321) — needed because WebCrypto does not support HMAC-MD5
// ---------------------------------------------------------------------------

const MD5_SHIFTS = [
  7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 5, 9, 14, 20, 5,
  9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11,
  16, 23, 4, 11, 16, 23, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15,
  21,
];

const MD5_TABLE = new Uint32Array(64);
for (let tableIndex = 0; tableIndex < 64; tableIndex++) {
  MD5_TABLE[tableIndex] =
    Math.floor(Math.abs(Math.sin(tableIndex + 1)) * 2 ** 32) >>> 0;
}

function add32(firstValue: number, secondValue: number): number {
  return (firstValue + secondValue) >>> 0;
}

export function md5(message: Uint8Array): Uint8Array {
  const messageLengthInBits = message.length * 8;
  const paddingLength = ((55 - message.length) & 63) + 1;
  const padded = new Uint8Array(message.length + paddingLength + 8);
  padded.set(message);
  padded[message.length] = 0x80;

  const view = new DataView(padded.buffer);
  view.setUint32(padded.length - 8, messageLengthInBits >>> 0, true);
  view.setUint32(
    padded.length - 4,
    Math.floor(messageLengthInBits / 2 ** 32),
    true,
  );

  let hashA = 0x67452301;
  let hashB = 0xefcdab89;
  let hashC = 0x98badcfe;
  let hashD = 0x10325476;

  for (let blockOffset = 0; blockOffset < padded.length; blockOffset += 64) {
    const blockView = new DataView(padded.buffer, blockOffset, 64);
    const words = new Uint32Array(16);
    for (let wordIndex = 0; wordIndex < 16; wordIndex++) {
      words[wordIndex] = blockView.getUint32(wordIndex * 4, true);
    }

    let a = hashA,
      b = hashB,
      c = hashC,
      d = hashD;

    for (let step = 0; step < 64; step++) {
      let func: number;
      let wordIndex: number;

      if (step < 16) {
        func = (b & c) | (~b & d);
        wordIndex = step;
      } else if (step < 32) {
        func = (d & b) | (~d & c);
        wordIndex = (5 * step + 1) & 15;
      } else if (step < 48) {
        func = b ^ c ^ d;
        wordIndex = (3 * step + 5) & 15;
      } else {
        func = c ^ (b | ~d);
        wordIndex = (7 * step) & 15;
      }

      const temp = d;
      d = c;
      c = b;
      const shifted = add32(a, func);
      const withTable = add32(shifted, MD5_TABLE[step]);
      const withWord = add32(withTable, words[wordIndex]);
      const rotated =
        (withWord << MD5_SHIFTS[step]) | (withWord >>> (32 - MD5_SHIFTS[step]));
      b = add32(b, rotated);
      a = temp;
    }

    hashA = add32(hashA, a);
    hashB = add32(hashB, b);
    hashC = add32(hashC, c);
    hashD = add32(hashD, d);
  }

  const digest = new Uint8Array(16);
  const digestView = new DataView(digest.buffer);
  digestView.setUint32(0, hashA, true);
  digestView.setUint32(4, hashB, true);
  digestView.setUint32(8, hashC, true);
  digestView.setUint32(12, hashD, true);
  return digest;
}

// ---------------------------------------------------------------------------
// HMAC-MD5
// ---------------------------------------------------------------------------

const HMAC_BLOCK_SIZE = 64;

function buildHmacPads(key: Uint8Array): {
  innerPad: Uint8Array;
  outerPad: Uint8Array;
} {
  const normalizedKey = key.length > HMAC_BLOCK_SIZE ? md5(key) : key;
  const innerPad = new Uint8Array(HMAC_BLOCK_SIZE);
  const outerPad = new Uint8Array(HMAC_BLOCK_SIZE);
  innerPad.fill(0x36);
  outerPad.fill(0x5c);
  for (let index = 0; index < normalizedKey.length; index++) {
    innerPad[index] ^= normalizedKey[index];
    outerPad[index] ^= normalizedKey[index];
  }
  return { innerPad, outerPad };
}

const { innerPad: HMAC_INNER_PAD, outerPad: HMAC_OUTER_PAD } =
  buildHmacPads(HMAC_MD5_KEY);

export function computeHmacMd5(
  headerBytes: Uint8Array,
  payload: Uint8Array,
): Uint8Array {
  const innerMessage = new Uint8Array(
    HMAC_BLOCK_SIZE + headerBytes.length + payload.length,
  );
  innerMessage.set(HMAC_INNER_PAD);
  innerMessage.set(headerBytes, HMAC_BLOCK_SIZE);
  innerMessage.set(payload, HMAC_BLOCK_SIZE + headerBytes.length);

  const innerHash = md5(innerMessage);

  const outerMessage = new Uint8Array(HMAC_BLOCK_SIZE + 16);
  outerMessage.set(HMAC_OUTER_PAD);
  outerMessage.set(innerHash, HMAC_BLOCK_SIZE);

  return md5(outerMessage);
}

// ---------------------------------------------------------------------------
// Session field — the value the client presents in the 0x3003 check-session
// packet. Ported from the reference SessionField.java (ELF-derived, verified
// against live captures).
//
// The client never sends the login token back. When it parses the login reply
// it keeps the token as its sixteen ASCII characters, immediately derives a
// sixteen-byte value from them, and presents THAT on check-session. So the
// server never inverts anything: it applies the same transform to the token it
// issued and stores the result (hex) for a plain lookup at check-session time.
//
// The transform (context = 8-byte IV followed by a 56-byte key):
//   C[i] = decrypt(P[i]) XOR P[i-1],  with P[-1] = IV
//
// Note the block is DECRYPTED rather than encrypted, and the XOR is against
// the previous PLAINTEXT block, not the previous ciphertext. That is neither
// ECB nor CBC — which is why the transform resisted being guessed from
// captured pairs. Capture-proven vectors pin it:
//   1.36: token "f5a0880bc3a40336" -> 8dde80bae7eac2753b7c89139395cb21
//   1.0:  token "1888e089ebe181fd" -> a5a0dd9199494cf00e06ae9dc4655563
// (this server serves 1.36, whose kit schedule is BLOWFISH_KEY_AUTH above)
// ---------------------------------------------------------------------------

const SESSION_FIELD_TOKEN_LENGTH = 16;
const SESSION_FIELD_LENGTH = 16;

export function deriveSessionField(token: string): Uint8Array {
  const plain = new Uint8Array(SESSION_FIELD_TOKEN_LENGTH);
  for (let index = 0; index < SESSION_FIELD_TOKEN_LENGTH; index++) {
    plain[index] = token.charCodeAt(index) & 0xff;
  }

  const out = new Uint8Array(SESSION_FIELD_LENGTH);
  let previous = SESSION_FIELD_IV;

  for (let offset = 0; offset < plain.length; offset += 8) {
    const block = plain.slice(offset, offset + 8);
    const transformed = block.slice();
    blowfishDecrypt(transformed, AUTH_KEY_VIEW);
    for (let i = 0; i < 8; i++) {
      out[offset + i] = transformed[i] ^ previous[i];
    }
    // The chain carries the plaintext block forward, not the block just written.
    previous = block;
  }

  return out;
}

/** The derived field in the form stored on the session row (hex). */
export function storeSessionField(token: string): string {
  return Array.from(deriveSessionField(token))
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

/** Renders a field taken off the wire into the form storeSessionField produces. */
export function storedSessionFieldFromWire(field: Uint8Array): string {
  return Array.from(field.slice(0, SESSION_FIELD_LENGTH))
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

// ---------------------------------------------------------------------------
// Blowfish — ported from Crypto.java (big-endian, 8-round custom variant)
// ---------------------------------------------------------------------------

function blowfishRound(keyTable: DataView, firstValue: number): number {
  const indexA = (firstValue >>> 22) & 0x3fc;
  const indexB = (firstValue >>> 14) & 0x3fc;
  const indexC = (firstValue >>> 6) & 0x3fc;
  const indexD = ((firstValue << 2) | (firstValue >>> 30)) & 0x3fc;

  const valueA = keyTable.getInt32(indexA + 0x48, false);
  const valueB = keyTable.getInt32(indexB + 0x448, false);
  const valueC = keyTable.getInt32(indexC + 0x848, false);
  const valueD = keyTable.getInt32(indexD + 0xc48, false);

  return (((valueA + valueB) ^ valueC) + valueD) | 0;
}

export function blowfishDecrypt(data: Uint8Array, keyTable: DataView): void {
  const blockCount = (data.length / 8) | 0;
  const dataView = new DataView(data.buffer, data.byteOffset, data.byteLength);

  for (let blockIndex = 0; blockIndex < blockCount; blockIndex++) {
    const byteOffset = blockIndex * 8;
    let a = dataView.getInt32(byteOffset + 4, false);
    let b = dataView.getInt32(byteOffset, false);

    b ^= keyTable.getInt32(0x44, false);

    for (let round = 0; round < 8; round++) {
      a ^= keyTable.getInt32(0x40 - round * 8, false);
      a ^= blowfishRound(keyTable, b);

      b ^= keyTable.getInt32(0x3c - round * 8, false);
      b ^= blowfishRound(keyTable, a);
    }

    a ^= keyTable.getInt32(0x0, false);

    dataView.setInt32(byteOffset, a, false);
    dataView.setInt32(byteOffset + 4, b, false);
  }
}

export function blowfishEncrypt(data: Uint8Array, keyTable: DataView): void {
  const blockCount = (data.length / 8) | 0;
  const dataView = new DataView(data.buffer, data.byteOffset, data.byteLength);

  for (let blockIndex = 0; blockIndex < blockCount; blockIndex++) {
    const byteOffset = blockIndex * 8;
    let a = dataView.getInt32(byteOffset + 4, false);
    let b = dataView.getInt32(byteOffset, false);

    b ^= keyTable.getInt32(0x0, false);

    for (let round = 7; round >= 0; round--) {
      a ^= keyTable.getInt32(0x3c - round * 8, false);
      a ^= blowfishRound(keyTable, b);

      b ^= keyTable.getInt32(0x40 - round * 8, false);
      b ^= blowfishRound(keyTable, a);
    }

    a ^= keyTable.getInt32(0x44, false);

    dataView.setInt32(byteOffset, a, false);
    dataView.setInt32(byteOffset + 4, b, false);
  }
}

// ---------------------------------------------------------------------------
// Cached DataView wrappers for the two key tables
// ---------------------------------------------------------------------------

const PACKET_KEY_VIEW = new DataView(
  BLOWFISH_KEY_PACKET.buffer,
  BLOWFISH_KEY_PACKET.byteOffset,
);
const AUTH_KEY_VIEW = new DataView(
  BLOWFISH_KEY_AUTH.buffer,
  BLOWFISH_KEY_AUTH.byteOffset,
);

export function decryptPacketPayload(payload: Uint8Array): void {
  blowfishDecrypt(payload, PACKET_KEY_VIEW);
}

export function encryptPacketPayload(payload: Uint8Array): void {
  blowfishEncrypt(payload, PACKET_KEY_VIEW);
}

export function encryptAuthPayload(payload: Uint8Array): void {
  blowfishEncrypt(payload, AUTH_KEY_VIEW);
}
