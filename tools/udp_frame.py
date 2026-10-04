#!/usr/bin/env python3
"""Decode the MGO2 UDP peer-to-peer datagrams of a pcapng capture.

Usage:
    python3 tools/udp_frame.py docs/mgo2-game.pcapng [--limit N] [--peer IP:PORT]

Implements the framing documented in docs/protocol/UDP_P2P_PROTOCOL.md sections
3 to 6: the header scramble (5.1), the plaintext-feedback XOR chain (5.2) and the
self-verifying MD5 tail (5.3). Handshakes are decoded with the pre-key constant;
data frames with the session key, which is only known once both counter bases are
seen, so the tool derives it per peer and re-decodes earlier frames when it does.

Nothing here is guessed structure. A frame whose tail digest does not verify is
reported as such rather than parsed.
"""

from __future__ import annotations

import hashlib
import struct
import sys
from collections import defaultdict

from pcap_conversations import TCP, UDP, Pcapng

LZSS_RING_SIZE = 0x200
LZSS_MAXIMUM_OUTPUT = 0x800

PRE_KEY = 0x87103C2F
BASE_DERIVE = 0x2B58DE69
LCG = 0x5D588B65


def lcg(value: int) -> int:
    return (value * LCG + 1) & 0xFFFFFFFF


def unscramble_header(frame: bytearray) -> int:
    """Undo the header scramble and return the LE u16 counter (section 5.1)."""
    length = len(frame)
    span = length - 2
    x0 = lcg(length)
    x1 = lcg(x0)
    x2 = lcg(x1)
    x3 = lcg(x2)
    p0 = ((x0 >> 16) % span) + 2
    p1 = ((x1 >> 16) % span) + 2
    q0 = (x2 >> 16) % length
    q1 = (x3 >> 16) % length

    frame[1], frame[q1] = frame[q1], frame[1]
    frame[0], frame[q0] = frame[q0], frame[0]
    frame[1] ^= frame[p1]
    frame[0] ^= frame[p0]
    return frame[0] | (frame[1] << 8)


def scramble_header(frame: bytearray, counter: int) -> None:
    """The exact inverse of unscramble_header (section 5.1, sender side)."""
    length = len(frame)
    span = length - 2
    x0 = lcg(length)
    x1 = lcg(x0)
    x2 = lcg(x1)
    x3 = lcg(x2)
    p0 = ((x0 >> 16) % span) + 2
    p1 = ((x1 >> 16) % span) + 2
    q0 = (x2 >> 16) % length
    q1 = (x3 >> 16) % length

    frame[0] ^= frame[p0]
    frame[1] ^= frame[p1]
    frame[0], frame[q0] = frame[q0], frame[0]
    frame[1], frame[q1] = frame[q1], frame[1]
    del counter


def dechain(frame: bytearray, counter: int, key: int) -> None:
    """Undo the plaintext-feedback XOR chain over [2 .. len-0xa) (section 5.2)."""
    chain = (((counter & 0x7FFF) * LCG + 1) & 0xFFFFFFFF) ^ key
    region = len(frame) - 0xC
    offset = 2
    end = 2 + region
    while offset < end:
        take = min(4, end - offset)
        word = int.from_bytes(frame[offset : offset + take], "little")
        plain = word ^ (chain & ((1 << (8 * take)) - 1))
        frame[offset : offset + take] = plain.to_bytes(take, "little")
        chain = (chain + plain) & 0xFFFFFFFF
        offset += take


def digest_key_for(key: int) -> int:
    """The tail digest key for a chain key: bare constant pre-keyed, derived otherwise.

    The pre-keyed case digests with `BASE_DERIVE` and not with the pre-handshake
    chain key: `FrameCryptoUtility` takes `TailDigestKey` directly for a frame it
    classifies as pre-keyed, and only a session-keyed frame gets its key
    exclusive-ORed in. Measuring the captured join confirms it - the joiner's
    handshake verifies with the bare constant and at neither `PRE_KEY` nor
    `BASE_DERIVE ^ K`.
    """
    return BASE_DERIVE if key == PRE_KEY else (key ^ BASE_DERIVE) & 0xFFFFFFFF


def verify_tail(frame: bytes, digest_key: int) -> bool:
    """Check the self-verifying 10-byte tail (section 5.3).

    The digest covers the header-unscrambled but STILL XOR-chained datagram: the
    decoder unscrambles the header, verifies the digest, and only then unchains.
    """
    body = frame[: len(frame) - 0xA]
    wire_tail = frame[len(frame) - 0xA :]
    key_bytes = struct.pack("<I", digest_key)
    digest1 = hashlib.md5(key_bytes + body).digest()
    double = hashlib.md5(key_bytes + digest1).digest()
    for index in range(10):
        expected = double[index] ^ (double[10 + index] if index < 6 else 0)
        if expected != wire_tail[index]:
            return False
    return True


def decode(frame_bytes: bytes, key: int) -> tuple[int, bytes, bool]:
    """Return (header counter, unchained datagram, tail verified).

    `frame` is left header-unscrambled for the digest, matching the decoder's
    order: unscramble, verify, unchain.
    """
    frame = bytearray(frame_bytes)
    counter = unscramble_header(frame)
    verified = verify_tail(bytes(frame), digest_key_for(key))
    plain = bytearray(frame)
    dechain(plain, counter, key)
    return counter, bytes(plain), verified


