"""Decode the patcher's logged lobby frames with the repo's own frame cipher.

The cipher is the shared one (`src/Shared/Utils/FrameCryptoUtility.cs`), so the
readers in `tools/udp_frame.py` are the reference and are reused here rather than
copied: this file is about the patcher's log, not about the arithmetic.

A lobby session is keyed like a peer session is. Both sides open with a
pre-keyed handshake that announces a counter base, and the key the rest of the
session uses is the exclusive-or of the two bases (`AcceptHandshakeHandler`:
`SessionKey = handshake.CounterBase ^ hostIdentity.CounterBase`). The log holds
both directions of every lobby port, so the handshakes are read first, the key
derived from them, and every frame then decoded with it.

A frame the patcher logged only in part cannot be verified at all -- the tail
digest covers everything before the last ten bytes -- so it is reported as
truncated instead of decoded.

Usage:
    python3 tools/lobby_frame_decode.py mgo2-patcher.log [--port N]
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from udp_frame import (  # noqa: E402
    PRE_KEY,
    decode_frame,
    handshake_identity_of,
    lzss_decompress,
    parse_messages,
)

COMPRESSION_MARKER = 0x8000
TAIL = 0xA
HEADER = 2

LINE = re.compile(r"lobby: (in|out) frame of (\d+) byte\(s\) on port (\d+): ([0-9a-f ]+)")


def frames_of(path: str) -> list[tuple[str, int, int, bytes]]:
    """Every logged frame as (direction, port, length the socket moved, wire bytes)."""
    found: list[tuple[str, int, int, bytes]] = []
    for line in open(path, "r", errors="ignore"):
        match = LINE.search(line)
        if not match:
            continue
        direction, length, port, hex_text = match.groups()
        try:
            wire = bytes.fromhex(hex_text)
        except ValueError:
            continue
        found.append((direction, int(port), int(length), wire))
    return found


def session_keys(frames: list[tuple[str, int, int, bytes]]) -> dict[int, int]:
    """Per port, the key both sides derived: the two counter bases exclusive-ored.

    A handshake is pre-keyed, so it is readable before any key is known, and the
    two directions of one lobby session carry one each.
    """
    bases: dict[tuple[int, str], int] = {}
    for direction, port, length, wire in frames:
        if len(wire) != length:
            continue
        identity = handshake_identity_of(wire)
        if identity is not None:
            bases[(port, direction)] = identity[1]

    keys: dict[int, int] = {}
    for (port, direction), base in bases.items():
        other = bases.get((port, "out" if direction == "in" else "in"))
        if other is not None:
            keys[port] = base ^ other
    return keys


def records_of(counter: int, plain: bytes) -> list[tuple[int, int, int, bytes]]:
    content = plain[HEADER:len(plain) - TAIL]
    if counter & COMPRESSION_MARKER:
        content = lzss_decompress(content)
    return parse_messages(content)


def main(path: str, only_port: int | None) -> int:
    frames = frames_of(path)
    keys = session_keys(frames)
    print(f"{len(frames)} logged frame(s) in {path}")
    for port, key in sorted(keys.items()):
        print(f"  port {port}: session key {key:#010x} (from the two handshakes)")

    decoded = 0
    for direction, port, length, wire in frames:
        if only_port is not None and port != only_port:
            continue
        if len(wire) != length:
            print(f"{direction:3} port {port} {length} B: truncated, {len(wire)} B logged"
                  f" -- a partial frame cannot be verified")
            continue

        if port in keys:
            counter, plain, verified, pre_keyed = decode_frame(wire, keys[port])
        else:
            counter, plain, verified, pre_keyed = decode_frame(wire, PRE_KEY)
        if not verified:
            print(f"{direction:3} port {port} {length} B: neither the session key nor the"
                  f" pre-handshake key verifies its tail")
            continue

        decoded += 1
        label = "pre-handshake key" if pre_keyed else "session key"
        print(f"{direction:3} port {port} {length} B: counter {counter}, {label}")
        for message_type, body_length, fourth, body in records_of(counter, plain):
            print(f"    0x{message_type:04x} len {body_length} fourth {fourth}: {body.hex()}")

    print(f"decoded {decoded} frame(s)")
    return 0 if decoded or not frames else 1


if __name__ == "__main__":
    port = None
    if "--port" in sys.argv:
        port = int(sys.argv[sys.argv.index("--port") + 1])
    sys.exit(main(sys.argv[1], port))
