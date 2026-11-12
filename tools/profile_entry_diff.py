#!/usr/bin/env python3
"""Diff the player-profile entries two captures answer a join with, byte for byte.

The client builds its roster table (`DAT_113000d0 + 0xd4`, 24 entries of `0xa8`)
from these records, and the kill feed's killer is resolved through that table, so
a byte that a client needs to file an entry is worth reading off the reference
rather than guessed.

    python3 tools/profile_entry_diff.py REFERENCE.pcapng CANDIDATE.pcapng

Prints, for each side, every entry of the roster run in the order it was sent and
its body as hex, then a byte-wise diff of the entries that carry the same slot,
naming the offsets that differ.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

PROFILE_TYPES = (0x1001, 0x9001, 0x5001)


def entries(path: str) -> list[tuple[int, bytes]]:
    """The roster run's profile entries, in the order the answering side wrote them."""
    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((destination for _, source, destination, _ in datagrams if source == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    found: list[tuple[int, bytes]] = []
    for frame in replay.frames:
        if not frame.verified or frame.source == joiner:
            continue
        for message_type, _length, _flags, body in frame.messages:
            if message_type in PROFILE_TYPES and body and body[0] == 0x07 and len(body) in (57, 63, 68):
                if body not in [entry for _, entry in found]:
                    found.append((body[5], body))
    return found


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    sides = {path: entries(path) for path in sys.argv[1:]}
    for path, found in sides.items():
        print(f"\n{path}: {len(found)} profile entries")
        for slot, body in found:
            print(f"  slot {slot:2d} len={len(body):3d} {body.hex(' ')}")

    reference, candidate = (sides[path] for path in sys.argv[1:])
    by_slot = {slot: body for slot, body in reference}
    print("\nper-slot diff against the reference (offset: reference -> candidate)")
    for slot, body in candidate:
        other = by_slot.get(slot)
        if other is None:
            print(f"  slot {slot:2d}: not in the reference")
            continue
        if len(other) != len(body):
            print(f"  slot {slot:2d}: length {len(other)} -> {len(body)}")
        differing = [
            (offset, other[offset], body[offset])
            for offset in range(min(len(other), len(body)))
            if other[offset] != body[offset]
        ]
        if not differing:
            print(f"  slot {slot:2d}: identical")
        for offset, before, after in differing:
            print(f"  slot {slot:2d} +0x{offset:02x}: {before:02x} -> {after:02x}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
