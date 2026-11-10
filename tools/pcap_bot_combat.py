#!/usr/bin/env python3
"""Dump a gameplay capture's combat tick records: damage, vitals and actor state.

Usage:
    python3 tools/pcap_bot_combat.py CAPTURE.pcapng [--from SECONDS] [--to SECONDS]

The actor block is six tick records ten identifiers apart: state (class 0x01) at
the base, vitals (class 0x03) at +1, position (class 0x02) at +2, the weapon pair
and byte at +4, and aim (class 0x05) at +6. A hit lands three below the base, as
class 0x00 and fifteen bytes.

This prints, per slot: the health its vitals records carry and when it changes,
the hits it takes and the damage word they carry, and the state records' target
slot and firing byte. It is what a report of "the bots never shoot me" or "a
death shows the wrong notification" is checked against.
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


def slot_of(identifier: int) -> str:
    """The slot and block offset an identifier belongs to, as `slot/offset`."""
    for base in STATE_BASES:
        offset = identifier - base
        if 0 <= offset < SLOT_STRIDE * 24:
            return f"{offset // SLOT_STRIDE + 1}/{offset % SLOT_STRIDE}"
    return f"0x{identifier:04x}"


def _coordinate(body: bytes, offset: int) -> float:
    """A signed wire coordinate at `offset`, as stage units (a tenth each)."""
    value = body[offset] | (body[offset + 1] << 8)
    if value & 0x8000:
        value -= 0x10000
    return value * 10.0


def percentile(values: list[float], fraction: float) -> float:
    """A plain nearest-rank percentile."""
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round(fraction * (len(ordered) - 1)))))
    return ordered[index]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--from", dest="start", type=float, default=0.0)
    parser.add_argument("--to", dest="end", type=float, default=None)
    parser.add_argument(
        "--distances",
        action="store_true",
        help="also report the distance to the held target when a bot fires and when it does not",
    )
    parser.add_argument(
        "--operations",
        action="store_true",
        help="dump every operations-stream record, which is where a death is announced",
    )
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
    ]
    print(f"{arguments.capture}: {len(window)} verified frame(s), window "
          f"{arguments.start:.1f}s to {arguments.end if arguments.end is not None else 'end'}s")

    vitals: dict[int, list[tuple[float, int, int]]] = defaultdict(list)
    damage: dict[int, list[tuple[float, int, bytes]]] = defaultdict(list)
    states: dict[int, list[tuple[float, bytes]]] = defaultdict(list)
    session: Counter[int] = Counter()
    session_bodies: dict[int, list[bytes]] = defaultdict(list)
    latest_position: dict[int, tuple[float, float]] = {}
    distances: dict[int, tuple[list[float], list[float]]] = defaultdict(lambda: ([], []))
    operations: list[tuple[float, int, bytes]] = []

    for frame in window:
        for message_type, length, flags, body in frame.messages:
            if message_type < 0x1000 and flags == 0x02 and length == 16:
                slot = int(slot_of(message_type).split("/")[0])
                latest_position[slot] = (_coordinate(body, 6), _coordinate(body, 10))
                continue
            at = frame.ts - first
            if message_type >= 0x1000:
                session[message_type] += 1
                if arguments.operations and message_type & 0x7FF == 0x254:
                    operations.append((at, message_type, body))
                if len(session_bodies[message_type]) < 3 and message_type & 0xFFF8 in (0x1A58, 0x1258, 0x1A50, 0x1250):
                    session_bodies[message_type].append(body)
                continue

            if flags == 0x03 and length == 2:
                vitals[message_type].append((at, body[0], body[1]))
            elif flags == 0x00 and length == 15:
                word = body[2] | (body[3] << 8)
                damage[message_type].append((at, word, body))
            elif flags == 0x01 and length in (3, 9):
                states[message_type].append((at, body))

                # The distance the state record is written at, which is what says
                # whether a bot fires from across the stage or only on contact.
                if arguments.distances and length == 9 and body[1] != 0xFF:
                    held = latest_position.get(body[1])
                    if held is not None:
                        dx = held[0] - _coordinate(body, 3)
                        dz = held[1] - _coordinate(body, 7)
                        firing, still = distances[int(slot_of(message_type).split("/")[0])]
                        (firing if body[2] == 1 else still).append((dx * dx + dz * dz) ** 0.5)

    print("\nHits, by the identifier they land under (class 0x00, fifteen bytes):")
    if not damage:
        print("  none")
    for identifier, hits in sorted(damage.items()):
        points = [word / 128.0 for _, word, _ in hits]
        print(
            f"  {slot_of(identifier):8s} 0x{identifier:04x} hits={len(hits):4d}"
            f" points={min(points):.1f}..{max(points):.1f}"
            f" first={hits[0][0]:7.2f}s last={hits[-1][0]:7.2f}s"
            f" body={hits[0][2].hex(' ')}"
        )

    print("\nVitals, by the identifier they ride under (class 0x03, two bytes):")
    if not vitals:
        print("  none")
    for identifier, values in sorted(vitals.items()):
        healths = [health for _, health, _ in values]
        falls = [
            (values[index - 1][0], values[index - 1][1], at, health)
            for index, (at, health, _) in enumerate(values)
            if index and health != values[index - 1][1]
        ]
        print(
            f"  {slot_of(identifier):8s} 0x{identifier:04x} records={len(values):5d}"
            f" health={min(healths)}..{max(healths)} changes={len(falls)}"
        )
        for previous_at, previous, at, health in falls[:8]:
            print(f"      {previous_at:8.2f}s {previous:3d} -> {at:8.2f}s {health:3d}")

    print("\nActor state, by the identifier it rides under (class 0x01):")
    if not states:
        print("  none")
    for identifier, records in sorted(states.items()):
        targets = Counter(body[1] for _, body in records if len(body) == 9)
        firing = sum(1 for _, body in records if len(body) == 9 and body[2] == 1)
        print(
            f"  {slot_of(identifier):8s} 0x{identifier:04x} records={len(records):5d}"
            f" standing={sum(1 for _, body in records if len(body) == 3)}"
            f" firing={firing:4d} targets={dict(targets.most_common(6))}"
        )

    if arguments.operations:
        print(f"\nOperations-stream records, in order ({len(operations)}):")
        for at, identifier, body in operations[:200]:
            print(f"  {at:8.2f}s 0x{identifier:04x} {body.hex(' ')}")

    if arguments.distances:
        print("\nDistance to the held target, at the beats a bot fires and the beats it does not:")
        for slot, (firing, still) in sorted(distances.items()):
            print(
                f"  slot {slot:2d}: fired {len(firing):4d} at {percentile(firing, 0.0):6.0f}"
                f"..{percentile(firing, 1.0):6.0f} (median {percentile(firing, 0.5):6.0f})"
                f"   held {len(still):5d} at {percentile(still, 0.0):6.0f}"
                f"..{percentile(still, 1.0):6.0f} (median {percentile(still, 0.5):6.0f})"
            )

    if session:
        print("\nSession records seen (identifier: count, first bodies):")
        for identifier, count in session.most_common(12):
            bodies = session_bodies.get(identifier)
            sample = "" if not bodies else "  " + " | ".join(body.hex(" ") for body in bodies)
            print(f"  0x{identifier:04x}: {count}{sample}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
