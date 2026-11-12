#!/usr/bin/env python3
"""Print the records around every kill feed in a capture.

Usage:
    python3 tools/kill_credit_trace.py CAPTURE.pcapng [--limit N]

For each operations-stream `09 <killer> <victim>` record, prints the feed and
every host record of the victim's block and the killer's block in the two
seconds before it, so the fields that name the attacker can be read off.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_bot_combat import slot_of  # noqa: E402
from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402


def slot_of_record(identifier: int) -> int | None:
    text = slot_of(identifier)
    return int(text.split("/")[0]) if "/" in text else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--limit", type=int, default=4)
    parser.add_argument("--back", type=float, default=2.0)
    arguments = parser.parse_args()

    capture = Pcapng(arguments.capture)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((d for _, s, d, _ in datagrams if s == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [frame for frame in replay.frames if frame.verified]
    first = frames[0].ts

    rows = []
    for frame in frames:
        for message_type, length, flags, body in frame.messages:
            if (frame.source != joiner and message_type & 0x7FF == 0x254
                    and body and body[0] == 0x09 and len(body) >= 3):
                rows.append((frame.ts, body[1], body[2], body.hex(" ")))

    print(f"{arguments.capture}: {len(frames)} frames, {len(rows)} host feed records")
    damage = Counter()
    states = Counter()
    for frame in frames:
        if frame.source == joiner:
            continue
        for message_type, length, flags, body in frame.messages:
            if message_type >= 0x1000 or length < 2:
                continue
            if flags == 0x00 and body[0] in (0x11, 0x12, 0x42, 0x91, 0x13):
                damage[(body[0], 1 if len(body) > 1 and body[1] else 0)] += 1
            if flags == 0x01 and slot_of_record(message_type) is not None:
                states[(len(body), body[0] if body else -1, body[1] if len(body) > 1 else -1)] += 1

    print(f"\nHost damage records by (subtype, marker): {dict(damage)}")
    print(f"Host state records by (length, first, target), 8 most common:")
    for key, count in states.most_common(8):
        print(f"  len={key[0]} first=0x{key[1]:02x} target=0x{key[2]:02x} n={count}")

    print(f"\nFirst {arguments.limit} kills, host records of killer and victim:")
    for at, killer, victim, text in rows[:arguments.limit]:
        relative = at - first
        print(f"\n-- t+{relative:.2f}s  feed {text}  killer={killer} victim={victim}")
        for frame in frames:
            when = frame.ts - first
            if frame.source == joiner or not (relative - arguments.back <= when <= relative):
                continue
            for message_type, length, flags, body in frame.messages:
                slot = slot_of_record(message_type)
                if slot not in (killer, victim):
                    continue
                if flags == 0x02 and len(body) > 2:
                    continue
                print(f"   {when:8.2f}s slot={slot:2d} cls={flags:02x} len={length:2d} {body.hex(' ')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
