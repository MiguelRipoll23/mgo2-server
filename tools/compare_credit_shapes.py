#!/usr/bin/env python3
"""Print the wire shapes behind kill credit, stuns and CQC in a capture.

Usage:
    python3 tools/compare_credit_shapes.py CAPTURE.pcapng [--window FROM TO]

Prints every operations-stream record (the kill feed's `09` and the score
deltas' `0b`), every class-0x00 damage record that is a knife or a stun, and
every class-0x02 state record that carries a CQC action, with the raw record
identifier, the direction and the body in hex.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_bot_combat import slot_of  # noqa: E402
from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--window", nargs=2, type=float, default=None)
    arguments = parser.parse_args()

    capture = Pcapng(arguments.capture)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((d for _, s, d, _ in datagrams if s == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [frame for frame in replay.frames if frame.verified]
    first = frames[0].ts

    feeds, deltas, knives, holds, rosters = [], [], [], [], []
    for frame in frames:
        at = frame.ts - first
        if arguments.window and not (arguments.window[0] <= at <= arguments.window[1]):
            continue
        way = "c>" if frame.source == joiner else "h>"
        for message_type, length, flags, body in frame.messages:
            if message_type & 0x7FF == 0x254 and len(body) >= 3 and body[0] == 0x09:
                feeds.append((at, way, message_type, length, body))
            if message_type & 0x7FF == 0x254 and len(body) >= 2 and body[0] == 0x0B:
                deltas.append((at, way, message_type, length, body))
            if message_type >= 0x1000:
                if body and body[0] == 0x07 and body[1] == len(body) - 7 and len(body) in (57, 61, 63, 68):
                    rosters.append((at, way, message_type, body))
                continue
            if flags == 0x00 and len(body) in (15, 28) and body[0] in (0x42, 0x91):
                knives.append((at, way, message_type, length, body))
            if flags & 0x02 and len(body) >= 2 and (body[0] & 0x3F) in (0x17, 0x18, 0x19):
                holds.append((at, way, message_type, length, body))

    print(f"{arguments.capture}: {len(frames)} frames, first {first:.3f}")
    print(f"\nRoster bodies ({len(rosters)}):")
    seen = set()
    for at, way, message_type, body in rosters:
        if body in seen:
            continue
        seen.add(body)
        print(f"  {at:8.2f}s {way} type={message_type:04x} len={len(body):2d} {body[:24].hex(' ')}")
    print(f"\nKill feed ({len(feeds)}):")
    for at, way, message_type, length, body in feeds:
        print(f"  {at:8.2f}s {way} type={message_type:04x} len={length:2d} {body.hex(' ')}")
    print(f"\nScore deltas / ops 0x0b ({len(deltas)}):")
    for at, way, message_type, length, body in deltas:
        print(f"  {at:8.2f}s {way} type={message_type:04x} len={length:2d} {body.hex(' ')}")
    print(f"\nKnife/stun hits ({len(knives)}):")
    for at, way, message_type, length, body in knives:
        print(f"  {at:8.2f}s {way} type={message_type:04x} slot={slot_of(message_type)} len={length:2d} {body.hex(' ')}")
    print(f"\nCQC state records ({len(holds)}):")
    for at, way, message_type, length, body in holds:
        print(f"  {at:8.2f}s {way} type={message_type:04x} slot={slot_of(message_type)} len={length:2d} {body.hex(' ')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
