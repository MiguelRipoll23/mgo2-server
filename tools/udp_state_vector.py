#!/usr/bin/env python3
"""Reconstruct the operations stream's 89-byte state vector from a capture.

Usage:
    python3 tools/udp_state_vector.py CAPTURE.pcapng [--vectors] [--offsets N]

The operations stream (`0x1254` / `0x1a54` / `0x9254` / `0x9a54`) carries the
round's state as a small opcode language, and the replay console's decoder
(`docs/analysis/replay-console/rpdt.js`) names seven of its fields. This tool
answers the question the names leave open: which of the eighty-nine bytes the
captures actually move, which they hold still, and in what order.

    opcode 0x00  the whole vector: 89 bytes follow the opcode
    opcode 0x01  the round clock: a little-endian 16-bit count of 125 ms units
    opcode 0x02  a write: offset, count, then `count` values

`state[2]`, `state[3]`, `state[40]`, `state[41]` and `state[87]`/`state[88]` are
named by the console; the rest are not, and a byte that never moves in a
capture is a byte whose value is that capture's, not the protocol's.

The vector is folded forward exactly as the console folds it — a full vector
replaces the whole thing, a write patches from its offset — so what is reported
at the end of the run is the state as a reader replaying the stream sees it,
not the last body that happened to arrive. Both are printed: the folded state
and, with `--vectors`, every intermediate one.

Directions are roles read off the wire as everywhere else in `tools/`: `host` is
the gameplay host that sent the handshake reply, `client` the joiner.
"""

from __future__ import annotations

import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    decode_frame,
    handshake_identity_of,
    lzss_decompress,
    parse_messages,
)
from udp_record_semantics import COMPRESSION_MARKER  # noqa: E402

OPERATIONS_IDENTIFIERS = {0x1254, 0x1A54, 0x9254, 0x9A54}

VECTOR_LENGTH = 89
OP_VECTOR = 0x00
OP_CLOCK = 0x01
OP_WRITE = 0x02
CLOCK_MS_PER_UNIT = 125.0

# The offsets the replay console names, with the reading it gives them. Anything
# not in here is reported as unnamed.
NAMED_OFFSETS = {
    2: "round result (2 = raw team 0, 3 = raw team 2, 1 = draw, 255 = unwritten)",
    3: "match result, same coding as state[2]",
    37: "side-0 destabilizer (255 = none, 254 = lost, 253 = fired, <64 = slot)",
    38: "side-1 destabilizer, same coding as state[37]",
    40: "objective holder 0 (0xff = none, 0x3f = nobody, &0x3f = slot, &0x40 = flag)",
    41: "objective holder 1, same coding as state[40]",
    42: "capture clock, side 0 (Capture mode only; seconds, counting down)",
    43: "capture clock, side 1, same coding as state[42]",
    87: "team-sneaking countdown in seconds",
    88: "team-sneaking lobby slot",
}

# The opcode the console reads off the operations stream but this tool does not
# interpret, counted so a run says what it walked past.
OTHER_OPCODES = {
    0x05: "names one player, a lobby slot; meaning not decoded",
    0x09: "kill feed: killer, victim",
    0x0B: "a six-byte per-entity countdown opening on 0x3c; not the briefing "
          "countdown and not read -- see docs/protocol/STATE_VECTOR.md section 4",
    0x0E: "round over, with a reason byte",
    0x13: "per-player rating, an array of signed sixteen-bit values",
}


def gameplay_flow(datagrams: list[tuple]) -> set[str]:
    """The two endpoints the gameplay session ran between.

    The busiest flow. A capture of a game is dominated by it, and the lobby,
    STUN and HTTPS flows beside it are smaller by an order of magnitude.
    """
    flows = Counter(
        frozenset((source, destination)) for _, source, destination, _ in datagrams
    )
    endpoints = flows.most_common(1)[0][0]
    if len(endpoints) != 2:
        raise SystemExit(f"the busiest flow is not a pair: {sorted(endpoints)}")
    return endpoints


def session_records(capture_path: str) -> list[tuple[float, str, int, bytes]]:
    """Every record of the gameplay session, in capture order.

    Yields `(timestamp, role, message type, body)` where the role is `host` or
    `client`, read off which side answered the handshake rather than off a port.
    """
    capture = Pcapng(capture_path)
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
        raise SystemExit(f"{capture_path}: holds no UDP datagrams")

    endpoints = gameplay_flow(datagrams)

    bases: dict[str, int] = {}
    joiner: str | None = None
    for _, source, destination, payload in datagrams:
        if {source, destination} != endpoints:
            continue
        identity = handshake_identity_of(payload)
        if identity is not None and source not in bases:
            joiner = joiner or source
            bases[source] = identity[1]

    if joiner is None or len(bases) < 2:
        raise SystemExit(f"{capture_path}: no handshake pair; the session key is unknown")

    host = next(endpoint for endpoint in endpoints if endpoint != joiner)
    key = 0
    for base in bases.values():
        key ^= base

    out: list[tuple[float, str, int, bytes]] = []
    for ts, source, destination, payload in datagrams:
        if {source, destination} != endpoints:
            continue
        role = "host" if source == host else "client"

        counter, plain, verified, _pre_keyed = decode_frame(payload, key)
        if not verified:
            continue

        # The content region sits between the two-byte header and the ten-byte
        # tail digest; a compressed frame's content is a raw LZSS stream.
        content = plain[2 : len(plain) - 0xA]
        if counter & COMPRESSION_MARKER:
            try:
                content = lzss_decompress(content)
            except ValueError:
                continue
        messages = parse_messages(content)
        if not messages:
            continue

        for message_type, _flags, _ordinal, body in messages:
            out.append((ts, role, message_type, bytes(body)))
    return out


