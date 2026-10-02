#!/usr/bin/env python3
"""Dump one flow from a pcapng capture with payloads, reassembling TCP streams.

Usage:
    python3 tools/pcap_flow.py docs/mgo2-game.pcapng <port> [--udp] [--limit N]

Prints each packet of the flow that carries <port>, with its payload hex and a
short ASCII rendering, in capture order. TCP payloads are shown as the wire bytes;
nothing is reassembled, so the frame boundaries are the ones the socket saw.
"""

from __future__ import annotations

import sys

from pcap_conversations import TCP, UDP, Pcapng


def main() -> None:
    if len(sys.argv) < 3:
        print(__doc__)
        return

    path = sys.argv[1]
    wanted_port = int(sys.argv[2])
    wanted_protocol = UDP if "--udp" in sys.argv else TCP
    limit = 40
    if "--limit" in sys.argv:
        limit = int(sys.argv[sys.argv.index("--limit") + 1])

    capture = Pcapng(path)
    capture.read()

    first_timestamp = None
    shown = 0
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != wanted_protocol:
            continue
        if source_port != wanted_port and destination_port != wanted_port:
            continue
        if first_timestamp is None:
            first_timestamp = ts
        shown += 1
        if shown > limit:
            break
        direction = "OUT" if source_port == wanted_port else "IN "
        relative = 0.0 if first_timestamp is None else ts - first_timestamp
        hex_bytes = " ".join(f"{byte:02x}" for byte in payload)
        text = "".join(chr(byte) if 32 <= byte < 127 else "." for byte in payload)
        print(f"[{relative:9.3f}] {direction} {source}:{source_port} -> {destination}:{destination_port} "
              f"({len(payload)} B)")
        print(f"    {hex_bytes}")
        print(f"    {text}")


if __name__ == "__main__":
    main()