#!/usr/bin/env python3
"""Every damage record must sit on a beat whose shooter shows the shot state.

The client draws a wound from the damage record and the muzzle from the shooter's
own state block on the same beat: `40|7` with a `1` in the third byte is the
shot's beat (`BotActorRecordUtils.BuildStateBody`). A record that lands on a beat
where no slot shows that state is a hit out of nothing — the bug of 2026-11-12.

Usage:
    python3 tools/beat_shot_check.py [capture ...]

Defaults to both captures in `.pcapngs/`. Prints, per capture, how many damage
records had a slot firing on the same frame, how many only on a neighbouring
frame, and how many had none at all.

The numbers are diagnostic and not a pass/fail: the working capture itself writes
42% of its damage on frames where no slot shows the shot state, so a hit without
a firing state is not by itself a fault in the host's writing. What the check is
for is comparing a host against the reference and against its own past runs.
"""

from __future__ import annotations

import sys
from collections import defaultdict

sys.path.insert(0, "tools")

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

VITALS_SLOT_ZERO = 0x0844
SLOT_STRIDE = 0x0A
SIBLING_BIT = 0x800
STATE_CLASS = 0x01
DAMAGE_CLASS = 0x00
DAMAGE_LENGTHS = (15, 16, 28, 29)
SHOT_ACTION = 7


def slot_of(identifier: int, offset: int) -> int | None:
    """The slot an identifier names, given the offset from its slot-zero record."""
    for generation in (0, SIBLING_BIT):
        value = identifier ^ generation
        delta = value - (VITALS_SLOT_ZERO + offset)
        if delta < 0 or delta % SLOT_STRIDE:
            continue
        slot = delta // SLOT_STRIDE
        if 0 <= slot < 24:
            return slot
    return None


def main(paths: list[str]) -> int:
    if not paths:
        paths = [".pcapngs/myserver.pcapng", ".pcapngs/actions/actions.pcapng"]

    for path in paths:
        capture = Pcapng(path)
        capture.read()
        datagrams = collect_datagrams(capture)
        joiner = find_joiner(datagrams)
        # The joiner's datagram names the host as its destination; the host's own
        # records are the ones this check reads.
        host = next(
            (destination for _, source, destination, _ in datagrams if source == joiner),
            joiner,
        )
        replay = Replay(datagrams, joiner, host)
        frames = [frame for frame in replay.frames if frame.verified]

        # Per frame index: slot -> whether that slot's state says it is shooting.
        shooting: dict[int, set[int]] = defaultdict(set)
        damage: list[tuple[int, int]] = []
        for index, frame in enumerate(frames):
            for message_type, length, flags, body in frame.messages:
                if frame.source != host or body is None:
                    continue
                if flags == STATE_CLASS and length in (3, 9):
                    slot = slot_of(message_type, -1)
                    if slot is None:
                        continue
                    if length == 9 and (body[0] & 0x3F) == SHOT_ACTION:
                        shooting[index].add(slot)
                elif (
                    flags == DAMAGE_CLASS
                    and length in DAMAGE_LENGTHS
                    and body[0] in (0x11, 0x12, 0x42, 0x91, 0x13)
                ):
                    victim = slot_of(message_type, 6)
                    if victim is not None:
                        damage.append((index, victim))

        same = near = none = 0
        misses: list[tuple[int, int]] = []
        for index, victim in damage:
            if shooting.get(index):
                same += 1
                continue
            if shooting.get(index - 1) or shooting.get(index + 1):
                near += 1
                continue
            none += 1
            misses.append((index, victim))

        total = max(1, len(damage))
        print(f"\n{path}")
        print(f"  damage records: {len(damage)}")
        print(f"  a slot firing on the same beat:  {same} ({100 * same // total}%)")
        print(f"  only within one frame:           {near}")
        print(f"  no slot firing at all:           {none} ({100 * none // total}%)")
        for index, victim in misses[:8]:
            print(f"    frame {index}: damage on slot {victim}, no slot firing")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
