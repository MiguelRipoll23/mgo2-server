#!/usr/bin/env python3
"""Compare a capture's actor position records: which slots write them, how often,
and where the bodies are.

Usage:
    python3 tools/pcap_bot_actors.py CAPTURE.pcapng [--from SECONDS] [--to SECONDS] [--raw N]

An actor block is six tick records ten identifiers apart, and the position one is
the record whose class byte is 0x02 (`PlayerVitalsFramesUtils`, and
`BotActorRecordUtils` in the gameplay server). This tool decodes nothing else, so
it answers the questions a bot that stands under the map or teleports raises:

- how many position records each slot gets, and at what interval;
- the coordinate range each slot's records state;
- the jump from one record to the next, for the same slot.

The coordinates are decoded at both candidate offsets, 6/8/10 and 2/4/6, because
which of them the wire uses is exactly what this decides: a body at 6/8/10 that
decodes to the stage's own bounds is a body in the map.
"""

from __future__ import annotations

import argparse
import statistics
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

POSITION_CLASS = 0x02
POSITION_BODY = 16
# Slot one's state record, in the first generation and in its sibling. The two
# sets differ by 0x800 (MatchRecordGenerationUtils), so an identifier belongs to
# whichever base leaves a block offset below one slot's stride.
STATE_BASES = (0x084D, 0x004D)
SLOT_STRIDE = 0x0A


def slot_of(identifier: int) -> str:
    """The slot and block offset an identifier belongs to, as `slot/offset`."""
    for base in STATE_BASES:
        offset = identifier - base
        if 0 <= offset < SLOT_STRIDE * 24:
            return f"{offset // SLOT_STRIDE + 1}/{offset % SLOT_STRIDE}"
    return f"0x{identifier:04x}"


def int16(body: bytes, offset: int) -> int:
    """One signed sixteen-bit value, little-endian."""
    value = body[offset] | (body[offset + 1] << 8)
    return value - 0x10000 if value & 0x8000 else value


def as_units(value: int) -> float:
    """A wire coordinate as stage units: the record carries a tenth of one."""
    return value * 10.0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--from", dest="start", type=float, default=0.0)
    parser.add_argument("--to", dest="end", type=float, default=None)
    parser.add_argument("--raw", type=int, default=0, help="raw samples per identifier")
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
    print(f"{arguments.capture}: {len(window)} verified frame(s) of {len(verified)}")
    print(f"  window {arguments.start:.2f}s to {arguments.end if arguments.end is not None else 'end'}s")

    by_direction: dict[str, int] = defaultdict(int)
    for frame in window:
        by_direction[f"{frame.source} -> {frame.destination}"] += 1
    for direction, count in sorted(by_direction.items(), key=lambda item: -item[1]):
        print(f"  {count:6d}  {direction}")

    samples: dict[tuple[str, int], list[tuple[float, bytes]]] = defaultdict(list)
    for frame in window:
        for message_type, length, flags, body in frame.messages:
            if message_type >= 0x1000 or flags != POSITION_CLASS or length != POSITION_BODY:
                continue
            samples[(frame.source, message_type)].append((frame.ts, body))

    if not samples:
        print("\nNo position record (class 0x02, sixteen bytes) in the window.")
        return 0

    print("\nPosition records by identifier:")
    print(
        "  slot/off  id     frames  interval ms  span s   x range (6/8/10)"
        "              y range              z range"
    )
    for (source, identifier), records in sorted(
        samples.items(), key=lambda item: (slot_of(item[0][1]))
    ):
        times = [ts for ts, _ in records]
        gaps = [(b - a) * 1000.0 for a, b in zip(times, times[1:])]
        xs = [as_units(int16(body, 6)) for _, body in records]
        ys = [as_units(int16(body, 8)) for _, body in records]
        zs = [as_units(int16(body, 10)) for _, body in records]
        interval = f"{statistics.median(gaps):.0f}" if gaps else "-"
        print(
            f"  {slot_of(identifier):8s} 0x{identifier:04x} {len(records):6d}  {interval:>11s}"
            f"  {times[-1] - times[0]:6.1f}"
            f"  {min(xs):8.0f}..{max(xs):8.0f}"
            f"  {min(ys):8.0f}..{max(ys):8.0f}"
            f"  {min(zs):8.0f}..{max(zs):8.0f}"
        )

    print("\nMovement per identifier, at the 6/8/10 offsets, in stage units:")
    for (source, identifier), records in sorted(
        samples.items(), key=lambda item: (slot_of(item[0][1]))
    ):
        jumps = []
        axis = {"x": [], "y": [], "z": []}
        still = 0
        for (_, previous), (_, current) in zip(records, records[1:]):
            dx = as_units(int16(current, 6)) - as_units(int16(previous, 6))
            dy = as_units(int16(current, 8)) - as_units(int16(previous, 8))
            dz = as_units(int16(current, 10)) - as_units(int16(previous, 10))
            axis["x"].append(abs(dx))
            axis["y"].append(abs(dy))
            axis["z"].append(abs(dz))
            distance = (dx * dx + dy * dy + dz * dz) ** 0.5
            if distance == 0:
                still += 1
            jumps.append(distance)
        if not jumps:
            continue
        changes = sum(1 for value in jumps if value > 0)
        print(
            f"  {slot_of(identifier):8s} steps={len(jumps)} changed={changes} still={still}"
            f" step max={max(jumps):8.0f} mean={statistics.mean(jumps):7.0f}"
            f"  axis mean x={statistics.mean(axis['x']):7.1f}"
            f" y={statistics.mean(axis['y']):7.1f} z={statistics.mean(axis['z']):7.1f}"
        )

    if arguments.raw:
        print("\nRaw position bodies:")
        for (source, identifier), records in sorted(
            samples.items(), key=lambda item: (slot_of(item[0][1]))
        ):
            for ts, body in records[: arguments.raw]:
                print(
                    f"  {slot_of(identifier):8s} 0x{identifier:04x} t={ts - first:8.3f}"
                    f" x={as_units(int16(body, 6)):8.0f} y={as_units(int16(body, 8)):7.0f}"
                    f" z={as_units(int16(body, 10)):8.0f} head={int16(body, 12) & 0xFFFF:5d}"
                    f" {body.hex(' ')}"
                )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
