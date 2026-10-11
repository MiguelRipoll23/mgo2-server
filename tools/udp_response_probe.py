#!/usr/bin/env python3
"""Measure which outbound records a captured host sends only in response to a client.

Usage:
    python3 tools/udp_response_probe.py CAPTURE.pcapng [--window-ms N] [--joiner IP:PORT]

The gameplay server replays the recorded host's data phase on a clock. The
capture is not a clock: some of those records are the host's answer to a record
the client just sent, and delivering them on a schedule puts a round-start run
in front of a client that has not reported itself ready. This tool measures the
pairing rather than assuming it.

For every outbound record type it reports how long the host had been quiet - how
long since any inbound datagram - before each occurrence, and the inbound types
that immediately preceded it. A type whose *maximum* gap is inside the window
never went out unprompted; a type whose minimum gap is outside it did.

Nothing here is inferred from a coincidence of counts: a pairing is reported only
with the share of the type's occurrences that the pairing explains.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402


def build_records(replay: Replay):
    """Every verified record of the flow, tagged with its direction and time.

    Returns a time-ordered list of dicts so a caller can ask both "what preceded
    this outbound record" and "what followed this inbound one" under one clock.
    """
    records = []
    for frame in replay.frames:
        if not frame.verified:
            continue
        direction = "IN" if frame.source == replay.joiner else "OUT"
        for message_type, length, flags, body in frame.messages:
            records.append(
                {
                    "ts": frame.ts,
                    "direction": direction,
                    "type": message_type,
                    "length": length,
                    "flags": flags,
                    "body": body,
                }
            )
    return records


def last_inbound_index(records, index):
    """Index of the most recent inbound record strictly before `index`, or None."""
    for candidate in range(index - 1, -1, -1):
        if records[candidate]["direction"] == "IN":
            return candidate
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--window-ms", type=int, default=250)
    parser.add_argument("--joiner", default=None, help="Override the joiner endpoint")
    parser.add_argument("--min-count", type=int, default=1)
    parser.add_argument(
        "--session-only",
        action="store_true",
        help="Ignore in-game tick records (id < 0x1000) in both directions",
    )
    args = parser.parse_args()
    session_only = args.session_only

    def keep(message_type: int) -> bool:
        return not session_only or message_type >= 0x1000

    capture = Pcapng(args.capture)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = args.joiner or find_joiner(datagrams)
    if joiner is None:
        print(f"{args.capture}: no handshake found; cannot tell the sides apart")
        return
    host = next(
        (
            remote
            for _, source, remote, _ in datagrams
            if source == joiner and remote != joiner
        ),
        None,
    )
    if host is None:
        print(f"{args.capture}: no peer of {joiner} found")
        return

    replay = Replay(datagrams, joiner, host)
    if replay.key is None:
        print(f"{args.capture}: the flow holds only one handshake; no session key")
        return

    records = [record for record in build_records(replay) if keep(record["type"])]
    window = args.window_ms / 1000.0

    print(
        f"{args.capture}: joiner {joiner} host {host} key=0x{replay.key:08x} "
        f"{len(records)} records"
    )
    print(f"window {args.window_ms} ms\n")

    # For each outbound type: the gap to the previous inbound record, and which
    # inbound type most often preceded it.
    by_type = defaultdict(list)
    for index, record in enumerate(records):
        if record["direction"] != "OUT":
            continue
        previous = last_inbound_index(records, index)
        gap = None if previous is None else record["ts"] - records[previous]["ts"]
        by_type[record["type"]].append((gap, previous, index))

    print("=== outbound record types: how long the host had been quiet ===")
    print(f"{'type':>8} {'count':>7} {'<=win':>7} {'minms':>9} {'medms':>9} {'maxms':>9}  preceded-by")
    rows = []
    for message_type in sorted(by_type):
        entries = by_type[message_type]
        if len(entries) < args.min_count:
            continue
        gaps = [gap for gap, _, _ in entries if gap is not None]
        inside = sum(1 for gap in gaps if gap <= window)
        ordered = sorted(gaps)
        median = ordered[len(ordered) // 2] * 1000 if ordered else float("nan")
        preceding = Counter(
            records[previous]["type"] for _, previous, _ in entries if previous is not None
        )
        top = ", ".join(f"0x{value:04x}x{count}" for value, count in preceding.most_common(3))
        rows.append(
            (
                bool(gaps) and inside == len(gaps),
                message_type,
                len(entries),
                inside,
                min(gaps) * 1000 if gaps else float("nan"),
                median,
                max(gaps) * 1000 if gaps else float("nan"),
                top,
            )
        )

    # Response-only first: the types that were never sent after a longer silence.
    rows.sort(key=lambda row: (-row[0], -row[3] / max(row[2], 1)))
    for always, message_type, count, inside, minimum, median, maximum, top in rows:
        tag = "ALWAYS" if always else "      "
        print(
            f"{tag} 0x{message_type:04x} {count:7d} {inside:7d} "
            f"{minimum:9.1f} {median:9.1f} {maximum:9.1f}  {top}"
        )

    # The pairing that matters: for each outbound type, what share of its
    # occurrences the most common preceding inbound type explains, and how many
    # of that inbound type were followed by it.
    print("\n=== strongest inbound -> outbound pairings (>= 90% of the outbound type) ===")
    print(f"{'in':>8} {'out':>8} {'out n':>7} {'in->out':>8} {'out<-#in':>9} {'med gap ms':>11}")
    # One pass: between an inbound record and the next one, which outbound types
    # the host sent. Counted per inbound occurrence, so "every one of these was
    # followed by that" is a count of occurrences and not of records.
    inbound_followers = defaultdict(Counter)
    inbound_counts = Counter()
    pending_type = None
    seen = set()
    for record in records:
        if record["direction"] == "IN":
            if pending_type is not None:
                for followed in seen:
                    inbound_followers[pending_type][followed] += 1
            pending_type = record["type"]
            inbound_counts[pending_type] += 1
            seen = set()
        elif pending_type is not None:
            seen.add(record["type"])
    if pending_type is not None:
        for followed in seen:
            inbound_followers[pending_type][followed] += 1

    for message_type in sorted(by_type):
        entries = by_type[message_type]
        preceding = Counter(
            records[previous]["type"] for _, previous, _ in entries if previous is not None
        )
        if not preceding:
            continue
        inbound_type, matched = preceding.most_common(1)[0]
        share = matched / len(entries)
        if share < 0.9:
            continue
        followed = inbound_followers.get(inbound_type, Counter()).get(message_type, 0)
        gaps = [
            gap
            for gap, previous, _ in entries
            if previous is not None and records[previous]["type"] == inbound_type
        ]
        median = sorted(gaps)[len(gaps) // 2] * 1000 if gaps else float("nan")
        print(
            f"0x{inbound_type:04x} 0x{message_type:04x} {len(entries):7d} {share * 100:7.1f}% "
            f"{followed:7d}/{inbound_counts[inbound_type]:<4d} {median:11.1f}"
        )


if __name__ == "__main__":
    main()
