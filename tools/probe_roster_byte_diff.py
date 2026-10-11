#!/usr/bin/env python3
"""Diff a live roster against a captured one, offset by offset.

Usage:
    python3 tools/probe_roster_byte_diff.py <live-log> <reference.pcapng>

`probe_join_diff.py` prints both roster entries in full, which shows the fields
but leaves the reader to find them. This prints only where they disagree, as runs
of differing offsets, which is the form that answers "is anything here ours to
fix, or is it all our own name and address".

The two sides carry the same records, so they are matched by record type and
length rather than by position. The roster run is the first keyed host frame
carrying the empty `0x5001` head, together with the two frames after it -- the
recorded host sends the run, a repeat of it, and a trailer.

Our side is read from the server's log, which prints an inbound frame after the
codec has taken the scramble and the chain off it and an outbound frame as the
scrambled wire form. `docs/protocol/UDP_GAME_CAPTURE.md` is where a finding
belongs; this is the tool that produces one.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402

# The empty record the recorded host opens its roster with.
ROSTER_HEAD = 0x5001

# One `IN`/`OUT` frame dump. The inbound form is a plaintext datagram, so the
# counter is the logged one; the outbound form is wire bytes and is unscrambled.
INBOUND = re.compile(
    r"IN frame counter=(\d+)"
    r"(?: key=([0-9a-f]+))?"
    r"(?: handshake)?"
    r"(?: messages=\d+)?"
    r" compression=\w+ from \S+ \((\d+) bytes\): ([0-9a-f ]+)$"
)
OUTBOUND = re.compile(
    r"OUT frame counter=(\d+) messages=\d+( pre-keyed)?"
    r" compression=\w+ to \S+ \((\d+) bytes\): ([0-9a-f ]+)$"
)


def records_from_plain(plain: bytes, counter: int) -> list[tuple[int, bytes]]:
    """Records of a datagram that is already decrypted and unchained."""
    content = plain[2 : len(plain) - 0xA]
    if counter & 0x8000:
        content = lzss_decompress(content)
    return [(message_type, body) for message_type, _, _, body in parse_messages(content)]


def live_roster(path: str) -> list[tuple[int, bytes]]:
    """Roster records out of the server log, in the order they were sent."""
    key: int | None = None
    frames: list[list[tuple[int, bytes]]] = []

    for line in open(path, encoding="utf-8", errors="replace"):
        line = line.rstrip("\r\n")

        # The session key is only ever printed on an inbound line, and the
        # roster is only ever sent, so an inbound line is read for the key and
        # for nothing else.
        inbound = INBOUND.search(line)
        if inbound:
            if inbound.group(2):
                key = int(inbound.group(2), 16)
            continue

        match = OUTBOUND.search(line)
        if not match:
            continue
        counter, pre_keyed, size, wire = match.groups()
        raw = bytes.fromhex(wire)
        if len(raw) != int(size):
            continue
        if not pre_keyed:
            if key is None:
                continue  # a keyed frame before the log has shown us a key
            counter_value, plain, _ = decode(raw, key)
        else:
            counter_value, plain, _ = decode(raw, PRE_KEY)
        frames.append(records_from_plain(plain, counter_value))

    start = next(
        (i for i, frame in enumerate(frames) if any(t == ROSTER_HEAD and not b for t, b in frame)),
        None,
    )
    if start is None:
        return []

    run: list[tuple[int, bytes]] = []
    for frame in frames[start : start + 3]:
        run += [record for record in frame if record[1]]
    return run


def captured_roster(path: str) -> list[tuple[int, bytes]]:
    """Roster records out of a capture, taken from the recorded host."""
    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next(p for _, s, p, _ in datagrams if p != joiner and s == joiner)

    frames = [f for f in Replay(datagrams, joiner, host).frames if f.source == host]
    start = next(
        (i for i, f in enumerate(frames) if any(t == ROSTER_HEAD and not b for t, l, _, b in f.messages)),
        None,
    )
    if start is None:
        return []

    run: list[tuple[int, bytes]] = []
    for frame in frames[start : start + 3]:
        run += [(t, b) for t, l, _, b in frame.messages if b]
    return run


def runs_of_differences(left: bytes, right: bytes) -> list[tuple[int, int]]:
    """Differing offsets of two bodies, collapsed into contiguous runs."""
    runs: list[list[int]] = []
    for offset in range(max(len(left), len(right))):
        a = left[offset] if offset < len(left) else None
        b = right[offset] if offset < len(right) else None
        if a == b:
            continue
        if runs and runs[-1][-1] == offset - 1:
            runs[-1].append(offset)
        else:
            runs.append([offset])
    return [(r[0], r[-1]) for r in runs]


def show(lo: int, hi: int, body: bytes) -> str:
    cells = " ".join(f"{body[i]:02x}" if i < len(body) else "--" for i in range(lo, min(hi + 1, lo + 8)))
    return f"{cells:<26} ({len(body)} bytes)"


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2

    ours = live_roster(sys.argv[1])
    theirs = captured_roster(sys.argv[2])
    if not ours or not theirs:
        print("no roster run found on one side")
        return 1

    print(f"ours: {len(ours)} records, theirs: {len(theirs)}")
    for (our_type, our_body), (their_type, their_body) in zip(ours, theirs):
        if our_type != their_type:
            print(f"\ntype mismatch: ours 0x{our_type:04x}, theirs 0x{their_type:04x}")
            continue
        runs = runs_of_differences(their_body, our_body)
        print(f"\n0x{our_type:04x}  theirs {show(0, 0, their_body)}  ours {show(0, 0, our_body)}")
        if not runs:
            print("  identical")
        for lo, hi in runs:
            print(f"  [{lo:#04x}..{hi:#04x}]  theirs {show(lo, hi, their_body)}  ours {show(lo, hi, our_body)}")
        print(f"  {sum(hi - lo + 1 for lo, hi in runs)} of {max(len(their_body), len(our_body))} bytes differ")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())