#!/usr/bin/env python3
"""Derive and validate the per-type length-convention table for the gameplay stream.

Usage:
    python3 tools/udp_framing_table.py docs/mgo2-game.pcapng [--emit-csharp]

Two conventions are in play. For a session/session-control record the length byte
is the body length; for an in-game record it is the body length plus one, the
extra byte being the attribute class the tick parser reads. The partition is not
guessed: a type is pinned only by frames that walk exactly under one convention
and carry no other explanation, and a type whose observations disagree is left
unpinned rather than assigned.

The table is emitted for the C# codec, and the run reports how many frames the
resulting table parses to the last byte. A table that is merely self-consistent
is not the claim; the claim is the frame count.
"""

from __future__ import annotations

import sys
from collections import defaultdict

from pcap_conversations import Pcapng, UDP
from udp_frame import decode, lzss_decompress

HOST = "10.2.0.2:5730"
PEER = "99.66.131.177:5731"
HOST_BASE = 0x736773A6
PEER_BASE = 0x3C8A65D6

HEADER = 4
TAIL = 10

# The identifier at which a record stops being an in-game tick record. This is
# UdpCommandConstants.TickRecordThreshold; the two must not drift apart.
THRESHOLD = 0x1000

PLAIN, PLUS_ONE = 0, -1

# A type must be seen this many times under a single convention before it is
# pinned. Below it, one mis-walk would decide the type.
MIN_EVIDENCE = 8


def contents(capture: Pcapng, key: int) -> list[bytes]:
    result = []
    for _, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        if {f"{source}:{source_port}", f"{destination}:{destination_port}"} != {HOST, PEER}:
            continue
        counter, plain, verified = decode(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - TAIL]
        if counter & 0x8000:
            content = lzss_decompress(content)
        result.append(content)
    return result


def walk(content: bytes, adjust: int):
    """Walk under one convention; return (exact, [(type, body_offset, body_len)])."""
    records = []
    offset = 0
    while offset + HEADER <= len(content):
        message_type = content[offset] | (content[offset + 1] << 8)
        body_length = content[offset + 2] + adjust
        if body_length < 0 or offset + HEADER + body_length > len(content):
            return False, records
        records.append((message_type, offset + HEADER, body_length))
        offset += HEADER + body_length
    return offset == len(content), records


def walk_by_threshold(content: bytes):
    """Walk choosing the framing by identifier, the rule the server implements."""
    records = []
    offset = 0
    while offset + HEADER <= len(content):
        message_type = content[offset] | (content[offset + 1] << 8)
        body_length = content[offset + 2] + (PLUS_ONE if message_type < THRESHOLD else PLAIN)
        if body_length < 0 or offset + HEADER + body_length > len(content):
            return False, records
        records.append((message_type, offset + HEADER, body_length))
        offset += HEADER + body_length
    return offset == len(content), records


def walk_table(content: bytes, table: dict[int, int]):
    """Walk using the table; unknown types fall back to the body-length reading."""
    records = []
    offset = 0
    while offset + HEADER <= len(content):
        message_type = content[offset] | (content[offset + 1] << 8)
        body_length = content[offset + 2] + table.get(message_type, PLAIN)
        if body_length < 0 or offset + HEADER + body_length > len(content):
            return False, records
        records.append((message_type, offset + HEADER, body_length))
        offset += HEADER + body_length
    return offset == len(content), records


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    capture = Pcapng(sys.argv[1])
    capture.read()
    frames = contents(capture, HOST_BASE ^ PEER_BASE)

    evidence: dict[int, list[int]] = defaultdict(lambda: [0, 0])
    for content in frames:
        ok_plain, records_plain = walk(content, PLAIN)
        ok_plus, records_plus = walk(content, PLUS_ONE)
        if ok_plain == ok_plus:
            continue
        adjust = PLAIN if ok_plain else PLUS_ONE
        records = records_plain if ok_plain else records_plus
        for message_type, _, _ in records:
            evidence[message_type][0 if adjust == PLAIN else 1] += 1

    table: dict[int, int] = {}
    contested = []
    for message_type, (plain_count, plus_count) in evidence.items():
        if plain_count >= MIN_EVIDENCE and plus_count == 0:
            table[message_type] = PLAIN
        elif plus_count >= MIN_EVIDENCE and plain_count == 0:
            table[message_type] = PLUS_ONE
        elif plain_count == 0 and plus_count == 0:
            continue
        else:
            contested.append((message_type, plain_count, plus_count))

    exact = sum(1 for content in frames if walk_table(content, table)[0])
    baseline = sum(1 for content in frames if walk(content, PLAIN)[0])
    all_plus_one = sum(1 for content in frames if walk(content, PLUS_ONE)[0])
    threshold = sum(1 for content in frames if walk_by_threshold(content)[0])

    print(f"frames                                    : {len(frames)}")
    print(f"exact with the body-length rule alone    : {baseline}")
    print(f"exact with the body-length+1 rule alone  : {all_plus_one}")
    print(f"exact with the identifier rule (0x1000)   : {threshold}")
    print(f"exact with the per-type table above       : {exact}")
    print(f"types pinned                             : {len(table)}")
    print(f"types left unpinned (evidence mixed)     : {len(contested)}")
    print()
    print("The identifier rule is the one the docs and MessageCodecUtility use; the")
    print("per-type table is what this tool derives, and it pins fewer types than the")
    print("threshold needs because a type seen only inside mixed frames never gets a")
    print("clean single-convention vote. That is why its count is lower.")
    if contested:
        print("  contested types (message_type, plain_votes, plus_one_votes):")
        for message_type, plain_count, plus_count in sorted(contested)[:20]:
            print(f"    0x{message_type:04x} {plain_count} {plus_count}")

    plain_types = sorted(t for t, a in table.items() if a == PLAIN)
    plus_one_types = sorted(t for t, a in table.items() if a == PLUS_ONE)
    print(f"\nbody-length types ({len(plain_types)}):")
    print("  " + " ".join(f"0x{t:04x}" for t in plain_types))
    print(f"\nbody-length-plus-one types ({len(plus_one_types)}):")
    print("  " + " ".join(f"0x{t:04x}" for t in plus_one_types))

    if "--emit-csharp" in sys.argv:
        print("\n--- C# ---")
        print("/// <summary>Record types whose length byte is the body length plus one.</summary>")
        print("public static readonly ushort[] AttributeClassLengthTypes =")
        print("[")
        for index in range(0, len(plus_one_types), 12):
            chunk = plus_one_types[index : index + 12]
            print("    " + " ".join(f"0x{t:04x}," for t in chunk))
        print("];")


if __name__ == "__main__":
    main()