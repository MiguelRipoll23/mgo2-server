#!/usr/bin/env python3
"""Set a live join's records beside the reference join's, frame by frame.

Usage:
    python3 tools/join_compare.py <live-log> <reference.pcapng> [--joiner 10.2.0.2:5730]

The reference capture and the live server log carry the same thing -- one frame
per datagram, in order, with its counter and its records -- so the two joins can
be read side by side from the handshake to the point where one of them stops
moving. The live side is the server's own log of the datagram as it arrived or
left, so it shows exactly what the peer got, not what the server meant to send.

Columns are `t` (seconds from the first datagram of the flow), direction from the
host's point of view (`H>` is the host sending, `<J` the joiner), the frame
counter, and one entry per record as `type:length:fourth`.
"""

from __future__ import annotations

import re
import struct
import sys
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402

JOINER = "10.2.0.2:5730"

LINE = re.compile(
    r"(IN|OUT) frame counter=(\d+)"
    r"(?: key=([0-9a-f]+))?"
    r"(?: handshake)?"
    r"(?: messages=\d+)?"
    r"(?: pre-keyed)?"
    r" (?:from|to) ([\d.]+:\d+) \((\d+) bytes\): ([0-9a-f ]+)$"
)

CLOCK = re.compile(r"^(\d{4}-\d\d-\d\dT[\d:.]+[+-]\d\d:\d\d)")


def records_of(content: bytes, counter: int) -> list[tuple[int, int, int]]:
    if counter & 0x8000:
        content = lzss_decompress(content)
    return [(tp, ln, fl) for tp, ln, fl, _ in parse_messages(content)]


def live_side(path: str) -> list[tuple[float, str, int, list]]:
    """Decode the server log's own frame dumps into the same shape as a capture."""
    frames = []
    key = None
    for line in open(path, encoding="utf-8", errors="replace"):
        match = LINE.search(line.rstrip("\r\n"))
        if not match:
            continue
        direction, counter, seen_key, _, size, wire = match.groups()
        if seen_key:
            key = int(seen_key, 16)
        wire_bytes = bytes.fromhex(wire)
        if len(wire_bytes) != int(size):
            continue
        counter_value = int(counter)
        if direction == "IN":
            # The log prints the inbound frame after the codec has taken the
            # scramble and the chain back off it, so the bytes are the plain
            # datagram already. An outbound line prints the frame the codec
            # produced, which is the scrambled wire form.
            plain = wire_bytes
        else:
            counter_value, plain, _ = decode(wire_bytes, key if key is not None else PRE_KEY)
        content = plain[2 : len(plain) - 0xA]
        frames.append((_clock(line), direction == "OUT", counter_value, records_of(content, counter_value)))

    if not frames:
        return []
    start = frames[0][0]
    return [
        (round(moment - start, 3), "H>" if outgoing else "<J", counter, records)
        for moment, outgoing, counter, records in frames
    ]


def _clock(line: str) -> float:
    stamp = CLOCK.search(line)
    return datetime.fromisoformat(stamp.group(1)).timestamp()


def capture_side(path: str, joiner: str) -> list[tuple[float, str, int, list]]:
    capture = Pcapng(path)
    capture.read()
    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP or 3478 in (source_port, destination_port):
            continue
        datagrams.append((ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload))

    bases: dict[frozenset, dict[str, int]] = defaultdict(dict)
    for _, local, remote, payload in datagrams:
        if len(payload) < 22:
            continue
        _, plain, verified = decode(payload, PRE_KEY)
        if not verified or struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[frozenset((local, remote))][local] = struct.unpack_from("<II", plain, 6)[1]

    chosen = None
    host = None
    for pair, sides in bases.items():
        if joiner not in pair or len(sides) != 2:
            continue
        chosen = pair
        break
    if chosen is None:
        return []
    host = next(side for side in chosen if side != joiner)
    key = bases[chosen][joiner] ^ bases[chosen][host]

    frames = []
    start = None
    for ts, local, remote, payload in datagrams:
        if frozenset((local, remote)) != chosen or len(payload) < 16:
            continue
        counter, plain, verified = decode(payload, key)
        if not verified:
            counter, plain, verified = decode(payload, PRE_KEY)
            if not verified:
                continue
        if start is None:
            start = ts
        content = plain[2 : len(plain) - 0xA]
        frames.append(
            (
                round(ts - start, 3),
                "H>" if local == host else "<J",
                counter,
                records_of(content, counter),
            )
        )
    return frames


def render(name: str, frames: list, until: float, width: int) -> list[str]:
    lines = [f"--- {name}: {len(frames)} frames" + (f", showing t<{until}s" if until else "")]
    shown = 0
    for rel, direction, counter, records in frames:
        if until and rel > until:
            break
        listed = " ".join(f"{tp:04x}:{ln}:{fl}" for tp, ln, fl in records) or "(none)"
        lines.append(f"  {rel:9.3f} {direction} 0x{counter:04x} {listed[:width]}")
        shown += 1
    lines.append("")
    return lines


def inventory(name: str, frames: list) -> list[str]:
    lines = [f"--- {name}: session records in order (type, length, fourth)"]
    by_direction = defaultdict(list)
    for _, direction, _, records in frames:
        for tp, ln, fl in records:
            by_direction[direction].append((tp, ln, fl))
    for direction in ("H>", "<J"):
        rows = by_direction[direction]
        counts = Counter(tp for tp, _, _ in rows)
        lines.append(f"  {direction} total={len(rows)} types={dict(sorted(counts.items()))}")
        lines.append(f"    first 24: {[(hex(tp), ln, fl) for tp, ln, fl in rows[:24]]}")
    lines.append("")
    return lines


def main() -> None:
    live_path, reference_path = sys.argv[1], sys.argv[2]
    joiner = sys.argv[sys.argv.index("--joiner") + 1] if "--joiner" in sys.argv else JOINER
    until = float(sys.argv[sys.argv.index("--until") + 1]) if "--until" in sys.argv else 14.0

    live = live_side(live_path)
    reference = capture_side(reference_path, joiner)

    for name, frames in (("REFERENCE " + reference_path, reference), ("LIVE " + live_path, live)):
        for line in render(name, frames, until, 150):
            print(line)
        for line in inventory(name, frames):
            print(line)

    print("=== the two joins, frame against frame ===")
    print(f"{'t':>9} {'REFERENCE':<62} {'t':>9} {'LIVE':<62}")
    for index in range(max(len(reference), len(live))):
        left = reference[index] if index < len(reference) else None
        right = live[index] if index < len(live) else None
        if left is None and right is None:
            break
        if left and left[0] > until and (right is None or right[0] > until):
            break
        def cell(frame):
            if frame is None:
                return f"{'-':>9} {'':<68}"
            rel, direction, counter, records = frame
            listed = " ".join(f"{tp:04x}:{ln}:{fl}" for tp, ln, fl in records) or "(none)"
            return f"{rel:9.3f} {direction} {counter:04x} {listed}"[:78].ljust(78)
        print(f"{cell(left)} {cell(right)}")


if __name__ == "__main__":
    main()
