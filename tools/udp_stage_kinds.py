#!/usr/bin/env python3
"""Dump the stage records of the `0x1001` family, capture by capture.

Usage:
    python3 tools/udp_stage_kinds.py [capture.pcapng ...]

Defaults to every `.pcapng` under `.pcapngs/`. For each capture it decodes the
gameplay session, collects the roster-shaped records whose kind byte at `+3` is
`0x01`-`0x04`, and prints the payloads decoded as the signed sixteens they are
believed to hold:

- kind `0x01` (54 bytes): a quad of four signed sixteens at `0x0b`, then four
  eight-byte items at `0x16`, each read as four signed sixteens and as hex.
- kind `0x02` (20 and 52 bytes): the eight-byte header, the corner count, and
  the corners as signed sixteens.
- kind `0x03` (22 bytes): the tag, the triple written twice, and the tail.
- kind `0x04` (25 bytes): the slot, the team/place, the heading and the point.

The point of the listing is comparison: two captures on one stage write one
body, so a kind-`0x01` body that differs between captures is a different stage's
and its four items can be read against that stage's other records.
"""

from __future__ import annotations

import glob
import struct
import sys
from collections import Counter, defaultdict

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


def unsigned16(body: bytes, offset: int) -> int:
    return struct.unpack_from("<H", body, offset)[0]


def datagrams_of(path: str) -> list[tuple]:
    capture = Pcapng(path)
    capture.read()
    rows = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        rows.append((ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload))
    return rows


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
        raise SystemExit("the flow does not carry a handshake from both sides")
    first, second = bases.values()
    return (first ^ second) & 0xFFFFFFFF


def stage_records(path: str) -> dict[int, list[bytes]]:
    rows = datagrams_of(path)
    if not rows:
        return {}
    pair = gameplay_endpoints([(ts, source, destination, payload) for ts, source, destination, payload in rows])
    key = session_key(rows, pair)

    found: dict[int, list[bytes]] = defaultdict(list)
    for ts, source, _, payload in rows:
        if frozenset((source,)) and source not in pair:
            continue
        counter, plain, verified, _ = decode_frame(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for message_type, length, _, body in parse_messages(content):
            if message_type not in ROSTER_IDENTIFIERS:
                continue
            if length < 8 or body[0] != 0x07 or body[3] == 0x00:
                continue
            found[body[3]].append(body)
    return found


def render_kind_one(body: bytes) -> str:
    if len(body) < 54:
        return "short form: " + " ".join(f"{byte:02x}" for byte in body)
    quad = tuple(signed16(body, 0x0B + 2 * index) for index in range(4))
    items = []
    for index in range(4):
        start = 0x16 + 8 * index
        values = tuple(signed16(body, start + 2 * field) for field in range(4))
        raw = " ".join(f"{unsigned16(body, start + 2 * field):04x}" for field in range(4))
        items.append(f"{values} [{raw}]")
    return (
        f"idx={body[4]} var9={body[0x09]:02x} varA={body[0x0A]:02x} "
        f"quad={quad} items={items}"
    )


def render_kind_two(body: bytes) -> str:
    header = " ".join(f"{byte:02x}" for byte in body[0x07:0x11])
    count = body[0x11] | (body[0x12] << 8) | (body[0x13] << 16)
    fitted = min(count, (len(body) - 0x14) // 8)
    corners = [
        tuple(signed16(body, 0x14 + 8 * index + 2 * field) for field in range(4))
        for index in range(fitted)
    ]
    return f"idx={body[4]} header={header} count={count} corners={corners}"


def render_kind_three(body: bytes) -> str:
    if len(body) < 22:
        return "short form: " + " ".join(f"{byte:02x}" for byte in body)
    first = tuple(signed16(body, 0x08 + 2 * index) for index in range(3))
    second = tuple(signed16(body, 0x0E + 2 * index) for index in range(3))
    return f"tag={body[7]:02x} first={first} second={second} tail={unsigned16(body, 0x14):04x}"


def render_kind_four(body: bytes) -> str:
    if len(body) < 25:
        # The nine-byte marker: a player the host names without a payload.
        return "marker: " + " ".join(f"{byte:02x}" for byte in body)
    heading = signed16(body, 0x0A)
    point = tuple(struct.unpack_from("<iii", body, 0x0C))
    return f"slot={body[7]}/{body[8]} place={body[9]} heading={heading} point={point}"


RENDERERS = {
    0x01: render_kind_one,
    0x02: render_kind_two,
    0x03: render_kind_three,
    0x04: render_kind_four,
}


def main() -> None:
    paths = sys.argv[1:] or sorted(glob.glob(".pcapngs/*.pcapng"))
    if not paths:
        print(__doc__)
        return

    bodies: dict[bytes, list[str]] = defaultdict(list)
    for path in paths:
        name = path.replace("\\", "/").rsplit("/", 1)[-1]
        try:
            found = stage_records(path)
        except SystemExit as error:
            print(f"=== {name}: {error}")
            continue
        except Exception as error:  # a capture this reader cannot key is reported, not guessed
            print(f"=== {name}: unreadable ({error})")
            continue
        if not found:
            print(f"=== {name}: no stage records")
            continue
        print(f"=== {name}")
        for kind in sorted(found):
            unique = Counter(found[kind])
            print(f"  kind 0x{kind:02x}: {len(found[kind])} records, {len(unique)} distinct")
            for body, count in unique.most_common():
                bodies[bytes([kind]) + body].append(name)
                print(f"    x{count} len={len(body)}  {RENDERERS[kind](body)}")
        print()

    print("=== bodies by capture")
    for key, names in sorted(bodies.items(), key=lambda entry: (entry[0][0], -len(entry[1]))):
        kind, body = key[0], key[1:]
        shared = ", ".join(sorted(set(names)))
        print(f"  kind 0x{kind:02x} idx={body[4]} len={len(body)}: {shared}")


if __name__ == "__main__":
    main()