def fold(records: list[tuple[float, str, int, bytes]]) -> tuple[list[int | None], list[tuple[float, list[int | None]]], Counter]:
    """Fold the stream's writes into the state a reader would hold.

    Returns the folded vector, every intermediate vector with the time it became
    current, and a count of the opcodes walked past.
    """
    state: list[int | None] = [None] * VECTOR_LENGTH
    timeline: list[tuple[float, list[int | None]]] = []
    seen_opcodes: Counter = Counter()

    for ts, _role, message_type, body in records:
        if message_type not in OPERATIONS_IDENTIFIERS or not body:
            continue

        opcode = body[0]
        seen_opcodes[opcode] += 1

        if opcode == OP_VECTOR and len(body) >= 1 + VECTOR_LENGTH:
            state = list(body[1 : 1 + VECTOR_LENGTH])
        elif opcode == OP_WRITE and len(body) >= 3:
            offset, count = body[1], body[2]
            # A write is the opcode, an offset, a count and exactly `count`
            # values, so the body is 3 + count long. A count that overruns the
            # body is not a write this tool can read.
            if len(body) < 3 + count:
                continue
            for index in range(count):
                position = offset + index
                if 0 <= position < VECTOR_LENGTH:
                    state[position] = body[3 + index]
        else:
            continue

        timeline.append((ts, list(state)))

    return state, timeline, seen_opcodes


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    capture_path = sys.argv[1]
    show_vectors = "--vectors" in sys.argv
    show_writes = "--writes" in sys.argv
    only = None
    if "--offsets" in sys.argv:
        only = sys.argv[sys.argv.index("--offsets") + 1]

    records = session_records(capture_path)
    operations = [record for record in records if record[2] in OPERATIONS_IDENTIFIERS]
    if not operations:
        raise SystemExit(f"{capture_path}: no operations-stream records")

    state, timeline, opcodes = fold(records)

    base = operations[0][0]
    print(f"== {capture_path}")
    print(
        f"   {len(operations)} operations records over "
        f"{operations[-1][0] - base:.3f} s, "
        f"{sum(1 for r in operations if r[1] == 'host')} host / "
        f"{sum(1 for r in operations if r[1] == 'client')} client"
    )
    counted = ", ".join(
        f"0x{opcode:02x}={count}"
        for opcode, count in sorted(opcodes.items())
    )
    print(f"   opcodes: {counted}")
    for opcode, note in sorted(OTHER_OPCODES.items()):
        if opcodes.get(opcode):
            print(f"     0x{opcode:02x} ({opcodes[opcode]}): {note}")

    if show_writes:
        print()
        print("   every vector (opcode 0x00) and write (opcode 0x02) as it arrived:")
        for ts, role, _message_type, body in operations:
            if not body:
                continue
            opcode = body[0]
            if opcode == OP_VECTOR and len(body) >= 1 + VECTOR_LENGTH:
                print(f"     t+{ts - base:9.3f} {role:6} 0x00 VECTOR  "
                      + " ".join(f"{b:02x}" for b in body[1 : 1 + VECTOR_LENGTH]))
            elif opcode == OP_WRITE and len(body) >= 3:
                offset, count = body[1], body[2]
                values = body[3 : 3 + count]
                printable = "".join(
                    chr(b) if 32 <= b < 127 else "." for b in values
                )
                print(
                    f"     t+{ts - base:9.3f} {role:6} 0x02 WRITE   "
                    f"state[{offset}..{offset + count - 1}] = "
                    + " ".join(f"{b:02x}" for b in values)
                    + f"   |{printable}|"
                )

    if show_vectors:
        print()
        print("   every vector the reader held, as it changed:")
        previous = None
        for ts, snapshot in timeline:
            if snapshot == previous:
                continue
            previous = snapshot
            body = " ".join("--" if byte is None else f"{byte:02x}" for byte in snapshot)
            print(f"     t+{ts - base:9.3f}  {body}")

    print()
    print("   the folded vector:")
    print("     " + " ".join("--" if byte is None else f"{byte:02x}" for byte in state))

    print()
    print("   per offset, the values the stream ever wrote:")
    history: dict[int, Counter] = defaultdict(Counter)
    for _ts, snapshot in timeline:
        for index, byte in enumerate(snapshot):
            if byte is not None:
                history[index][byte] += 1

    for index in range(VECTOR_LENGTH):
        if only is not None and str(index) != only:
            continue
        values = history.get(index)
        if not values:
            line = "never written"
        else:
            distinct = sorted(values)
            rendered = ", ".join(f"{value}" for value in distinct[:8])
            if len(distinct) > 8:
                rendered += f", …+{len(distinct) - 8}"
            line = f"{rendered}"
        name = NAMED_OFFSETS.get(index)
        marker = "  *" if name else "   "
        print(f"     {marker} state[{index:2d}]  {line}" + (f"   -- {name}" if name else ""))


if __name__ == "__main__":
    main()
