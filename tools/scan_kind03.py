#!/usr/bin/env python3
"""Print every kind-0x03 record (and the stage kind-0x01 body) in the captures.

Usage:
    python3 tools/scan_kind03.py [capture.pcapng ...]
"""

from __future__ import annotations

import glob
import struct
import sys
from collections import defaultdict

from pcap_conversations import UDP, Pcapng
from udp_frame import (
    decode_frame,
    gameplay_endpoints,
    handshake_identity_of,
    lzss_decompress,
    parse_messages,
)

ROSTER_IDENTIFIERS = (0x1001, 0x9001)


def signed16(body: bytes, offset: int) -> int:
    return struct.unpack_from("<h", body, offset)[0]


def datagrams_of(path: str) -> list[tuple]:
    capture = Pcapng(path)
    capture.read()
    return [
        (ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload)
        for ts, protocol, source, source_port, destination, destination_port, payload
        in capture.decoded_packets()
        if protocol == UDP
    ]


def session_key(rows: list[tuple], pair: frozenset[str]) -> int:
    bases: dict[str, int] = {}
    for _, source, destination, payload in rows:
        if frozenset((source, destination)) != pair:
            continue
        identity = handshake_identity_of(payload)
        if identity is None:
            continue
        bases[source] = identity[1]
    if len(bases) != 2:
        raise SystemExit("no handshake from both sides")
    first, second = bases.values()
    return (first ^ second) & 0xFFFFFFFF


def scan(path: str) -> dict[int, list[bytes]]:
    rows = datagrams_of(path)
    if not rows:
        return {}
    pair = gameplay_endpoints([(ts, source, destination, payload) for ts, source, destination, payload in rows])
    key = session_key(rows, pair)
    found: dict[int, list[bytes]] = defaultdict(list)
    for ts, source, _, payload in rows:
        if source not in pair:
            continue
        _, plain, verified, _ = decode_frame(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if struct.unpack_from(">H", payload, 0)[0] & 0x8000:
            content = lzss_decompress(content)
        for message_type, length, _, body in parse_messages(content):
            if message_type not in ROSTER_IDENTIFIERS:
                continue
            if length < 8 or body[0] != 0x07 or body[3] == 0x00:
                continue
            found[body[3]].append(body)
    return found


def main() -> None:
    paths = sys.argv[1:] or sorted(glob.glob(".pcapngs/**/*.pcapng", recursive=True))
    for path in paths:
        name = path.replace("\\", "/")
        try:
            found = scan(path)
        except SystemExit as error:
            print(f"=== {name}: {error}")
            continue
        except Exception as error:
            print(f"=== {name}: unreadable ({error})")
            continue
        kinds = {kind: len(bodies) for kind, bodies in sorted(found.items())}
        print(f"=== {name}: {kinds}")
        for body in found.get(0x03, []):
            first = tuple(signed16(body, 0x08 + 2 * i) for i in range(3))
            second = tuple(signed16(body, 0x0E + 2 * i) for i in range(3))
            print(
                f"    03 idx={body[4]} tag={body[7]:02x} first={first} "
                f"second={second} tail={struct.unpack_from('<H', body, 0x14)[0]:04x}"
            )


if __name__ == "__main__":
    main()
