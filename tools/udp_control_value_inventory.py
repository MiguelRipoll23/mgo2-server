#!/usr/bin/env python3
"""Inventory the one-byte control value of the `0xd001` records in every capture.

Usage:
    python3 tools/udp_control_value_inventory.py [--ladder] [FILE...]

Why it exists: the gameplay host writes that byte as the phase of the pass
it answers in (`Rooms/PeerControlByteService`), and the client hands it to the
script event `0x16002C`. What each value *means* is not in the image
(`docs/analysis/join-stall-after-mode2.md` §30.7, §57, §58), so the only
evidence there is, is the set of values the two peers write — which is what
this prints, once for every capture and once over all of them.

Both directions matter: a client answers the host's own payload-class
records with the same `0xd001` and a byte of its own, and the codes it uses
are largely different ones, so a host-only count under-reads the alphabet.

The byte is read as `v & 0x1f` code and `v & 0xe0` flags, the reading §58
records: over both directions the code covers 31 of 32 values and the flag
bits appear in every combination, so a name for a code has to come from the
reference host rather than from a count.

`--ladder` adds, per capture, the answers in time order against the screen
step each one leaves behind. That column is **derived and not measured**: no
frame carries the client's screen state, so the step is walked from the rule
the two readers in the image implement, and a run of answers that changes
nothing says only that those values cannot have differed *for that screen*.
"""

from __future__ import annotations

import glob
import os
import sys
from collections import Counter

from udp_session_records import collect, key_for, parse
from udp_frame import decode_frame, lzss_decompress

CONTROL_RECORD = 0xD001

# The reader in the image (`0x77E6A0`, `0x77F878`): the record's handler writes the
# byte to state 0x00 byte 99 (`0x26DB78`), the screen copies it to `+0xd0`, returns
# on 0xff, returns at step 12, and otherwise moves `+0xb8` to 12 and raises
# `0x16002C` with the byte.
NO_VALUE = 0xFF
ADVANCED_STEP = 12
LAST_ADVANCING_STEP = 9


def control_values(path: str) -> tuple[Counter, Counter]:
    """The control byte of both sides' answers in one capture, counted per side."""
    datagrams, bases, joiners, origins = collect(path)
    host: Counter = Counter()
    client: Counter = Counter()
    for _, local, remote, payload in datagrams:
        counter, plain, verified, _ = decode_frame(payload, key_for(bases, local, remote))
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        joiner = joiners.get(frozenset((local, remote)))
        side = client if joiner is not None and local == joiner else host
        for message_type, _, _, body in parse(content):
            if message_type == CONTROL_RECORD and len(body) == 1:
                side[body[0]] += 1
    return host, client


def control_timeline(path: str) -> list[tuple[float, str, int]]:
    """Every answer of a capture in time order: seconds from its flow's start, side, byte."""
    datagrams, bases, joiners, origins = collect(path)
    rows: list[tuple[float, str, int]] = []
    for timestamp, local, remote, payload in datagrams:
        counter, plain, verified, _ = decode_frame(payload, key_for(bases, local, remote))
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        joiner = joiners.get(frozenset((local, remote)))
        side = "client" if joiner is not None and local == joiner else "host"
        origin = origins.get(frozenset((local, remote)), 0.0)
        for message_type, _, _, body in parse(content):
            if message_type == CONTROL_RECORD and len(body) == 1:
                rows.append((timestamp - origin, side, body[0]))
    rows.sort(key=lambda row: row[0])
    return rows


def step_after(step: int, value: int) -> tuple[int, str]:
    """The screen step the reader above leaves behind, and which branch it took."""
    if value == NO_VALUE:
        return step, "held: 0xff is the no-value"
    if step >= ADVANCED_STEP:
        return step, "no change: the screen is already at 12"
    if step <= LAST_ADVANCING_STEP:
        return ADVANCED_STEP, f"advanced {step} -> {ADVANCED_STEP}"
    return step, f"not advanced: the screen is at {step}, past 9"


def print_ladder(path: str) -> None:
    """Print one capture's answers as runs, against the step each one leaves.

    The byte is written by the answering side and consumed by the peer that
    receives it (`0x77E6A0`, `0x77F878` run in the receiving screen), so the step
    tracked here is the receiver's. A capture whose room has more than one joiner
    is read as one screen per side, which is the coarsest thing this can say.
    """
    rows = control_timeline(path)
    if not rows:
        return
    print(f"\n{os.path.basename(path)}: {len(rows)} answer(s)")
    steps = {"host": 0, "client": 0}
    index = 0
    while index < len(rows):
        side, value = rows[index][1], rows[index][2]
        run = [rows[index]]
        index += 1
        while index < len(rows) and rows[index][1] == side and rows[index][2] == value:
            run.append(rows[index])
            index += 1
        receiver = "client" if side == "host" else "host"
        before = steps[receiver]
        after, outcome = step_after(before, value)
        steps[receiver] = after
        print(
            f"  t+{run[0][0]:9.3f}  {side}->{receiver} {value:02x} x{len(run):3d}  "
            f"screen {before:2d} -> {after:2d}  {outcome}"
        )


def summarise(total: Counter, label: str) -> None:
    """Print the code and flag reading of one side's values, or of both."""
    codes = {value & 0x1F for value in total}
    flags: Counter = Counter()
    for value, count in total.items():
        if value & 0xE0:
            flags[value & 0xE0] += count
    absent = " ".join(f"{code:02x}" for code in range(32) if code not in codes) or "none"
    print(f"{label}: {sum(total.values())} records, {len(total)} distinct, "
          f"{len(codes)} of 32 codes, absent {absent}")
    if flags:
        print("  flags: " + " ".join(f"{flag:02x}x{count}" for flag, count in sorted(flags.items())))
    print(f"  no-value 0xff: {total.get(NO_VALUE, 0)}")


def report(captures: list[str], ladder: bool) -> int:
    host_total: Counter = Counter()
    client_total: Counter = Counter()
    for path in captures:
        host, client = control_values(path)
        host_total.update(host)
        client_total.update(client)
        distinct = len(set(host) | set(client))
        print(f"{os.path.basename(path):<62} host={sum(host.values()):4d} "
              f"client={sum(client.values()):3d}  distinct={distinct:2d}")

    both: Counter = Counter()
    both.update(host_total)
    both.update(client_total)
    print()
    summarise(host_total, "host  ")
    summarise(client_total, "client")
    summarise(both, "both  ")
    print("  bytes: " + " ".join(f"{value:02x}" for value in sorted(both)))

    if ladder:
        for path in captures:
            print_ladder(path)
    return 0


def main(argv: list[str]) -> int:
    flags = {argument for argument in argv[1:] if argument.startswith("-")}
    captures = [argument for argument in argv[1:] if not argument.startswith("-")]
    if not captures:
        captures = sorted(glob.glob("*.pcapng"))
    if not captures:
        print(__doc__)
        return 2
    return report(captures, bool(flags & {"-l", "--ladder"}))


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
