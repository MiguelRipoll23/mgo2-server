#!/usr/bin/env python3
"""Report what a client's NAT/UPnP module learned, from a capture of its own host.

Usage:
    python3 tools/probe_client_nat.py <capture.pcapng> [--port 5730]

The client's handshake mode is decided by one value: the P2P port its own
connection record publishes, read at `this+0x30154` and passed to the decision at
`0xd7d530`, which returns "guest" (mode 2) for anything outside
`{0, 1, 2, 16, 144}`. A record with no port in it therefore resolves to mode 1,
which is what this host's client sends where the recorded client sends 2
(docs/protocol/P2P_CONNECT_FSM.md §7.2). Nothing the server sends takes part, so
the only place to look is the client's own side.

That side is STUN. The game talks to `stun.mgo2pc.com` and the captures carry the
exchange — 8 request/reply pairs in `mgo2-game1.pcapng`, 18 in `mgo2-game2` — so a
capture taken on the client answers directly whether the transaction completed
and what address came back. The server cannot be fixed from that number, but
knowing the module came back empty is what separates a client-side bring-up
failure from anything this server is sending.

**This protocol is not standard STUN.** The magic cookie is `6c02e061` rather than
`2112a442`, the transaction id is opaque, and the attributes are renumbered:
`0x0001` MAPPED-ADDRESS, `0x0004` RESPONSE-ORIGIN, `0x0005` OTHER-ADDRESS, and
`CHANGE-REQUEST` as `0x0002`. Only `0x0001` is read here, and its address is taken
plain — the address it returns is the client's public one, byte for byte.
"""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402

# The game's NAT server, as the captures show it.
STUN_PORT = 3478

# Renumbered attribute carrying the client's mapped address.
MAPPED_ADDRESS = 0x0001
CHANGE_REQUEST = 0x0002

# The decision at 0xd7d530, as a set: anything outside it resolves to "guest".
NOT_GUEST = {0, 1, 2, 0x10, 0x90}


def parse_attributes(payload: bytes) -> list[tuple[int, bytes]]:
    """The attributes of one NAT message, after its 20-byte header."""
    if len(payload) < 20:
        return []
    declared = struct.unpack_from(">H", payload, 2)[0]
    attributes = []
    offset = 20
    while offset + 4 <= min(len(payload), 20 + declared):
        kind, length = struct.unpack_from(">HH", payload, offset)
        attributes.append((kind, payload[offset + 4 : offset + 4 + length]))
        offset += 4 + ((length + 3) // 4) * 4
    return attributes


def mapped_address(body: bytes) -> str | None:
    """The address out of a MAPPED-ADDRESS value, or None if it is not one."""
    if len(body) < 8 or body[1] != 0x01:
        return None
    port, = struct.unpack_from(">H", body, 2)
    return f"{'.'.join(str(octet) for octet in body[4:8])}:{port}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("capture")
    parser.add_argument("--port", type=int, default=5730, help="client's P2P port")
    parser.add_argument("--nat-port", type=int, default=STUN_PORT)
    args = parser.parse_args()

    capture = Pcapng(args.capture)
    capture.read()

    requests = replies = 0
    learned: set[str] = set()
    first = None
    print(f"STUN with port {args.nat_port}:")
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP or args.nat_port not in (source_port, destination_port):
            continue
        if first is None:
            first = ts
        # The client asks the NAT server, so its request is the one whose
        # *destination* is the NAT port.
        outbound = destination_port == args.nat_port
        if outbound:
            requests += 1
            continue
        replies += 1
        for kind, body in parse_attributes(payload):
            if kind == MAPPED_ADDRESS:
                address = mapped_address(body)
                if address:
                    learned.add(address)
                    print(f"  t={ts - first:7.3f}s  MAPPED-ADDRESS {address}")
            elif kind == CHANGE_REQUEST:
                print(f"  t={ts - first:7.3f}s  CHANGE-REQUEST (server asked to test the mapping)")

    print(f"\n  {requests} requests, {replies} replies")
    if requests == 0:
        print("  the client never asked -- its NAT module did not run")
    elif replies == 0:
        print("  no reply came back -- the transaction is being dropped")
    elif not learned:
        print("  replies arrived but none carried a MAPPED-ADDRESS")
    else:
        print(f"  the client learned {', '.join(sorted(learned))}")

    local = args.port
    masked = local & 0xFCFF
    verdict = "guest, mode 2" if masked not in NOT_GUEST else "host, mode 1"
    print(f"\n  P2P port {local} & 0xfcff = 0x{masked:04x} -> {verdict}")
    print("  this is what the connection record has to hold for that verdict;")
    print("  an unpublished port reads 0 and resolves to mode 1.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())