#!/usr/bin/env python3
"""Test whether the session records pair up as request and answer.

Usage:
    python3 tools/udp_pairing_probe.py docs/mgo2-game.pcapng

The inventory lists the server sending a `0x1x..` family (0x1250, 0x1254,
0x1256, 0x1258, 0x125a, 0x12de, 0x1a50, ...) in volume and the joiner sending a
`0x5x..` family (0x5250, 0x5254, 0x5256, 0x5258, 0x525a, 0x52de, 0x5a50, ...) whose
low bytes and counts line up with it. This reports, per low byte, what each side
actually sent and how closely the answer follows the request in time, so the
pairing can be confirmed or dismissed rather than assumed.
"""

from __future__ import annotations

import struct
import sys
from collections import Counter, defaultdict

from pcap_conversations import UDP, Pcapng
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

JOINER = "10.2.0.2:5730"
SERVER = "99.66.131.177:5731"


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

    bases: dict[str, int] = {}
    for _, local, _, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[local] = struct.unpack_from("<II", plain, 6)[1]
    key = (bases[JOINER] ^ bases[SERVER]) & 0xFFFFFFFF

    sent: dict[str, list[tuple[float, int, int, int]]] = {SERVER: [], JOINER: []}
    for ts, local, remote, payload in datagrams:
        counter, plain, verified = decode(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for message_type, length, flags, body in parse_messages(content):
            sent[local].append((ts, message_type, length, flags))

    server_counts = Counter(t for _, t, _, _ in sent[SERVER])
    joiner_counts = Counter(t for _, t, _, _ in sent[JOINER])
    print(f"session key 0x{key:08x}\n")

    print("=== low byte, server 0x1x.. count, joiner 0x5x.. count ===")
    print(f"{'low':>5} {'server':>8} {'joiner':>8} {'ratio':>7}")
    lows = sorted({t & 0xFF for t in server_counts if 0x1000 <= t < 0x2000} |
                  {t & 0xFF for t in joiner_counts if 0x5000 <= t < 0x6000})
    for low in lows:
        server_total = sum(c for t, c in server_counts.items() if 0x1000 <= t < 0x2000 and t & 0xFF == low)
        joiner_total = sum(c for t, c in joiner_counts.items() if 0x5000 <= t < 0x6000 and t & 0xFF == low)
        if not server_total and not joiner_total:
            continue
        ratio = joiner_total / server_total if server_total else float("inf")
        print(f"0x{low:02x} {server_total:8d} {joiner_total:8d} {ratio:7.2f}")

    # Does the answer follow its request? For each server record, look for the
    # matching joiner record in the window after it.
    print("\n=== answer latency: joiner 0x5x(low) after a server 0x1x(low) ===")
    server_times: dict[int, list[float]] = defaultdict(list)
    for ts, message_type, _, _ in sent[SERVER]:
        if 0x1000 <= message_type < 0x2000:
            server_times[message_type & 0xFF].append(ts)
    joiner_times: dict[int, list[float]] = defaultdict(list)
    for ts, message_type, _, _ in sent[JOINER]:
        if 0x5000 <= message_type < 0x6000:
            joiner_times[message_type & 0xFF].append(ts)

    for low in sorted(set(server_times) & set(joiner_times)):
        requests = server_times[low]
        answers = joiner_times[low]
        latencies = []
        matched = 0
        for request in requests:
            following = [a for a in answers if 0 <= a - request <= 1.0]
            if following:
                matched += 1
                latencies.append(min(following) - request)
        if not requests:
            continue
        latencies.sort()
        median = latencies[len(latencies) // 2] if latencies else float("nan")
        print(
            f"  low 0x{low:02x}: {matched}/{len(requests)} requests answered within 1s, "
            f"median latency {median * 1000:.1f} ms"
        )

    # Direction check: which side actually leads? Test both orders rather than
    # assuming the server asks and the joiner answers.
    print("\n=== who leads? server 0x1x(low) after joiner 0x5x(low) ===")
    for low in sorted(set(server_times) & set(joiner_times)):
        requests = joiner_times[low]
        answers = server_times[low]
        matched = 0
        latencies = []
        for request in requests:
            following = [a for a in answers if 0 <= a - request <= 1.0]
            if following:
                matched += 1
                latencies.append(min(following) - request)
        if not requests:
            continue
        latencies.sort()
        median = latencies[len(latencies) // 2] if latencies else float("nan")
        print(
            f"  low 0x{low:02x}: {matched}/{len(requests)} joiner records followed within 1s, "
            f"median {median * 1000:.1f} ms"
        )

    print("\n=== interleaving: first and last occurrence per low byte ===")
    for low in sorted(set(server_times) & set(joiner_times)):
        s, j = server_times[low], joiner_times[low]
        print(
            f"  low 0x{low:02x}: server first {s[0]:.3f} last {s[-1]:.3f} | "
            f"joiner first {j[0]:.3f} last {j[-1]:.3f}"
        )


if __name__ == "__main__":
    main()
