#!/usr/bin/env python3
"""Inventory the one-byte control value of the `0xd001` records in every capture.

Usage:
    python3 tools/udp_control_value_inventory.py [FILE...]

Why it exists: the gameplay host writes that byte as the phase of the pass
it answers in (`PeerAcknowledgementUtils`), and the client hands it to the
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
"""

from __future__ import annotations

import glob
import os
import sys
from collections import Counter

from udp_session_records import collect, key_for, parse
from udp_frame import decode_frame, lzss_decompress

CONTROL_RECORD = 0xD001


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


def report(captures: list[str]) -> int:
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
    return 0


def main(argv: list[str]) -> int:
    captures = argv[1:] or sorted(glob.glob("*.pcapng"))
    if not captures:
        print(__doc__)
        return 2
    return report(captures)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
