#!/usr/bin/env python3
"""Check the round's opening run against a capture, record for record.

Usage:
    python3 tools/udp_start_run.py CAPTURE.pcapng

Decompresses the host's round-open frame and prints the run's records in the
order the host wrote them, then compares that order against the shape
`MatchStartRunFramesUtils.BuildRun` builds.

The comparison is by **role**, not by identifier: the team array travels as
`0x9001` in `mgo2-game1.pcapng` and as `0x1001` in
`mgo2-game-started-on-briefing.pcapng`, and the payload is six bytes in one and
eight in the other, while the roles those records play are the same thirteen in
both. A role is read from the identifier's low eleven bits, the body length and —
where a family carries more than one record — the body's first byte.

The run is the thirteen roles a round opens with, and the marker is a record
beside them: `mgo2-game1` writes the marker as a datagram of its own and the
thirteen behind it, and `mgo2-game-started-on-briefing` writes the marker inside
the same frame. **The two captures do not agree on the order**, and the tool
reports that rather than hiding it: `mgo2-game1` writes the payload near the end
and the rules pair after it, and the two briefing captures write the payload
right behind the roster heads and no rules pair at all. The record *set* is the
same; the order is the host's, so the code follows `mgo2-game1`, the dedicated
host this project implements. The frame is found by the two records only a run's tail carries —
the answer to the report and the seven-byte roster close — so a briefing
publication, which also answers `e0`, is not read as a run.

The session key is derived from the two handshake counter bases, as everywhere
else in `tools/`.
"""

from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    decode_frame,
    handshake_identity_of,
    lzss_decompress,
    parse_messages,
)

# The shape BuildRun writes, as the roles it writes them in. The identifier and
# length beside each are the first round's own, which is what the code emits;
# the comparison itself is by label.
CODE_SHAPE = [
    ("marker", 0x5ADE, 0),
    ("roster head", 0x5001, 0),
    ("roster head", 0x5001, 0),
    ("state block", 0x1A54, 90),
    ("operations short", 0x1A54, 2),
    ("team array", 0x9001, 26),
    ("occupancy array", 0x1001, 26),
    ("presence answer", 0x0A5C, 4),
    ("close", 0x1001, 20),
    ("close", 0x1001, 20),
    ("roster close", 0x1001, 7),
    ("round clock", 0x1A54, 3),
    ("payload", 0x1ADE, 6),
    ("rules", 0x0A61, 1),
    ("rules", 0x0A61, 1),
]

# Roles this host writes inside the run that the run's *shape* does not count:
# the presence answer. Both captures write it beside the report, within half a
# second of it, and `mgo2-game-started-on-briefing` also writes it twice inside
# its round-open frame — which is the position `DATA_PHASE_DELIVERY.md` measures
# and this host keeps.
HOST_ADDITIONS = {"presence answer"}

# Stream families whose records are the live round's own rather than the run's.
WORLD_OBJECT_FAMILIES = {0x250, 0x25A, 0x256, 0x257, 0x258}


def role(identifier: int, length: int, body: bytes) -> str | None:
    """What a record of an opening frame is, or None when it is not the run.

    The low eleven bits of an identifier name a stream family; within a family
    the length and, where needed, the body's first byte name the record.
    """
    family = identifier & 0x7FF
    if family == 0x001:
        if length == 0:
            return "roster head"
        if length == 26 and body[:1] == b"\x85":
            return "team array"
        if length == 26 and body[:1] == b"\x86":
            return "occupancy array"
        if length == 20:
            return "close"
        if length == 7:
            return "roster close"
        return None
    if family == 0x254:
        if length == 90:
            return "state block"
        if length == 2:
            return "operations short"
        if length == 3:
            return "round clock"
        return None
    if family == 0x2DE:
        if length == 0:
            return None  # the marker, a record beside the run
        return "payload" if body[:1] == b"\xe0" else None
    if family == 0x25C and length == 4:
        return "presence answer"
    if family == 0x261 and length == 1:
        return "rules"
    return None


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    capture = Pcapng(sys.argv[1])
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        datagrams.append(
            (ts, f"{source}:{source_port}", f"{destination}:{destination_port}", bytes(payload))
        )

    if not datagrams:
        raise SystemExit("capture holds no UDP datagrams")

    flows = Counter(frozenset((source, destination)) for _, source, destination, _ in datagrams)
    endpoints = flows.most_common(1)[0][0]
    session = [datagram for datagram in datagrams if {datagram[1], datagram[2]} == endpoints]

    bases: dict[str, int] = {}
    joiner = None
    for _, source, _, payload in session:
        identity = handshake_identity_of(payload)
        if identity is None or source in bases:
            continue
        joiner = joiner or source
        bases[source] = identity[1]

    if len(bases) < 2:
        print(f"{sys.argv[1]}: only {len(bases)} handshake(s); the key cannot be derived")
        return

    key = 0
    for base in bases.values():
        key ^= base
    host = next(endpoint for endpoint in endpoints if endpoint != joiner)

    print(f"{sys.argv[1]}")
    print(f"  session {joiner} (joiner) <-> {host} (host), key 0x{key:08x}")

    start = session[0][0]
    for ts, source, _, payload in session:
        if source != host:
            continue
        counter, plain, verified, _ = decode_frame(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        messages = parse_messages(content)

        # Only a run's tail carries the answer to the report *and* the seven-byte
        # roster close behind it; a briefing publication answers `e0` too.
        roles = [role(message_type, length, body) for message_type, length, _, body in messages]
        if "payload" not in roles or "roster close" not in roles:
            continue

        print(f"\n  t+{ts - start:.3f}  host frame, counter {counter}, {len(messages)} record(s)")
        print(f"\n  {'#':>3}  {'record':>6}  {'len':>4}  role")
        seen = []
        for index, (message_type, length, _, body) in enumerate(messages):
            label = role(message_type, length, body)
            if label:
                seen.append(label)
            shown = label or (
                "(item-box / world object, the live round's own)"
                if message_type & 0x7FF in WORLD_OBJECT_FAMILIES
                else ""
            )
            print(f"  {index:3d}  0x{message_type:04x}  {length:4d}  {shown}")
        break
    else:
        print("\n  no host frame carries the round's opening run")
        return

    expected = [
        label
        for label, _, _ in CODE_SHAPE
        if label != "marker" and label not in HOST_ADDITIONS
    ]

    print()
    for label, identifier, length in CODE_SHAPE:
        if label in HOST_ADDITIONS:
            print(
                f"  + {label} (0x{identifier:04x}/{length}) — this host's own addition; "
                "both captures write it beside the report, and the briefing one also "
                "writes it inside the run"
            )

    actual = [label for label in seen if label not in HOST_ADDITIONS]

    if actual == expected:
        print(
            f"\n  the run is the measured {len(expected)} role(s), in order, "
            "with the additions above"
        )
        return

    print(f"\n  the run differs from the {len(expected)} role(s) the host writes:")
    for index in range(max(len(actual), len(expected))):
        got = actual[index] if index < len(actual) else "—"
        want = expected[index] if index < len(expected) else "—"
        flag = "ok " if got == want else ">>>"
        print(f"    {flag} {index:3d}  capture {got:>18}  code {want:>18}")


if __name__ == "__main__":
    main()
