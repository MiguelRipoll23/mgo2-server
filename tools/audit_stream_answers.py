#!/usr/bin/env python3
"""Compare, per identifier and direction, what a host wrote with what its client answered.

Usage:
    python3 tools/audit_stream_answers.py CAPTURE.pcapng [CAPTURE.pcapng ...]

Why it exists: an answer repeats the fourth byte of the record the peer took
(`decode_p2p_datagram`'s `0x4000` branch walks that stream's sent entries and marks
the one whose byte the answer carries), so the client's answers are its own record of
what it accepted. A record written and never answered is one the client dropped, and
the first such byte in wire order is where the stream stopped.

The listing is per exact identifier and not per stream, because the `0x4000` bit is
set on records that answer and on the host's own class-d control records alike, so
only the direction separates a write from an answer.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from udp_ordinal_rules import records_of  # noqa: E402


def collect(path: str):
    """Every record of one capture, as (direction, identifier, byte, time)."""
    return [
        (direction, message_type, flags, at)
        for at, direction, message_type, flags, _body in records_of(path)
    ]


def report(path: str) -> None:
    records = collect(path)
    written: dict[int, list[int]] = {}
    answered: dict[int, list[int]] = {}
    for direction, message_type, flags, _at in records:
        if direction == "host":
            written.setdefault(message_type, []).append(flags)
        else:
            answered.setdefault(message_type, []).append(flags)

    print(f"== {Path(path).name}")
    print(f"{'host id':>8} {'wrote':>6}  first bytes")
    for message_type in sorted(written):
        values = written[message_type]
        print(f"  0x{message_type:04x} {len(values):6d}  " + " ".join(f"{v:02x}" for v in values[:12]))
    print(f"{'client id':>8} {'answered':>9}  first bytes")
    for message_type in sorted(answered):
        values = answered[message_type]
        print(f"  0x{message_type:04x} {len(values):9d}  " + " ".join(f"{v:02x}" for v in values[:12]))
    print()


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return
    for path in sys.argv[1:]:
        report(path)


if __name__ == "__main__":
    main()
