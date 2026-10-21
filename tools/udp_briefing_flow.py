#!/usr/bin/env python3
"""Read a capture's briefing: who reported ready, and when the round opened.

Usage:
    python3 tools/udp_briefing_flow.py CAPTURE.pcapng [--until SECONDS]

Prints, in capture order, the three things that make up a briefing:

- the **operations clock**: the operations record (`0x1a54` / `0x1254`) whose
  payload opens `01` and whose two bytes are a time in the stream's own 125 ms
  units. It is the *round's* clock — the rule's own time, 2 400 units for five
  minutes, held there through a briefing — and not the briefing's countdown,
  which is a separate `0x0b` record (`docs/protocol/STATE_VECTOR.md` §4);
- the **ready state**: every record on the round's payload identifier
  (`0x1ade` / `0x12de`, and the same with the `0x8000` class bit) whose body
  opens `e1` or `e0`. `e1` is a client reporting its own state; `e0` is the host
  publishing one, one record per tracked player, and the record's fourth byte is
  where that player sits in the publication;
- the **round open**: the point at which the operations identifier flips to its
  sibling (`0x1254` <-> `0x1a54`). Nothing opens a round on a clock; the flip
  follows the reports.

A frame whose tail digest does not verify contributes nothing, and the run ends
with the counts it took, so a capture that has been read the wrong way reports
that rather than printing a plausible timeline.

The session key is derived from the two handshake counter bases, as everywhere
else in `tools/`. A host is observed that keys *every* frame of a live session
with the pre-handshake constant instead, so each frame is read with the session
key and then with that constant; how many took the second route is reported,
which is zero on a host that switches keys and the whole of one direction on a
host that does not.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    decode_frame,
    gameplay_endpoints,
    handshake_identity_of,
    lzss_decompress,
    parse_messages,
)

# The operations stream's identifiers. The two generations are a 0x800 bit
# apart, and a flip between them is a round boundary.
OPERATIONS_IDENTIFIERS = {0x1254, 0x1A54, 0x9254, 0x9A54}

# The round's payload identifier, in both generations and both class-bit forms.
PAYLOAD_IDENTIFIERS = {0x12DE, 0x1ADE, 0x92DE, 0x9ADE}

# Opcode a client reports its readiness under, and the one the host publishes a
# player's readiness under. The byte behind either is the state.
REPORT_OPCODE = 0xE1
PUBLISH_OPCODE = 0xE0

# The operations stream's clock opcode, and the length of its body: the opcode
# and a little-endian 16-bit count of 125 ms units.
CLOCK_OPCODE = 0x01
CLOCK_LENGTH = 3


def clock_units(body: bytes) -> int | None:
    """The time a clock body carries, in 125 ms units, or None.

    The body is the opcode and a little-endian 16-bit count. A body of any other
    length, or one on another opcode, is another record of the same stream.
    """
    if len(body) != CLOCK_LENGTH or body[0] != CLOCK_OPCODE:
        return None
    return body[1] | (body[2] << 8)


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    until = None
    if "--until" in sys.argv:
        until = float(sys.argv[sys.argv.index("--until") + 1])

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

    endpoints = gameplay_endpoints(datagrams)
    session = [datagram for datagram in datagrams if {datagram[1], datagram[2]} == endpoints]

    # Both counter bases, from the handshake each side opened with. Neither side
    # is assumed to be the higher-numbered port: the joiner is the side that
    # sent a handshake first, which is the only thing on the wire that says so.
    bases: dict[str, int] = {}
    joiner = None
    for _, source, _, payload in session:
        identity = handshake_identity_of(payload)
        if identity is None or source in bases:
            continue
        joiner = joiner or source
        bases[source] = identity[1]

    if len(bases) < 2:
        print(f"{sys.argv[1]}: only {len(bases)} handshake(s) in the session; "
              "the session key cannot be derived")
        return

    key = 0
    for base in bases.values():
        key ^= base
    host = next(endpoint for endpoint in endpoints if endpoint != joiner)

    print(f"{sys.argv[1]}")
    print(f"  session {joiner} (joiner) <-> {host} (host), key 0x{key:08x}")

    start = session[0][0]
    fallbacks = 0
    reports = 0
    publications = 0
    opens = 0
    generation: int | None = None
    heading = False

    for ts, source, _, payload in session:
        counter, plain, verified, pre_keyed = decode_frame(payload, key)
        if not verified:
            continue
        if pre_keyed:
            fallbacks += 1

        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        messages = parse_messages(content)
        if not messages:
            continue

        # A round opens when the *generation* of the operations identifier
        # changes. The two generations are a 0x800 bit apart, so a record's
        # class bit above it (0x8000) does not read as a change and the same
        # generation seen under two class-bit forms is not two rounds.
        identifiers = {message_type for message_type, _, _, _ in messages}
        operations = identifiers & OPERATIONS_IDENTIFIERS
        flipped = False
        if operations:
            found = {bool(identifier & 0x800) for identifier in operations}
            if len(found) == 1:
                current = found.pop()
                flipped = generation is not None and current != generation
                generation = current

        relative = ts - start
        if until is not None and relative > until:
            break

        direction = "IN" if source == joiner else "OUT"
        if flipped:
            opens += 1
            if not heading:
                heading = True
                print(f"\n{'t':>9}  {'dir':3} {'record':>6} {'ord':>3}  what")
            print(f"{relative:9.3f}  {direction:3}  ------  ---  ROUND OPENS "
                  f"(operations identifier set changed)")

        for message_type, _, ordinal, body in messages:
            line = None
            if message_type in PAYLOAD_IDENTIFIERS and body and body[0] in (
                REPORT_OPCODE,
                PUBLISH_OPCODE,
            ):
                if body[0] == REPORT_OPCODE:
                    reports += 1
                    line = f"client reports ready: {body.hex(' ')}"
                else:
                    publications += 1
                    line = f"host publishes a ready state: {body.hex(' ')}"
            elif message_type in OPERATIONS_IDENTIFIERS:
                countdown = clock_units(body)
                if countdown is not None:
                    line = f"operations clock {countdown} x 125 ms"

            if line is None:
                continue
            if not heading:
                heading = True
                print(f"\n{'t':>9}  {'dir':3} {'record':>6} {'ord':>3}  what")
            print(f"{relative:9.3f}  {direction:3} 0x{message_type:04x} {ordinal:3}  {line}")

    print()
    if fallbacks:
        print(f"{fallbacks} frame(s) read with the pre-handshake key, not the session key")
    print(f"{reports} client report(s), {publications} host publication(s), {opens} round open(s)")


if __name__ == "__main__":
    main()
