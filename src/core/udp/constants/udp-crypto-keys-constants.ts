// Multiplier of the LCG driving the header scramble positions and the XOR
// chain seed (decoder FUN_002666c8).
export const UDP_LCG_MULTIPLIER = 0x5d588b65;

// Chain key of frames sent before the session key is established (state < 8).
export const UDP_PRE_HANDSHAKE_KEY = 0x87103c2f;

// Magic every handshake must carry (validated by the receiver).
export const UDP_MODULE_MAGIC = 0x4d258ab7;

// Constant key of the self-verifying 10-byte tail digest. Session-keyed
// frames use (sessionKey ^ UDP_TAIL_DIGEST_KEY) instead (§5.3).
export const UDP_TAIL_DIGEST_KEY = 0x2b58de69;
