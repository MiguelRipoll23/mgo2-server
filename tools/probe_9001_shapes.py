#!/usr/bin/env python3
"""Report every message of a chosen type in a capture, by direction and length.

Usage:
    python3 tools/probe_9001_shapes.py mgo2-game.pcapng 0x9001

The question this answers is narrow: when the console client is stuck re-sending
a short record, is the length it sends one the captured dedicated server ever
received? Lengths are counted per direction so a type the host only ever *sends*
is not mistaken for one it answers.
"""

from __future__ import annotations

import struct
import sys
from collections import Counter

from pcap_conversations import UDP, Pcapng
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

JOINER = "10.2.0.2:5730"
SERVER = "99.66.131.177:5731"


def main() -> None:
    if len(sys.argv) < 3:
        print(__doc__)
        return
    wanted = int(sys.argv[2], 16)

    capture = Pcapng(sys.argv[1])
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in capture.decoded_packets():
        if protocol != UDP:
            continue
        local = f"{source}:{source_port}"
        remote = f"{destination}:{destination_port}"
        if {local, remote} != {JOINER, SERVER}:
            continue
        datagrams.append((ts, local, payload))
    datagrams.sort(key=lambda row: row[0])

    bases: dict[str, int] = {}
    for _, local, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[local] = struct.unpack_from("<II", plain, 6)[1]
    key = (bases[JOINER] ^ bases[SERVER]) & 0xFFFFFFFF

    shapes: dict[str, Counter] = {"joiner": Counter(), "server": Counter()}
    samples: dict[tuple[str, int], bytes] = {}

    for _, local, payload in datagrams:
        role = "joiner" if local == JOINER else "server"
        if len(payload) == 44:
            _, plain, _ = decode(payload, PRE_KEY)
            counter = 0
        else:
            counter, plain, verified = decode(payload, key)
            if not verified:
                continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for message_type, _, _, body in parse_messages(content):
            if message_type != wanted:
                continue
            shapes[role][len(body)] += 1
            samples.setdefault((role, len(body)), body)

    print(f"session key 0x{key:08x}\n")
    for role in ("joiner", "server"):
        print(f"{role} -> other, type 0x{wanted:04x}:")
        if not shapes[role]:
            print("    (none)")
        for length, count in sorted(shapes[role].items()):
            body = samples[(role, length)]
            print(
                f"    {count:5d} x body[{length:3d}]  {body.hex()[:72]}"
                f"{'…' if len(body) > 36 else ''}"
            )
        print()


if __name__ == "__main__":
    main()