#!/usr/bin/env python3
"""Replay the opening exchange of a live capture, in both directions.

Usage:
    python3 tools/udp_join_replay.py docs/mgo2-game.pcapng [--frames N]

Prints every frame of the join in time order with its records decoded, so the
server's own answer to each inbound frame can be set beside what the captured
server actually sent. Nothing is summarised: the point is the exact bytes and
their order, because that is what the implementation has to reproduce.
"""

from __future__ import annotations

import struct
import sys

from pcap_conversations import UDP, Pcapng
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

JOINER = "10.2.0.2:5730"
SERVER = "99.66.131.177:5731"


def describe(role: str, counter: int, content: bytes) -> None:
    if counter & 0x8000:
        print(f"    [{role}] hdr=0x{counter:04x} compressed -> {len(content)} bytes")
    else:
        print(f"    [{role}] hdr=0x{counter:04x} plain, {len(content)} bytes")
    for message_type, length, flags, body in parse_messages(content):
        print(
            f"        type=0x{message_type:04x} len={length} flags2={flags} "
            f"body[{len(body)}]={body.hex()[:96]}{'…' if len(body) > 48 else ''}"
        )


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return
    wanted = 12
    if "--frames" in sys.argv:
        wanted = int(sys.argv[sys.argv.index("--frames") + 1])

    joiner, server = JOINER, SERVER
    if "--joiner" in sys.argv:
        joiner = sys.argv[sys.argv.index("--joiner") + 1]
    if "--server" in sys.argv:
        server = sys.argv[sys.argv.index("--server") + 1]

    capture = Pcapng(sys.argv[1])
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in capture.decoded_packets():
        if protocol != UDP:
            continue
        local = f"{source}:{source_port}"
        remote = f"{destination}:{destination_port}"
        if {local, remote} != {joiner, server}:
            continue
        datagrams.append((ts, local, remote, payload))
    datagrams.sort(key=lambda row: row[0])

    bases: dict[str, int] = {}
    for _, local, _, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[local] = struct.unpack_from("<II", plain, 6)[1]
    key = (bases[joiner] ^ bases[server]) & 0xFFFFFFFF
    print(f"session key 0x{key:08x}, joiner base 0x{bases[joiner]:08x}, server base 0x{bases[server]:08x}\n")

    start = datagrams[0][0]
    shown = 0
    for ts, local, remote, payload in datagrams:
        counter, plain, verified = decode(payload, key)
        if not verified and len(payload) != 44:
            role = "joiner" if local == joiner else "server"
            print(f"[{ts - start:8.3f}] {role} -> other  {len(payload)} B  hdr=0x{counter:04x}  TAIL DID NOT VERIFY")
            continue
        role = "joiner" if local == joiner else "server"
        if len(payload) == 44:
            # A handshake is an ordinary session record, so it decodes through the
            # same path as everything else rather than by hand-placed offsets.
            _, plain, _ = decode(payload, PRE_KEY)
            content = plain[2 : len(plain) - 0xA]
            print(f"[{ts - start:8.3f}] {role} -> other  handshake {len(payload)} B")
            for message_type, length, flags, body in parse_messages(content):
                peer_id, counter_base = struct.unpack_from("<II", body, 0)
                count = body[15]
                print(
                    f"        type=0x{message_type:04x} len={length} flags2={flags} "
                    f"peer_id={peer_id} counter_base=0x{counter_base:08x} "
                    f"magic={body[8:12].hex()} flags=0x{body[12]:02x} "
                    f"ver_field=0x{struct.unpack_from('<H', body, 13)[0]:04x} pairs={count}"
                )
                for index in range(count):
                    offset = 16 + (index * 6)
                    address = ".".join(str(body[offset + n]) for n in range(4))
                    port = struct.unpack_from("<H", body, offset + 4)[0]
                    print(f"          pair {index}: {address}:{port}")
            shown += 1
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        print(f"[{ts - start:8.3f}] {role} -> other  {len(payload)} B")
        describe(role, counter, content)
        shown += 1
        if shown >= wanted:
            break


if __name__ == "__main__":
    main()