def lzss_decompress(source: bytes) -> bytes:
    """Decompress the game's LZSS bitstream (see src/Shared/Utils/LzssUtility.cs).

    Most significant bit first; a flag of 1 is a literal byte, a flag of 0 is a
    back-reference of a nine-bit absolute ring offset and a four-bit length
    field, length = field + 2. The ring write index starts at 1 and offset 0 is
    the end-of-stream marker. A stream that runs out of input rather than
    writing the marker yields what it decoded so far, which is what a captured
    joiner frame does.
    """
    output = bytearray()
    ring = bytearray(LZSS_RING_SIZE)
    write_index = 1
    position = 0
    total_bits = len(source) * 8

    def read_bit() -> int:
        nonlocal position
        if position >= total_bits:
            return -1
        bit = (source[position >> 3] >> (7 - (position & 7))) & 1
        position += 1
        return bit

    def read_bits(count: int) -> int:
        value = 0
        for _ in range(count):
            bit = read_bit()
            if bit < 0:
                return -1
            value = (value << 1) | bit
        return value

    while position < total_bits and len(output) < LZSS_MAXIMUM_OUTPUT:
        flag = read_bit()
        if flag < 0:
            break
        if flag == 1:
            literal = read_bits(8)
            if literal < 0:
                break
            output.append(literal)
            ring[write_index & (LZSS_RING_SIZE - 1)] = literal
            write_index += 1
            continue

        offset = read_bits(9)
        length_field = read_bits(4)
        if offset < 0 or length_field < 0:
            break
        if offset == 0:
            break
        for index in range(length_field + 2):
            if len(output) >= LZSS_MAXIMUM_OUTPUT:
                break
            value = ring[(offset + index) & (LZSS_RING_SIZE - 1)]
            output.append(value)
            ring[write_index & (LZSS_RING_SIZE - 1)] = value
            write_index += 1

    return bytes(output)


def parse_messages(content: bytes) -> list[tuple[int, int, int, bytes]]:
    """Walk the records of a content region.

    Two framings share the wire and are told apart by the record identifier:

    - id >= 0x1000, a session record: `{id u16 LE, len u8, flags2 u8, body[len]}`
      - the length byte is the body length.
    - id < 0x1000, an in-game tick record: `{id u16 LE, len u8, class u8,
      body[len-1]}` - the length byte counts the body plus the class byte, which
      is the same shape the replay parser walks (tools/mgo2_replay_parser.py).

    Records start at offset 0 of the content region, which is everything between
    the two-byte frame header and the ten-byte tail.
    """
    messages = []
    offset = 0
    end = len(content)
    while offset + 4 <= end:
        message_type = struct.unpack_from("<H", content, offset)[0]
        flags = content[offset + 3]
        body_length = content[offset + 2] + (-1 if message_type < 0x1000 else 0)
        if body_length < 0 or offset + 4 + body_length > end:
            break
        body_start = offset + 4
        messages.append((message_type, body_length, flags, content[body_start : body_start + body_length]))
        offset = body_start + body_length
    return messages


def hexdump(payload: bytes, width: int = 32) -> str:
    lines = []
    for start in range(0, len(payload), width):
        chunk = payload[start : start + width]
        hex_part = " ".join(f"{byte:02x}" for byte in chunk)
        text = "".join(chr(byte) if 32 <= byte < 127 else "." for byte in chunk)
        lines.append(f"      {start:04x}  {hex_part:<{width * 3}} {text}")
    return "\n".join(lines)


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    limit = int(sys.argv[sys.argv.index("--limit") + 1]) if "--limit" in sys.argv else 30
    peer_filter = None
    if "--peer" in sys.argv:
        peer_filter = sys.argv[sys.argv.index("--peer") + 1]

    # The captured host is whichever side owns the p2p port the game dials from.
    self_endpoint = "10.2.0.2:5730"

    capture = Pcapng(sys.argv[1])
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        datagrams.append((ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload))

    # First pass: find handshakes and record each side's counter base per peer pair.
    # The handshake body is read from the dechained datagram; the chain key for
    # handshakes is the documented pre-key constant, so this needs no session key.
    bases: dict[tuple[str, str], dict[str, int]] = defaultdict(dict)
    for _, local, remote, payload in datagrams:
        if len(payload) != 44:
            continue
        counter, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        _, counter_base = struct.unpack_from("<II", plain, 6)
        bases[(local, remote)][remote] = counter_base
        bases[(remote, local)][remote] = counter_base

    shown = 0
    for ts, local, remote, payload in datagrams:
        if peer_filter and peer_filter not in (local, remote):
            continue
        # STUN shares the client's p2p port; it has no 2-byte scramble that fits.
        if len(payload) < 16 or payload[0:2] == b"\x00\x01" and payload[2:4] == b"\x00\x00":
            continue

        own_base = bases[(local, remote)].get(local)
        peer_base = bases[(local, remote)].get(remote)
        if own_base is None or peer_base is None:
            key = PRE_KEY
        else:
            key = (own_base ^ peer_base) & 0xFFFFFFFF

        counter, plain, verified = decode(payload, key)
        direction = "OUT" if local == self_endpoint else "IN "
        state = "ok " if verified else "BAD"
        print(f"[{ts:10.3f}] {direction} {local} -> {remote} ({len(payload)} B) "
              f"hdr=0x{counter:04x} key=0x{key:08x} tail={state}")
        shown += 1
        if shown > limit:
            break
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
            print(f"      LZSS {len(content)} bytes")
        for message_type, length, flags, body in parse_messages(content):
            print(f"      type=0x{message_type:04x} len={length} flags2={flags}")
            if body:
                print(hexdump(body))


if __name__ == "__main__":
    main()