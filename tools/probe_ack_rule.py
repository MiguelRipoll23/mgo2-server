#!/usr/bin/env python3
"""Check the acknowledgement rule against a capture, record by record.

Usage:
    python3 tools/probe_ack_rule.py <capture.pcapng> [joiner-ip:port] [--key 0x...]

The rule, as `PeerAcknowledgementUtils` implements it: a session record whose
identifier does not already carry `0x4000` is answered by its peer with the same
identifier with `0x4000` set, the same fourth byte, and a body of one byte when
the identifier carries `0x8000` and none when it does not.

Every record of the flow that has `0x4000` set is checked against the records the
other side sent before it: the nearest unanswered record of the same identifier
minus `0x4000` and the same fourth byte. What the check reports is the share of
answers the rule explains, and every answer it cannot — a rule that holds over
one round is one round's evidence, so the exceptions are the point of running it.
"""

from __future__ import annotations

import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402

ACK_BIT = 0x4000
ONE_BYTE_CLASS = 0x8000


def main() -> None:
    path = sys.argv[1]
    joiner = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else "10.2.0.2:5730"

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

    override = int(sys.argv[sys.argv.index("--key") + 1], 16) if "--key" in sys.argv else None
    pair = next((key for key in bases if joiner in key and (len(bases[key]) == 2 or override)), None)
    if pair is None:
        print(f"{path}: no flow with a discoverable key")
        return
    sides = sorted(pair)
    key = override if override is not None else bases[pair][sides[0]] ^ bases[pair][sides[1]]

    # Records in time order, per direction, as (identifier, length, fourth byte).
    sent: dict[str, list] = defaultdict(list)
    answers = []
    for ts, local, remote, payload in datagrams:
        if frozenset((local, remote)) != pair or len(payload) < 16:
            continue
        counter, plain, verified = decode(payload, key)
        if not verified:
            counter, plain, verified = decode(payload, PRE_KEY)
            if not verified:
                continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            try:
                content = lzss_decompress(content)
            except Exception:
                pass
        for message_type, length, fourth, _body in parse_messages(content):
            if message_type < 0x1000:
                continue
            entry = (ts, message_type, length, fourth)
            if message_type & ACK_BIT:
                answers.append((local, entry))
            else:
                sent[local].append(entry)

    explained = Counter()
    exceptions = []
    for direction, (ts, message_type, length, fourth) in answers:
        wanted = (message_type & ~ACK_BIT, fourth)
        wanted_length = 1 if message_type & ONE_BYTE_CLASS else 0
        base = message_type & ~ACK_BIT
        pending = [
            entry
            for entry in sent[_other(sides, direction)]
            if entry[0] < ts and entry[1] == base and entry[3] == fourth
        ]
        if not pending:
            explained["no such record"] += 1
            exceptions.append(
                (ts, direction, message_type, length, fourth, f"nothing to answer (base 0x{base:04x})")
            )
            continue
        explained["answered a real record"] += 1
        if length == wanted_length:
            explained["body length as the rule says"] += 1
        else:
            explained["body length differs"] += 1
            exceptions.append(
                (ts, direction, message_type, length, fourth, f"expected {wanted_length} body bytes")
            )

    print(f"{path}: flow {sides[0]} <-> {sides[1]}, {len(answers)} answers")
    for direction, count in Counter(direction for direction, _ in answers).items():
        print(f"    {count:5d}  sent by {direction}")
    for label, count in explained.most_common():
        print(f"    {count:5d}  {label}")
    per_side = Counter((direction, note) for _, direction, _, _, _, note in exceptions)
    for (direction, note), count in sorted(per_side.items()):
        print(f"    {count:5d}  {direction} {note}")
    if exceptions:
        print("  exceptions:")
        for ts, direction, message_type, length, fourth, note in exceptions[:12]:
            print(
                f"    {ts:10.3f} {direction:24} 0x{message_type:04x} len={length} "
                f"4th={fourth}  {note}"
            )
    else:
        print("  no exceptions")
    print()


def _other(sides: list[str], direction: str) -> str:
    return sides[0] if direction == sides[1] else sides[1]


if __name__ == "__main__":
    main()
