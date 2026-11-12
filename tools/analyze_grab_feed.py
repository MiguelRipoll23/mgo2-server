#!/usr/bin/env python3
"""Dump a capture's roster slots, kill feed and the records of a CQC grab window.

Usage:
    python3 tools/analyze_grab_feed.py CAPTURE.pcapng [--from 391] [--to 396]

Prints, in order: every roster entry with the slot bytes it carries and the name
it holds; the operations-stream kill feed (`09 <killer> <victim>`); and, for the
window, every record of the slots named on the command line, in hex, so the
position body's own bytes can be read off against a known state.
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


def direction(frame, joiner: str) -> str:
    """Whether a frame went from the joiner to the host, or the other way."""
    source = getattr(frame, "source", None) or getattr(frame, "src", None)
    return "j>" if source == joiner else "h>"


def roster_name(body: bytes) -> str:
    """The name a roster body carries, at either layout's name offset."""
    for offset in (0x44, 0x38):
        if len(body) > offset:
            end = body.find(b"\x00", offset)
            if end > offset:
                text = body[offset:end].decode("latin-1", "replace")
                if text.isprintable():
                    return text
    return ""


def slot_of_record(identifier: int) -> int | None:
    """The slot a tick record's identifier names."""
    text = slot_of(identifier)
    return int(text.split("/")[0]) if "/" in text else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--from", dest="start", type=float, default=391.0)
    parser.add_argument("--to", dest="end", type=float, default=396.0)
    parser.add_argument("--slots", default="15,16", help="comma-separated slots to dump")
    parser.add_argument("--feed", action="store_true", help="print every feed record")
    arguments = parser.parse_args()

    capture = Pcapng(arguments.capture)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((destination for _, source, destination, _ in datagrams if source == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [frame for frame in replay.frames if frame.verified]
    if not frames:
        print("No frame verified.")
        return 1
    first = frames[0].ts
    wanted = {int(value) for value in arguments.slots.split(",")}

    rosters: list[tuple[float, bytes]] = []
    feed: list[tuple[float, int, int, bytes]] = []
    window: list[str] = []
    for frame in frames:
        at = frame.ts - first
        for message_type, length, flags, body in frame.messages:
            if message_type >= 0x1000:
                if body and body[0] == 0x07 and len(body) > 2 and body[1] == len(body) - 7:
                    rosters.append((at, body))
                if message_type & 0x7FF == 0x254 and body and body[0] == 0x09 and len(body) >= 3:
                    feed.append((at, body[1], body[2], body))
                continue
            slot = slot_of_record(message_type)
            if slot is None or slot not in wanted:
                continue
            if arguments.start <= at <= arguments.end:
                window.append(
                    f"  {at:8.2f}s {direction(frame, joiner)} slot={slot:2d}"
                    f" cls={flags:02x} len={length:2d} {body.hex(' ')}"
                )

    print(f"{arguments.capture}: {len(frames)} verified frames, {len(rosters)} roster records, "
          f"{len(feed)} feed records, first frame at {first:.3f}")

    print("\nRoster entries (bytes 4/5/6/7, id 8-11, byte 0x10, name):")
    seen: set[bytes] = set()
    for at, body in rosters:
        if body in seen or len(body) <= 0x11:
            continue
        seen.add(body)
        print(
            f"  {at:8.2f}s len={len(body):3d}"
            f" b4={body[4]:02x} b5={body[5]:02x} b6={body[6]:02x} b7={body[7]:02x}"
            f" id={body[8]:02x}{body[9]:02x}{body[10]:02x}{body[11]:02x}"
            f" b10={body[0x10]:02x} name={roster_name(body)!r}"
        )

    print(f"\nKill feed records ({len(feed)}):")
    rows = feed if arguments.feed else feed[:400]
    for at, killer, victim, body in rows:
        print(f"  {at:8.2f}s  killer={killer:2d} victim={victim:2d}  body={body.hex(' ')}")

    print(f"\nRecords of slots {sorted(wanted)} from {arguments.start:.1f}s to {arguments.end:.1f}s:")
    for line in window:
        print(line)

    print("\nPosition bodies seen in the window (slot, first two bytes, counts):")
    counts: Counter[tuple[int, str]] = Counter()
    for line in window:
        parts = line.split()
        slot = int(parts[2].split("=")[1])
        if "cls=02" not in line:
            continue
        body = parts[-1]
        counts[(slot, body[:5])] += 1
    for (slot, head), count in sorted(counts.items()):
        print(f"  slot={slot:2d} head={head} n={count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
