#!/usr/bin/env python3
"""Inventory one p2p flow of a capture with per-record fourth bytes.

Usage:
    python3 tools/probe_ack_compare.py <capture.pcapng> [joiner-ip:port] [--key 0x...]

Auto-discovers the counter bases from the pre-keyed handshakes so the session key
can be derived, then decodes every frame of the flow and prints the record
inventory and the fourth-byte behaviour of the session records. A capture whose
host replies with a frame that carries no `0x1000` record and keys its own
direction pre-keyed — `mgo2-game2.pcapng` and `mgo2-game3.pcapng` — exposes only
the joiner's base, so its key has to be passed with `--key`.
"""
from __future__ import annotations

import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402


def main() -> None:
    path = sys.argv[1]
    joiner = sys.argv[2] if len(sys.argv) > 2 else "10.2.0.2:5730"

    capture = Pcapng(path)
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        if 3478 in (source_port, destination_port):
            continue
        datagrams.append(
            (ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload)
        )

    # Discover handshake counter bases per peer pair. A handshake is any
    # pre-keyed frame carrying a 0x1000 record; its body is 16 bytes (no
    # endpoints, count 0) here rather than the reference's 28, so length is not
    # a discriminator.
    bases: dict[frozenset, dict[str, int]] = defaultdict(dict)
    identities: dict[frozenset, dict[str, tuple]] = defaultdict(dict)
    for _, local, remote, payload in datagrams:
        if len(payload) < 22:
            continue
        counter, plain, verified = decode(payload, PRE_KEY)
        if not verified or struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        peer_id, counter_base = struct.unpack_from("<II", plain, 6)
        magic = struct.unpack_from("<I", plain, 14)[0]
        pair = frozenset((local, remote))
        bases[pair][local] = counter_base
        identities[pair][local] = (peer_id, counter_base, plain[18], magic, len(payload))

    print(f"=== {path}: {len(datagrams)} datagrams; handshake bases by pair ===")
    for pair, sides in identities.items():
        print(f"  pair {sorted(pair)}")
        for side, (peer_id, base, ver, magic, length) in sides.items():
            print(f"    {side:24} peer_id={peer_id:<8} base=0x{base:08x} cap={ver} len={length} magic=0x{magic:08x}")
        vals = list(bases[pair].values())
        if len(vals) == 2:
            print(f"    key K = 0x{vals[0] ^ vals[1]:08x}")
    print()

    # A key may be given outright, because not every capture exposes both
    # counter bases: the host of mgo2-game2/3 replies with a 31-byte handshake
    # frame that carries no 0x1000 record, and keys its own direction with the
    # bare pre-key constant, so only the joiner's base is discoverable.
    override = None
    if "--key" in sys.argv:
        override = int(sys.argv[sys.argv.index("--key") + 1], 16)

    # Pick the flow that has both handshakes, or the joiner's pair when a key
    # was supplied.
    chosen = None
    for pair, sides in bases.items():
        if joiner not in pair:
            continue
        if len(sides) == 2 or override is not None:
            chosen = pair
            break
    if chosen is None:
        print("no pair with both handshakes")
        return
    left, right = sorted(chosen)
    key = override if override is not None else bases[chosen][left] ^ bases[chosen][right]
    print(f"=== chosen flow {left} <-> {right}, K=0x{key:08x} ===")

    records = []
    unreadable = Counter()
    t0 = None
    for ts, local, remote, payload in datagrams:
        if frozenset((local, remote)) != chosen:
            continue
        if len(payload) < 16:
            continue
        counter, plain, verified = decode(payload, key)
        if not verified:
            # handshakes are pre-keyed
            counter, plain, verified = decode(payload, PRE_KEY)
            if not verified:
                unreadable[local] += 1
                continue
        if t0 is None:
            t0 = ts
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            try:
                content = lzss_decompress(content)
            except Exception:
                pass
        for (tp, ln, fl, body) in parse_messages(content):
            records.append((round(ts - t0, 3), "J" if local == joiner else "H", counter, tp, ln, fl, bytes(body)))

    print(f"records: {len(records)}; unreadable: {dict(unreadable)}")

    # Inventory per direction and type.
    by_dir_type = defaultdict(Counter)
    for rel, d, c, tp, ln, fl, body in records:
        by_dir_type[d][tp] += 1
    for d in ("J", "H"):
        print(f"\n--- direction {d} inventory ---")
        for tp, n in sorted(by_dir_type[d].items()):
            print(f"  0x{tp:04x} x{n}")

    # 0x5001 fourth bytes through time.
    for d in ("J", "H"):
        seq = [(rel, fl, ln, body) for rel, dd, c, tp, ln, fl, body in records if dd == d and tp == 0x5001]
        print(f"\n--- 0x5001 direction {d}: n={len(seq)} ---")
        print("  first 40 (t, 4th, len):", [(round(t, 2), fl, ln) for t, fl, ln, _ in seq[:40]])
        vals = [fl for _, fl, _, _ in seq]
        if vals:
            print(f"  min={min(vals)} max={max(vals)} distinct={len(set(vals))}")

    # Ordered timeline of the session records, to see who answers whom.
    print("\n=== ordered session records for t<=45 (t, dir, type, len, 4th) ===")
    for rel, d, c, tp, ln, fl, body in records:
        if rel > 45:
            break
        if tp >= 0x1000:
            print(f"  {rel:8.2f} {d} 0x{tp:04x} len={ln:<4} b4={fl}")

    # First 60 records with a small body listing, to see the opening exchange.
    print("\n=== first 60 records (t, dir, frame hdr, type, len, 4th) ===")
    for rel, d, c, tp, ln, fl, body in records[:60]:
        print(f"  t={rel:8.3f} {d} hdr=0x{c:04x} type=0x{tp:04x} len={ln:<4} b4={fl:<3} body={body[:8].hex(' ')}")

    # Small session records (len<=2) timeline with fourth bytes for both dirs.
    print("\n=== one-byte-body session records, by (dir,type), fourth-byte series ===")
    series = defaultdict(list)
    for rel, d, c, tp, ln, fl, body in records:
        if tp >= 0x1000 and ln == 1:
            series[(d, tp)].append((round(rel, 2), fl))
    for k in sorted(series):
        vals = series[k]
        print(f"  {k[0]} 0x{k[1]:04x} n={len(vals)} first20={vals[:20]}")


if __name__ == "__main__":
    main()
