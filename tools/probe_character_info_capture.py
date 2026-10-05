#!/usr/bin/env python3
"""Decrypt the game server's 0x4101 character-info frames from mgo2-game.pcapng
and show the bytes this server sends differently in the main-menu flags, the
content mask and the trailer.

Usage:
    python3 tools/probe_character_info_capture.py

Only mgo2-game.pcapng is read. The server->client TCP flow on the game port
(5733) is reassembled per direction. Each frame is a 24-byte header plus payload
XOR'd with 0x5a7085af; a non-XOR prologue and control blocks between frames are
skipped by resynchronising on a plausible frame header.
"""

from __future__ import annotations

import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng, TCP  # noqa: E402

CAPTURE = Path(__file__).resolve().parent.parent / "mgo2-game.pcapng"
# The game server flow in the capture: client 10.2.0.2:65217 <-> 15.204.239.231:5733.
GAME_SERVER = ("15.204.239.231", 5733)
XOR_KEY = bytes.fromhex("5a7085af")
COMMAND_CHARACTER_INFO = 0x4101

# What src/GameLobbyServer/Commands/Game/Characters/CharacterInfoPayloadBuilder.cs sends.
OURS_TAIL_BYTE = 0x00
OURS_CONTENT_MASK = bytes([0xB7, 0xFD, 0xCB, 0xFC, 0xFF, 0xFF, 0x7B] + [0x00] * 9)
# The trailer is dead_13100 (zero) then grade_points, which mirrors the captured
# character's experience of 0x578, so it depends on the record being sent.
OURS_TRAILER = bytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x78])
OURS_FEATURE_BYTE = 0x00

TAIL_BYTE_OFFSET = 0x229
CONTENT_MASK_OFFSET = 0x22A
CONTENT_MASK_SIZE = 16
TRAILER_OFFSET = 0x23A
TRAILER_SIZE = 8
FEATURE_BYTE_OFFSET = 0x242


def xor(data: bytes) -> bytes:
    """Undo the per-frame XOR."""
    return bytes(value ^ XOR_KEY[index & 3] for index, value in enumerate(data))


def frame_at(data: bytes, offset: int) -> tuple[int, int, bytes, int] | None:
    """Return (command, payload length, payload, total) for a frame at offset."""
    if offset + 24 > len(data):
        return None
    header = xor(data[offset:offset + 24])
    command = struct.unpack_from(">H", header, 0)[0]
    length = struct.unpack_from(">H", header, 2)[0]
    if length > 0x3FF or not 0x0100 <= command <= 0x4FFF:
        return None
    total = 24 + length
    if offset + total > len(data):
        return None
    return command, length, xor(data[offset + 24:offset + total]), total


def walk(data: bytes):
    """Yield each decodable frame, skipping the unencrypted blocks between them."""
    offset = 0
    while offset < len(data):
        frame = frame_at(data, offset)
        if frame is not None:
            yield frame
            offset += frame[3]
            continue
        offset += 1


def collect_server_stream(capture: Pcapng) -> bytes:
    """Reassemble the server->client bytes of the game-server TCP flow."""
    chunks = bytearray()
    for _ts, protocol, source, source_port, _destination, _destination_port, payload in capture.decoded_packets():
        if protocol != TCP or not payload:
            continue
        if source == GAME_SERVER[0] and source_port == GAME_SERVER[1]:
            chunks += payload
    return bytes(chunks)


def region(payload: bytes, offset: int, size: int) -> bytes:
    """Safely slice a region, padding short payloads with the codec's zero tail."""
    return payload[offset:offset + size].ljust(size, b"\x00")


def format_mask(mask: bytes) -> str:
    """Render the 16-byte mask as hex plus the list of set and clear bits."""
    set_bits = [index for index in range(128) if index < 56 and (mask[index // 8] >> (index % 8)) & 1]
    clear_bits = [index for index in range(56) if not (mask[index // 8] >> (index % 8)) & 1]
    return f"{mask.hex(' ')}\n      set   bits: {set_bits}\n      clear bits: {clear_bits}"


def main() -> int:
    if not CAPTURE.exists():
        print(f"{CAPTURE} not found", file=sys.stderr)
        return 1

    capture = Pcapng(str(CAPTURE))
    capture.read()
    stream = collect_server_stream(capture)
    print(f"{CAPTURE.name}: {len(stream)} server->client bytes on {GAME_SERVER[0]}:{GAME_SERVER[1]}")

    found = False
    for command, length, payload, _total in walk(stream):
        if command != COMMAND_CHARACTER_INFO:
            continue
        found = True
        print(f"\n0x{command:04x} len={length} (0x{length:x})")
        if len(payload) < FEATURE_BYTE_OFFSET + 1:
            print("  payload is short of the trailer")
            continue

        tail_byte = payload[TAIL_BYTE_OFFSET]
        mask = region(payload, CONTENT_MASK_OFFSET, CONTENT_MASK_SIZE)
        trailer = region(payload, TRAILER_OFFSET, TRAILER_SIZE)
        feature_byte = payload[FEATURE_BYTE_OFFSET]

        print(f"  0x229 tail byte      : 0x{tail_byte:02x}   (ours 0x{OURS_TAIL_BYTE:02x})")
        print(f"  0x22a content mask   : {format_mask(mask)}")
        print(f"                         ours {format_mask(OURS_CONTENT_MASK)}")
        print(f"  0x23a trailer        : {trailer.hex(' ')}   (ours {OURS_TRAILER.hex(' ')})")
        print(f"  0x242 main-menu flags: 0x{feature_byte:02x}   (ours 0x{OURS_FEATURE_BYTE:02x})")

        print("  differing offsets in this packet's tail:")
        differences = []
        if tail_byte != OURS_TAIL_BYTE:
            differences.append(f"    0x{TAIL_BYTE_OFFSET:03x} 0x{tail_byte:02x} != ours 0x{OURS_TAIL_BYTE:02x}")
        for index in range(CONTENT_MASK_SIZE):
            if mask[index] != OURS_CONTENT_MASK[index]:
                differences.append(
                    f"    0x{CONTENT_MASK_OFFSET + index:03x} 0x{mask[index]:02x} != ours 0x{OURS_CONTENT_MASK[index]:02x}"
                )
        for index in range(TRAILER_SIZE):
            if trailer[index] != OURS_TRAILER[index]:
                differences.append(
                    f"    0x{TRAILER_OFFSET + index:03x} 0x{trailer[index]:02x} != ours 0x{OURS_TRAILER[index]:02x}"
                )
        if feature_byte != OURS_FEATURE_BYTE:
            differences.append(
                f"    0x{FEATURE_BYTE_OFFSET:03x} 0x{feature_byte:02x} != ours 0x{OURS_FEATURE_BYTE:02x}"
            )
        print("\n".join(differences) if differences else "    none")

    if not found:
        print("no 0x4101 frame found")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
