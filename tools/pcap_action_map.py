#!/usr/bin/env python3
"""Summarize a gameplay capture's action, pose, weapon and damage records.

Usage:
    python3 tools/pcap_action_map.py CAPTURE.pcapng [--from SECONDS] [--to SECONDS] [--slot N]

The actor block is six tick records ten identifiers apart: state (class 0x01) at
the base, vitals (class 0x03) at +1, position (class 0x02) at +2, the weapon pair
(class 0x04) at +4, and aim (class 0x05) at +6 (`BotActorRecordUtils`).

This prints the runs of state actions per slot, the position heads and pose
bytes, the weapon pair, and the damage subtypes. It is the evidence tool behind
`docs/protocol/ACTOR_POSE_AND_ACTION.md`: every row there is a line this prints.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

STATE_BASES = (0x084D, 0x004D)
SLOT_STRIDE = 0x0A
SLOTS = 24


def slot_of(identifier: int) -> str:
    """The slot and block offset an identifier belongs to, as `slot/offset`."""
    for base in STATE_BASES:
        offset = identifier - base
        if 0 <= offset < SLOT_STRIDE * SLOTS:
            return f"{offset // SLOT_STRIDE + 1}/{offset % SLOT_STRIDE}"
    return f"0x{identifier:04x}"


def slot_number(identifier: int) -> int | None:
    """The slot an identifier belongs to, or None when it names none."""
    kind = slot_of(identifier)
    return int(kind.split("/")[0]) if "/" in kind else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--from", dest="start", type=float, default=0.0)
    parser.add_argument("--to", dest="end", type=float, default=None)
    parser.add_argument("--slot", type=int, default=None)
    parser.add_argument("--action", type=str, default=None, help="dump bodies of one action byte, in hex")
    parser.add_argument("--events", action="store_true", help="print state and pose records in time order")
    arguments = parser.parse_args()

    capture = Pcapng(arguments.capture)
    capture.read()
    datagrams = collect_datagrams(capture)
    if not datagrams:
        print("No gameplay datagrams in the capture.")
        return 1

    joiner = find_joiner(datagrams)
    endpoints = {source for _, source, _, _ in datagrams}
    if joiner is None and endpoints:
        joiner = sorted(endpoints)[0]
    host = next(
        (destination for _, source, destination, _ in datagrams if source == joiner),
        joiner,
    )
    replay = Replay(datagrams, joiner, host)
    verified = [frame for frame in replay.frames if frame.verified]
    if not verified:
        print("No frame in the capture verified: nothing can be decoded.")
        return 1

    first = verified[0].ts
    window = [
        frame
        for frame in verified
        if (frame.ts - first) >= arguments.start
        and (arguments.end is None or (frame.ts - first) <= arguments.end)
        and (arguments.slot is None
             or any(slot_number(message_type) == arguments.slot
                    for message_type, *_ in frame.messages))
    ]
    print(f"{arguments.capture}: {len(window)} verified frame(s), window "
          f"{arguments.start:.2f}s to {arguments.end if arguments.end is not None else 'end'}s")

    # Consecutive runs: a slot's state or pose, closed by a different value or a
    # gap longer than a couple of beats.
    state_runs: list[tuple[int, int, int, int, float, float]] = []
    pose_runs: list[tuple[int, int, int, int, float, float]] = []
    open_state: dict[int, tuple[int, int, int, float, float]] = {}
    open_pose: dict[int, tuple[int, int, int, float, float]] = {}
    poses: Counter[tuple[int, int]] = Counter()
    heads: Counter[int] = Counter()
    weapons: Counter[bytes] = Counter()
    damage: Counter[tuple[int, int, int | None]] = Counter()
    body_lengths: Counter[int] = Counter()
    down_bodies: list[tuple[float, int, bytes]] = []
    vitals: list[tuple[float, int, int]] = []
    wanted_action = int(arguments.action, 16) if arguments.action else None
    action_bodies: list[tuple[float, int, int, bytes]] = []
    events: list[tuple[float, int, str, bytes, bool]] = []
    run_gap = 0.5

    for frame in window:
        at = frame.ts - first
        for message_type, length, flags, body in frame.messages:
            if message_type >= 0x1000:
                continue
            slot = slot_number(message_type)
            if slot is None:
                continue
            if arguments.slot is not None and slot != arguments.slot:
                continue

            if flags == 0x01 and length in (3, 9) and body:
                action = body[0]
                if wanted_action is not None and action == wanted_action:
                    action_bodies.append((at, slot, length, bytes(body)))
                if arguments.events:
                    events.append((at, slot, "state", bytes(body), frame.source == joiner))
                opened = open_state.get(slot)
                if opened is not None and opened[0] == action and opened[1] == length \
                        and at - opened[4] <= run_gap:
                    open_state[slot] = (action, length, opened[2] + 1, opened[3], at)
                else:
                    if opened is not None:
                        state_runs.append((slot, opened[0], opened[1], opened[2], opened[3], opened[4]))
                    open_state[slot] = (action, length, 1, at, at)
            elif flags == 0x02:
                body_lengths[length] += 1
                if length == 32:
                    down_bodies.append((at, slot, bytes(body)))
                if length != 16:
                    continue
                heads[body[0]] += 1
                poses[(body[0], body[1])] += 1
                head, pose = body[0], body[1]
                if arguments.events:
                    events.append((at, slot, "pose", bytes(body[:2]), frame.source == joiner))
                opened = open_pose.get(slot)
                if opened is not None and opened[0] == head and opened[1] == pose \
                        and at - opened[4] <= run_gap:
                    open_pose[slot] = (head, pose, opened[2] + 1, opened[3], at)
                else:
                    if opened is not None:
                        pose_runs.append((slot, opened[0], opened[1], opened[2], opened[3], opened[4]))
                    open_pose[slot] = (head, pose, 1, at, at)
            elif flags == 0x04 and length == 5:
                weapons[bytes(body)] += 1
            elif flags == 0x00 and length in (15, 28):
                damage[(body[0], length, body[1] if length > 1 else None)] += 1
            elif flags == 0x03 and length == 2:
                vitals.append((at, slot, body[0]))
            elif flags == 0x05 and arguments.events:
                events.append((at, slot, "aim", bytes(body[:2]), frame.source == joiner))

    print("\nPosition body lengths (bytes, count):")
    print("  " + "  ".join(f"{length}={count}" for length, count in body_lengths.most_common()))

    print("\nDown bodies (class 0x02, thirty-two bytes):")
    if not down_bodies:
        print("  none")
    seen_slots: set[int] = set()
    for at, slot, body in down_bodies:
        if slot in seen_slots:
            continue
        seen_slots.add(slot)
        before = [(when, health) for when, where, health in vitals if where == slot and when <= at]
        health = f"vitals {before[-1][1]:3d} at {before[-1][0]:8.2f}s" if before else "no vitals yet"
        print(f"  slot {slot:2d} {at:8.2f}s  {health}  {body[:8].hex(' ')}")

    print("\nPosition heads (hex, count):")
    print("  " + "  ".join(f"{head:02x}={count}" for head, count in heads.most_common()))

    print("\nPosition head and pose pairs (hex, count):")
    print("  " + "  ".join(
        f"{head:02x} {pose:02x}={count}" for (head, pose), count in poses.most_common()
    ))

    print("\nWeapon pairs (hex, count):")
    print("  " + "  ".join(f"{pair.hex(' ')}={count}" for pair, count in weapons.most_common()))

    print("\nDamage records (subtype/len/victim slot, count):")
    print("  " + "  ".join(
        f"{subtype:02x}/{length}/{victim}={count}"
        for (subtype, length, victim), count in damage.most_common()
    ))

    for slot, (action, length, count, start, last) in open_state.items():
        state_runs.append((slot, action, length, count, start, last))
    for slot, (head, pose, count, start, last) in open_pose.items():
        pose_runs.append((slot, head, pose, count, start, last))

    print("\nState runs, in time order (slot action=hex length count start..end):")
    for slot, action, length, count, start, end in sorted(state_runs, key=lambda run: run[4]):
        print(
            f"  slot {slot:2d} action={action:02x} len={length} n={count:3d}"
            f"  {start:8.2f}..{end:8.2f}"
        )

    print("\nPose runs, in time order (slot head/pose count start..end):")
    for slot, head, pose, count, start, end in sorted(pose_runs, key=lambda run: run[4]):
        print(f"  slot {slot:2d} {head:02x} {pose:02x} n={count:3d}  {start:8.2f}..{end:8.2f}")

    if wanted_action is not None:
        print(f"\nBodies of action {wanted_action:02x}:")
        for at, slot, length, body in action_bodies[:100]:
            print(f"  slot {slot:2d} len={length} {at:8.2f}s  {body.hex(' ')}")

    if arguments.events:
        print("\nEvents, in time order (J=joiner, H=host):")
        for at, slot, kind, body, from_joiner in sorted(events, key=lambda item: item[0])[:400]:
            side = "J" if from_joiner else "H"
            if kind == "state":
                print(
                    f"  {at:8.2f}s {side} slot {slot:2d} state {body[0]:02x} len={len(body)}"
                    f" target={body[1]:02x} fire={body[2] if len(body) > 2 else 0}"
                )
            else:
                print(f"  {at:8.2f}s {side} slot {slot:2d} pose  {body[0]:02x} {body[1]:02x}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
