#!/usr/bin/env python3
"""Report the unresolved session records of a live capture exactly as sent.

Usage:
    python3 tools/udp_unresolved_probe.py docs/mgo2-game.pcapng

The inventory in docs/protocol/UDP_GAME_CAPTURE.md leaves `0x5001` and `0xd001`
unresolved. This prints every one of them in time order, with the rest of its
frame, so a proposed rule can be checked against what each side actually did
rather than against the shape of one record.
"""

from __future__ import annotations

import struct
import sys
from collections import Counter, defaultdict

from pcap_conversations import UDP, Pcapng
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

JOINER = "10.2.0.2:5730"
SERVER = "99.66.131.177:5731"
INTERESTING = (0x5001, 0x5000, 0xD001)


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

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
        datagrams.append((ts, local, remote, payload))

    # Each handshake carries the sender's own counter base; the key is their xor.
    bases: dict[str, int] = {}
    for _, local, _, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[local] = struct.unpack_from("<II", plain, 6)[1]
    key = (bases[JOINER] ^ bases[SERVER]) & 0xFFFFFFFF
    print(f"session key 0x{key:08x}\n")

    frames = []
    for ts, local, remote, payload in datagrams:
        counter, plain, verified = decode(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        frames.append((ts, local, counter, parse_messages(content)))

    # Which side sends what, and how it is shaped.
    for side in (SERVER, JOINER):
        shapes = Counter()
        companions = Counter()
        for _, local, _, messages in frames:
            if local != side:
                continue
            for message in messages:
                if message[0] in INTERESTING:
                    shapes[(message[0], message[1], message[2], message[3].hex())] += 1
                    companions[tuple(sorted({m[0] for m in messages} - {message[0]}))] += 1
        print(f"=== {side} ===")
        for (message_type, length, flags, body), count in shapes.most_common(10):
            print(f"  type=0x{message_type:04x} len={length} flags2={flags} body={body or '-'} x{count}")
        print("  co-occurring record types in the same frame:")
        for others, count in companions.most_common(6):
            rendered = ", ".join(f"0x{other:04x}" for other in others) or "(alone)"
            print(f"    {rendered} x{count}")
        print()

    # The first and last few of each, in time order, for the eye.
    for target in INTERESTING:
        print(f"=== 0x{target:04x} in time order (first 12 and last 6) ===")
        rows = [
            (ts, local, counter, message)
            for ts, local, counter, messages in frames
            for message in messages
            if message[0] == target
        ]
        if not rows:
            print("  none\n")
            continue
        for ts, local, counter, message in rows[:12] + rows[-6:]:
            who = "srv" if local == SERVER else "join"
            print(
                f"  [{ts:10.3f}] {who} hdr=0x{counter:04x} flags2={message[2]:3d} "
                f"len={message[1]} body={message[3].hex() or '-'}"
            )
        sides = Counter("srv" if local == SERVER else "join" for _, local, _, _ in rows)
        values = Counter(message[2] for _, _, _, message in rows)
        print(f"  total {len(rows)}; sides {dict(sides)}")
        print(f"  distinct flags2 values: {len(values)}, min {min(values)} max {max(values)}")
        print(f"  flags2 histogram (first 20): {sorted(values.items())[:20]}\n")


if __name__ == "__main__":
    main()
