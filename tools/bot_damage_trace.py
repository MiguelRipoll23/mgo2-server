#!/usr/bin/env python3
"""Trace one side's damage records against the vitals the other side answers with.

A hit is only applied if the victim's health moves, so a damage record with no
answer beside it is a hit the host ignored — the "bots do not take damage" report.

    python3 tools/bot_damage_trace.py CAPTURE.pcapng [--writer joiner|host]

Prints every class-0x00 hit in arrival order with the side that wrote it, the slot
its identifier names, its subtype and points, and the victim's next vitals record
within two seconds.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_bot_combat import slot_of  # noqa: E402
from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

HIT_LENGTHS = (15, 28)
SUBTYPES = (0x11, 0x12, 0x42, 0x91, 0x13)


def slot(text: str) -> int | None:
    return int(text.split("/")[0]) if "/" in text else None


def main() -> int:
    path = sys.argv[1]
    writer = None
    if "--writer" in sys.argv:
        writer = sys.argv[sys.argv.index("--writer") + 1]

    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((destination for _, source, destination, _ in datagrams if source == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [frame for frame in replay.frames if frame.verified]
    first = frames[0].ts

    vitals: dict[int, list[tuple[float, int]]] = {}
    hits = []
    for frame in frames:
        at = frame.ts - first
        side = "joiner" if frame.source == joiner else "host"
        for message_type, length, flags, body in frame.messages:
            if flags == 0x03 and length == 2 and message_type < 0x1000:
                victim = slot(slot_of(message_type))
                if victim is not None:
                    vitals.setdefault(victim, []).append((at, body[0]))
            if flags != 0x00 or length not in HIT_LENGTHS or not body:
                continue
            if body[0] not in SUBTYPES or message_type >= 0x1000:
                continue
            victim = slot(slot_of(message_type))
            if victim is None:
                continue
            hits.append((at, side, victim, body[0], (body[2] | (body[3] << 8)) / 128.0))

    print(f"{path}: {len(hits)} damage records")
    counts: dict[str, int] = {}
    unanswered: list[tuple] = []
    for at, side, victim, subtype, points in hits:
        counts[side] = counts.get(side, 0) + 1
        if writer and side != writer:
            continue
        after = [(when, health) for when, health in vitals.get(victim, []) if at <= when <= at + 2.0]
        change = next(
            (
                (when, health)
                for index, (when, health) in enumerate(after)
                if index and health != after[index - 1][1]
            ),
            None,
        )
        mark = ""
        if change is None:
            unanswered.append((at, side, victim, subtype, points))
            mark = "  <-- no vitals change within 2s"
        print(
            f"  t+{at:7.2f} {side:6s} slot {victim:2d} subtype=0x{subtype:02x} "
            f"points={points:6.1f}"
            + (f" -> {change[1]:3d} at t+{change[0]:.2f}" if change else "")
            + mark
        )

    print(f"  by writer: {counts}; with no answer: {len(unanswered)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
