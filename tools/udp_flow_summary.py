#!/usr/bin/env python3
"""Summarise one MGO2 UDP peer session in a pcapng capture.

Usage:
    python3 tools/udp_flow_summary.py docs/mgo2-game.pcapng --peer IP:PORT

Prints the handshake identity of both sides, then a message-type inventory with
counts, direction split and body lengths. Frames whose tail digest does not
verify are counted separately rather than parsed, so a session whose key is
unknown cannot silently contribute invented records.
"""

from __future__ import annotations

import struct
import sys
from collections import Counter, defaultdict

from pcap_conversations import Pcapng, UDP
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

# The captured host is 10.2.0.2:5730 - the joiner. The far side is the
# dedicated server it joined. Both addresses are private to the capture this
# was written against; pass --peer to point the tool at any other session.
JOINER = "10.2.0.2:5730"


def endpoint(address: str, port: int) -> str:
    return f"{address}:{port}"


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    peer_filter = None
    if "--peer" in sys.argv:
        peer_filter = sys.argv[sys.argv.index("--peer") + 1]

    capture = Pcapng(sys.argv[1])
    capture.read()

    datagrams = []
    for _, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        local = endpoint(source, source_port)
        remote = endpoint(destination, destination_port)
        if peer_filter and peer_filter not in (local, remote):
            continue
        if 3478 in (source_port, destination_port):
            continue
        datagrams.append((local, remote, payload))

    # Handshake identity per direction. Both sides use the pre-key chain key.
    identity: dict[str, tuple[int, int, int]] = {}
    bases: dict[str, int] = {}
    for local, remote, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if len(plain) < 34 or struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        peer_id, counter_base = struct.unpack_from("<II", plain, 6)
        identity[local] = (peer_id, counter_base, plain[18])
        bases[local] = counter_base

    print(f"session {peer_filter}")
    for side, (peer_id, counter_base, version) in identity.items():
        role = "joiner" if side == JOINER else "dedicated server"
        print(f"  {side:22} peer_id={peer_id:<8} counter_base=0x{counter_base:08x} "
              f"ver={version} [{role}]")
    if len(bases) == 2:
        left, right = list(bases)
        key = bases[left] ^ bases[right]
        print(f"  session key K = 0x{key:08x} ({left} ^ {right})")

    landing = 0
    total = 0

    counters: dict[tuple[str, int], Counter] = defaultdict(Counter)
    lengths: dict[tuple[str, int], set] = defaultdict(set)
    unreadable = Counter()
    compressed = 0
    frames = 0

    for local, remote, payload in datagrams:
        sender = local if local in bases else remote
        receiver = remote if local in bases else local
        if sender not in bases or receiver not in bases:
            unreadable["no session key"] += 1
            continue
        direction = "server->joiner" if sender != JOINER else "joiner->server"
        counter, plain, verified = decode(payload, bases[sender] ^ bases[receiver])
        frames += 1
        if not verified:
            unreadable[direction] += 1
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            compressed += 1
            content = lzss_decompress(content)
        messages = parse_messages(content)
        total += 1
        if sum(4 + message[1] for message in messages) == len(content):
            landing += 1
        for message_type, length, _, _ in messages:
            counters[(direction, message_type)][length] += 1
            lengths[(direction, message_type)].add(length)

    print(f"\n{frames} frames, {compressed} LZSS-compressed")
    print(f"records parsed to the last byte: {landing}/{total}")
    if unreadable:
        print(f"not parsed: {dict(unreadable)}")

    print(f"\n{'dir':10} {'type':>6} {'count':>7}  body lengths (len x count)")
    for (direction, message_type), by_length in sorted(
        counters.items(), key=lambda item: (item[0][0], item[0][1])
    ):
        detail = " ".join(
            f"{length}x{count}" for length, count in sorted(by_length.items())
        )
        total = sum(by_length.values())
        print(f"{direction:10} 0x{message_type:04x} {total:7}  {detail}")


if __name__ == "__main__":
    main()