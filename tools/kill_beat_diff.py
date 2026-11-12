#!/usr/bin/env python3
"""Print the host records around one kill feed, side by side, so two captures diff.

    python3 tools/kill_beat_diff.py CAPTURE.pcapng [--feed N] [--back 1.0] [--after 0.6]

Every host record of the beat is printed with the slot and block offset its
identifier names, its class byte and its body as hex. That is the sequence the
client reads to learn that a death happened, and the only place a missing record
or a wrong byte shows.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_bot_combat import slot_of  # noqa: E402
from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402

CLASS_NAMES = {0x00: "damage", 0x01: "state", 0x02: "posn", 0x03: "vitals",
               0x04: "weapon", 0x05: "aim", 0x06: "weapon"}


def main() -> int:
    path = sys.argv[1]
    index = int(sys.argv[sys.argv.index("--feed") + 1]) if "--feed" in sys.argv else 0
    back = float(sys.argv[sys.argv.index("--back") + 1]) if "--back" in sys.argv else 1.0
    after = float(sys.argv[sys.argv.index("--after") + 1]) if "--after" in sys.argv else 0.6

    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((d for _, s, d, _ in datagrams if s == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [f for f in replay.frames if f.verified]
    first = frames[0].ts

    feeds = [
        (f.ts, b[1], b[2])
        for f in frames
        if f.source == host
        for t, _l, _fl, b in f.messages
        if t & 0x7FF == 0x254 and b and b[0] == 0x09 and len(b) == 3
    ]
    at, killer, victim = feeds[index]
    print(f"{path}: feed {index} at t+{at - first:.2f} killer={killer} victim={victim} "
          f"of {len(feeds)}")
    print(f"host records from t+{at - first - back:.2f} to t+{at - first + after:.2f}:\n")

    for frame in frames:
        when = frame.ts - first
        if frame.source != host or not (when >= at - first - back and when <= at - first + after):
            continue
        for message_type, length, flags, body in frame.messages:
            if message_type >= 0x1000:
                continue
            where = slot_of(message_type)
            name = CLASS_NAMES.get(flags, f"0x{flags:02x}")
            mark = ""
            if flags == 0x00 and len(body) == 15 and body[0] == 0x09:
                mark = "  <== kill feed" if body[1] == killer and body[2] == victim else "  <== feed"
            if flags == 0x03 and body and body[0] == 0:
                mark += "  <== health zero"
            print(f"  {when:8.2f}s {where:8s} cls={name:6s} len={length:2d} {body.hex(' ')}{mark}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
