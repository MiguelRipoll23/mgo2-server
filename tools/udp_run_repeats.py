#!/usr/bin/env python3
"""Report how often a capture writes the round-opening run, and what the client sent between.

Usage:
    python3 tools/udp_run_repeats.py CAPTURE.pcapng [CAPTURE.pcapng ...]

Why it exists: `MatchStartRunFramesUtils.RepeatCount` writes the run more than
once, and the question that decides the constant is whether the repeats are the
host's own or an answer to something the client did not send. `udp_start_run.py`
finds the run by its tail -- the report's answer and the seven-byte roster close
-- which every run carries; this lists every *copy* of it instead, including the
ones written without the answer, and prints what the client sent in each gap so
the copies can be read against the client's records.

A copy is an instant: the recorded host spreads one run over several datagrams at
the same moment, so frames within `CLUSTER` seconds are one copy, and the counts
printed are the records of the run that instant carries -- the state block, the
roster close, the payload and the marker.

Measured over the captures that hold a run (all of them but
`mgo2-started-on-solo-game`, which has none): a run is written one to three times
per report, one copy in most rounds and two or three in a few, and it is repeated
again two to two and a half seconds later on the briefing's tick. The client's
answers to the run's own records arrive 0.4 to 0.8 s after it, before every
repeat, and at least one repeat goes out with the client silent over the whole
gap -- so the copies are the host's own and not a retry owed to a missing
acknowledgement. The counts are the measurement behind
`docs/analysis/join-stall-after-mode2.md` §12.
"""

from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_record_semantics import (  # noqa: E402
    ACKNOWLEDGEMENT_BIT,
    is_frame_acknowledgement,
    is_record_acknowledgement,
)

# Frames within this many seconds are one instant, and one copy of the run.
CLUSTER = 0.05


def shape(message_type: int, body: bytes) -> str | None:
    """The run-signature name of a host record, or None when it is not part of a run.

    The marker is the readiness report's answer (the payload identifier with
    0x4000 set); the other records are the run's own, told apart by family,
    length and -- for the payload -- the body's first byte, so the ready set's
    publication, which answers `e0` on the same identifier, is not read as one.
    """
    family = message_type & 0x7FF
    if family == 0x2DE:
        if not body:
            return "marker"
        return "payload" if body[:1] == b"\xe0" else None
    if family == 0x254:
        if len(body) == 90:
            return "state block"
        if len(body) == 3 and body[:1] == b"\x01":
            return "round clock"
        return None
    if family in (0x001, 0x002):
        return {
            54: "close pair",
            22: "close",
            7: "roster close",
        }.get(len(body))
    if family == 0x261 and len(body) == 1:
        return "rules pair"
    return None


def copies_of(replay: Replay) -> list[tuple[float, dict[str, int]]]:
    """The host's instants that carry run-signature records: (time, {shape: count})."""
    found: list[list] = []
    for frame in replay.frames:
        if frame.source != replay.host or not frame.verified:
            continue
        for message_type, _length, _flags, body in frame.messages:
            key = shape(message_type, body)
            if key is None:
                continue
            if not found or frame.ts - found[-1][0] > CLUSTER:
                found.append([frame.ts, {}])
            found[-1][1][key] = found[-1][1].get(key, 0) + 1
    return [(ts, shapes) for ts, shapes in found]


def is_run(shapes: dict[str, int]) -> bool:
    """Whether an instant is a run: the report's answer, or the run's own tail."""
    return "marker" in shapes or ("state block" in shapes and "roster close" in shapes)


def client_between(replay: Replay, start: float, end: float) -> tuple[collections.Counter, dict]:
    """What the client sent in (start, end]: its records, and when it first answered what."""
    summary: collections.Counter = collections.Counter()
    first: dict[int, float] = {}
    for frame in replay.frames:
        if frame.source != replay.joiner or not frame.verified:
            continue
        if not start < frame.ts <= end:
            continue
        for message_type, _length, _flags, body in frame.messages:
            if is_frame_acknowledgement(message_type, body):
                summary["frame acknowledgement"] += 1
            elif is_record_acknowledgement(message_type, body):
                answered = message_type & ~ACKNOWLEDGEMENT_BIT
                summary[f"answer to 0x{answered:04x}"] += 1
                first.setdefault(answered, frame.ts)
            else:
                summary[f"0x{message_type:04x} ({len(body)} B)"] += 1
    return summary, first


def report(path: str) -> None:
    """Prints one capture's copies of the run and the client's records between them."""
    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    if joiner is None:
        print(f"== {Path(path).name}: no peer-to-peer handshake")
        return
    host = next(
        peer for _, source, peer, _ in datagrams if peer != joiner and source == joiner
    )
    replay = Replay(datagrams, joiner, host)
    base = replay.frames[0].ts if replay.frames else 0.0

    found = copies_of(replay)
    runs = [(ts, shapes) for ts, shapes in found if is_run(shapes)]
    print(
        f"== {Path(path).name}: {len(runs)} run instant(s) among {len(found)} "
        f"run-record instant(s)"
    )

    previous = None
    for index, (ts, shapes) in enumerate(runs):
        marker = "answers a report" if "marker" in shapes else "no marker"
        carried = " ".join(f"{key} x{count}" for key, count in sorted(shapes.items()))
        print(f"   run {index + 1:3} at {ts - base:9.3f}  {marker:16}  {carried}")
        gap_start = previous if previous is not None else ts - 1.0
        summary, first = client_between(replay, gap_start, ts)
        if not summary:
            print(f"        the client sent nothing in the {(ts - gap_start):.3f} s before it")
        for key, count in summary.most_common(8):
            note = ""
            if key.startswith("answer to "):
                answered = int(key.split("0x")[1], 16)
                note = f"   first {first[answered] - gap_start:.3f} s into the gap"
            print(f"        x{count:<4} {key}{note}")
        previous = ts
    print()


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return
    for path in sys.argv[1:]:
        report(path)


if __name__ == "__main__":
    main()
