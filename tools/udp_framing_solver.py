#!/usr/bin/env python3
"""Recover the per-type length convention of the gameplay record stream.

Usage:
    python3 tools/udp_framing_solver.py docs/mgo2-game.pcapng

The lobby-phase records and the in-game tick records do not share a length
convention: for some types the length byte is the body length, for others it is
the body length plus one. This tool does not assume which is which. It walks
every frame under both conventions and, wherever a frame walks under only one,
records the constraint that frame imposes on each type it contains. Frames that
contain both kinds are the ones that pin the partition down, so the search is
constraint propagation: apply a frame's single-convention verdict to every type
it carries, repeat, and report whatever is left over.
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

# A: length byte is the body length. B: length byte is body length plus one.
PLAIN, PLUS_ONE = 0, -1


def frames(capture: Pcapng, key: int):
    """Yield the decompressed content region of each verified frame."""
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
        yield content


def walk(content: bytes, adjust: int):
    """Walk records under one convention; return (lands_exactly, types)."""
    types = []
    offset = 0
    while offset + HEADER <= len(content):
        message_type = content[offset] | (content[offset + 1] << 8)
        body_length = content[offset + 2] + adjust
        if body_length < 0 or offset + HEADER + body_length > len(content):
            return False, types
        types.append(message_type)
        offset += HEADER + body_length
    return offset == len(content), types


def walk_mixed(content: bytes, known: dict[int, int]):
    """Walk using the known per-type convention; return (ok, types, needs)."""
    types = []
    needs = []
    offset = 0
    while offset + HEADER <= len(content):
        message_type = content[offset] | (content[offset + 1] << 8)
        adjust = known.get(message_type)
        if adjust is None:
            # Try both and keep the ones that stay in bounds; the frame's total
            # length is what finally decides.
            options = []
            for candidate in (PLAIN, PLUS_ONE):
                body_length = content[offset + 2] + candidate
                if 0 <= body_length and offset + HEADER + body_length <= len(content):
                    options.append(candidate)
            needs.append((message_type, options))
            if not options:
                return False, types, needs
            adjust = options[0]
        body_length = content[offset + 2] + adjust
        types.append(message_type)
        offset += HEADER + body_length
    return offset == len(content), types, needs


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    capture = Pcapng(sys.argv[1])
    capture.read()
    contents = list(frames(capture, HOST_BASE ^ PEER_BASE))

    # Pass 1: frames that walk under exactly one convention constrain every type
    # they carry to that convention.
    known: dict[int, int] = {}
    contradictions = 0
    for content in contents:
        ok_plain, types_plain = walk(content, PLAIN)
        ok_plus, types_plus = walk(content, PLUS_ONE)
        if ok_plain == ok_plus:
            continue
        adjust = PLAIN if ok_plain else PLUS_ONE
        types = types_plain if ok_plain else types_plus
        for message_type in types:
            previous = known.setdefault(message_type, adjust)
            if previous != adjust:
                contradictions += 1

    print(f"{len(contents)} frames")
    print(f"types pinned by single-convention frames: {len(known)}")
    print(f"contradicting observations: {contradictions}")

    # Pass 2: re-walk everything under the pinned map. Any frame that now lands
    # exactly had a mixed content, which is the confirmation that the partition
    # is right rather than merely self-consistent.
    landing = 0
    failing = []
    for content in contents:
        ok, _, _ = walk_mixed(content, known)
        if ok:
            landing += 1
        else:
            failing.append(content)

    print(f"frames landing exactly under the pinned map: {landing}/{len(contents)}")
    if failing:
        print(f"frames still failing: {len(failing)}")
        for content in failing[:3]:
            print("  ", content.hex())

    plain_types = sorted(t for t, a in known.items() if a == PLAIN)
    plus_one_types = sorted(t for t, a in known.items() if a == PLUS_ONE)
    print(f"\nlength byte IS the body length        ({len(plain_types)} types):")
    print("  " + " ".join(f"0x{t:04x}" for t in plain_types))
    print(f"\nlength byte IS body length + 1       ({len(plus_one_types)} types):")
    print("  " + " ".join(f"0x{t:04x}" for t in plus_one_types))

    unpinned = defaultdict(int)
    for content in contents:
        _, _, needs = walk_mixed(content, known)
        for message_type, options in needs:
            unpinned[message_type] += 1
    if unpinned:
        print(f"\ntypes never pinned ({len(unpinned)}), with how often they appear in "
              f"a mixed frame:")
        for message_type, count in sorted(unpinned.items()):
            print(f"  0x{message_type:04x} {count}")


if __name__ == "__main__":
    main()