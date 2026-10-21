#!/usr/bin/env python3
"""Check the settings header a reference host writes at a join, byte for byte.

Usage:
    python3 tools/join_header_check.py <capture.pcapng> [<capture.pcapng> ...]
                                       [--verbose]

The record is `SettingsHeaderBodiesUtils.BuildJoin`: a 25-byte `0b` body with
field 1's bit and field 156's bit set in the 21-byte presence map at offset 2 —

    0b SS 02 00 ... 00 10 00 fe ff

Byte 2 is field 1's bit (the map is read low bit first, so bit 1 is `0x02`) with
the value 0xFE at offset 23, and byte 21 is field 156's bit (`0x10`) with 0xFF at
offset 24; every other byte is zero. This tool prints
every copy it finds, says whether it matches that layout, and exits non-zero if
one does not. A capture with no such record is reported as a count of zero
rather than silently skipped, which is what makes the "missing from our joins"
claim checkable.

A 25-byte `0b` body is not always this record: a scoreboard or a slot-state
transfer is the same length, and is told apart by which bits its map sets. Those
are counted as `other` and not checked, so the two families stay visible.
"""

from __future__ import annotations

import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    PRE_KEY,
    decode_frame,
    handshake_of,
    lzss_decompress,
    parse_messages,
)

PAYLOAD_TYPE = 0x0B
PAYLOAD_LENGTH = 25
SLOT_INDEX = 1
MAP_OFFSET = 2
MAP_LENGTH = 21
FIRST_FIELD_INDEX = 1
SECOND_FIELD_INDEX = 156
FIRST_FIELD_VALUE = 0xFE
SECOND_FIELD_VALUE = 0xFF


def expected(slot: int) -> bytes:
    """The join-time body for a slot, as the server's builder writes it."""
    body = bytearray(PAYLOAD_LENGTH)
    body[0] = PAYLOAD_TYPE
    body[SLOT_INDEX] = slot
    body[MAP_OFFSET + FIRST_FIELD_INDEX // 8] |= 1 << (FIRST_FIELD_INDEX % 8)
    body[MAP_OFFSET + SECOND_FIELD_INDEX // 8] |= 1 << (SECOND_FIELD_INDEX % 8)
    body[MAP_OFFSET + MAP_LENGTH] = FIRST_FIELD_VALUE
    body[MAP_OFFSET + MAP_LENGTH + 1] = SECOND_FIELD_VALUE
    return bytes(body)


def is_join_header(body: bytes) -> bool:
    """Whether a 25-byte settings body is the join-time header (both fields set)."""
    if len(body) != PAYLOAD_LENGTH or body[0] != PAYLOAD_TYPE:
        return False
    first = body[MAP_OFFSET + FIRST_FIELD_INDEX // 8]
    second = body[MAP_OFFSET + SECOND_FIELD_INDEX // 8]
    return first == 1 << (FIRST_FIELD_INDEX % 8) and second == 1 << (
        SECOND_FIELD_INDEX % 8
    )


def datagrams_of(capture_path: str) -> list[tuple]:
    capture = Pcapng(capture_path)
    capture.read()
    return [
        (timestamp, f"{source}:{source_port}", f"{destination}:{destination_port}", payload)
        for timestamp, protocol, source, source_port, destination, destination_port, payload
        in capture.decoded_packets()
        if protocol == UDP
    ]


def roles_and_keys(datagrams: list[tuple]) -> tuple[dict[frozenset, str], dict]:
    joiners: dict[frozenset, str] = {}
    bases: dict[tuple[str, str], dict[str, int]] = defaultdict(dict)
    for _, local, remote, payload in datagrams:
        handshake = handshake_of(payload)
        if handshake is None:
            continue
        _, counter_base = handshake
        if local != remote and frozenset((local, remote)) not in joiners:
            joiners[frozenset((local, remote))] = local
        bases[(local, remote)][remote] = counter_base
        bases[(remote, local)][remote] = counter_base
    return joiners, bases


def bodies_of(capture_path: str) -> tuple[list[tuple[float, str, bytes]], int]:
    """Every 25-byte `0b` body, and how many of them are the join-time header."""
    datagrams = datagrams_of(capture_path)
    if not datagrams:
        return [], 0
    start = datagrams[0][0]
    joiners, bases = roles_and_keys(datagrams)
    found: list[tuple[float, str, bytes]] = []
    others = 0
    for timestamp, local, remote, payload in datagrams:
        # STUN shares the p2p port and has no scramble that fits.
        if len(payload) < 16:
            continue
        own = bases[(local, remote)].get(local)
        peer = bases[(local, remote)].get(remote)
        key = PRE_KEY if own is None or peer is None else (own ^ peer) & 0xFFFFFFFF
        counter, plain, verified, _ = decode_frame(payload, key)
        if not verified:
            continue
        joiner = joiners.get(frozenset((local, remote)))
        direction = "--" if joiner is None else ("IN" if local == joiner else "OUT")
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for _, _, _, body in parse_messages(content):
            if len(body) != PAYLOAD_LENGTH or body[:1] != bytes([PAYLOAD_TYPE]):
                continue
            if is_join_header(body):
                found.append((timestamp - start, direction, body))
            else:
                others += 1
    return found, others


def main() -> None:
    arguments = [argument for argument in sys.argv[1:] if not argument.startswith("--")]
    verbose = "--verbose" in sys.argv
    if not arguments:
        print(__doc__)
        return

    failures = 0
    for capture_path in arguments:
        headers, others = bodies_of(capture_path)
        other_note = f", {others} other 25-byte 0b body(ies)" if others else ""
        print(f"{Path(capture_path).name}: {len(headers)} join-time header(s){other_note}")
        if not headers:
            continue
        slots = Counter(body[SLOT_INDEX] for _, _, body in headers)
        shapes = Counter(body for _, _, body in headers)
        print(f"  slots {dict(sorted(slots.items()))}")
        for body, count in shapes.items():
            verdict = "matches BuildJoin" if body == expected(body[SLOT_INDEX]) else "MISMATCH"
            if body != expected(body[SLOT_INDEX]):
                failures += 1
            print(f"  {count:3} x slot {body[SLOT_INDEX]:2}: {verdict}  {body.hex(' ')}")
        if verbose:
            for when, direction, body in headers:
                print(f"    t+{when:8.3f} {direction:3} slot={body[SLOT_INDEX]}")

    if failures:
        raise SystemExit(f"{failures} distinct header shape(s) do not match BuildJoin")


if __name__ == "__main__":
    main()
