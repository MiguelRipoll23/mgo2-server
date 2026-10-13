#!/usr/bin/env python3
"""List the records of an MGO2 UDP peer session in a capture, in wire order.

Usage:
    python3 tools/udp_session_records.py mgo2-game1.pcapng [--peer IP:PORT]
        [--from SECONDS] [--to SECONDS] [--id 0x1a54] [--flags2] [--limit N]

Why it exists: the gameplay host writes its match records by replaying a capture,
and every remaining literal in `src/GameplayServer/MatchRecords/` is a value the
capture carries. A reader who wants to know whether a byte is a constant of the
layout, a per-session counter or a fact about a match has to see that byte in
every record of every capture, which is what this prints.

It is a listing and not an interpretation: the records come out of
`udp_frame.parse_messages`, so a frame whose tail digest does not verify is
counted as unreadable rather than parsed, and nothing here names a record.

`--flags2` prints only the fourth byte of every session record of the chosen
direction, in order, which is the measurement behind the sequence question: a
counter that runs across the session is visible as one monotone run, and a
per-record constant is not.
"""

from __future__ import annotations

import struct
import sys
from collections import defaultdict

from pcap_conversations import Pcapng, UDP
from udp_frame import PRE_KEY, decode_frame, handshake_of


def endpoint(address: str, port: int) -> str:
    return f"{address}:{port}"


def collect(path: str):
    """Every datagram of the capture, and each flow's two counter bases."""
    capture = Pcapng(path)
    capture.read()
    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP or len(payload) < 16:
            continue
        # STUN shares the client's p2p port; it has no 2-byte scramble that fits.
        if payload[0:2] == b"\x00\x01" and payload[2:4] == b"\x00\x00":
            continue
        datagrams.append((ts, endpoint(source, source_port), endpoint(destination, destination_port), payload))

    bases: dict[tuple[str, str], dict[str, int]] = defaultdict(dict)
    joiners: dict[frozenset, str] = {}
    for _, local, remote, payload in datagrams:
        handshake = handshake_of(payload)
        if handshake is None:
            continue
        _, counter_base = handshake
        if local != remote and frozenset((local, remote)) not in joiners:
            joiners[frozenset((local, remote))] = local
        bases[(local, remote)][remote] = counter_base
        bases[(remote, local)][remote] = counter_base

    # A capture's timestamps are wall-clock and two captures share no origin, so a
    # window is only meaningful against the flow's own first datagram.
    origins: dict[frozenset, float] = {}
    for ts, local, remote, _ in datagrams:
        pair = frozenset((local, remote))
        if pair not in origins:
            origins[pair] = ts
    return datagrams, bases, joiners, origins


def key_for(bases, local: str, remote: str) -> int:
    """The session key for a flow, or the pre-key constant when neither base is seen.

    A flow whose handshake the capture missed is decoded with the pre-handshake
    constant, the way `udp_frame` does: a host is observed that keys a whole live
    session with it, and a reader that skipped such a flow would report the
    capture as having no records rather than as being unreadable.
    """
    own_base = bases[(local, remote)].get(local)
    peer_base = bases[(local, remote)].get(remote)
    if own_base is None or peer_base is None:
        return PRE_KEY
    return (own_base ^ peer_base) & 0xFFFFFFFF


def parse(content: bytes):
    """Walk the records of a content region; the two framings of udp_frame.parse_messages.

    Duplicated rather than imported because this tool needs the length byte as
    well, which `parse_messages` folds into the body it returns.
    """
    records = []
    offset = 0
    end = len(content)
    while offset + 4 <= end:
        message_type = struct.unpack_from("<H", content, offset)[0]
        length = content[offset + 2]
        flags2 = content[offset + 3]
        body_length = length + (-1 if message_type < 0x1000 else 0)
        if body_length < 0 or offset + 4 + body_length > end:
            break
        body = content[offset + 4 : offset + 4 + body_length]
        records.append((message_type, length, flags2, body))
        offset += 4 + body_length
    return records


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return
    path = sys.argv[1]

    def option(name, default=None):
        return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default

    only = option("--id")
    only = int(only, 16) if only else None
    since = float(option("--from", "0"))
    until = float(option("--to", "1e9"))
    limit = int(option("--limit", "100000"))
    peer_filter = option("--peer")
    flags2_only = "--flags2" in sys.argv

    datagrams, bases, joiners, origins = collect(path)
    for flow, joiner in joiners.items():
        if len(flow) != 2:
            continue
        host = next(end for end in flow if end != joiner)
        print(
            f"session {joiner} (joiner) <-> {host} (host): OUT = sent by {host} "
            f"(t+0 = {origins[flow]:.3f})"
        )

    if flags2_only:
        # The host's own session records, in order: (time, id, length, fourth byte).
        print(f"{'time':>10}  {'id':>6} {'len':>4} {'4th':>4}")
        last = None
        for ts, local, remote, payload in datagrams:
            pair = frozenset((local, remote))
            at = ts - origins.get(pair, ts)
            if at < since or at > until:
                continue
            if peer_filter and peer_filter not in (local, remote):
                continue
            joiner = joiners.get(pair)
            if joiner is None or local == joiner:
                continue
            counter, plain, verified, _ = decode_frame(payload, key_for(bases, local, remote))
            if not verified:
                continue
            content = plain[2 : len(plain) - 0xA]
            if counter & 0x8000:
                from udp_frame import lzss_decompress

                content = lzss_decompress(content)
            for message_type, length, flags2, _ in parse(content):
                if message_type < 0x1000 or flags2 == 0:
                    continue
                mark = ""
                if last is not None and flags2 != last + 1:
                    mark = f"  <- was 0x{last:02x}"
                print(f"{at:10.3f}  0x{message_type:04x} {length:4d} 0x{flags2:02x}{mark}")
                last = flags2
        return

    shown = 0
    for ts, local, remote, payload in datagrams:
        pair = frozenset((local, remote))
        at = ts - origins.get(pair, ts)
        if at < since or at > until:
            continue
        if peer_filter and peer_filter not in (local, remote):
            continue
        counter, plain, verified, pre_keyed = decode_frame(payload, key_for(bases, local, remote))
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        compressed = bool(counter & 0x8000)
        if compressed:
            from udp_frame import lzss_decompress

            content = lzss_decompress(content)
        records = parse(content)
        joiner = joiners.get(pair)
        direction = "-- " if joiner is None else ("IN " if local == joiner else "OUT")
        if only is not None and not any(record[0] == only for record in records):
            continue
        print(
            f"[{at:10.3f}] {direction} {local} -> {remote} "
            f"hdr=0x{counter:04x}{' LZSS' if compressed else ''} "
            f"key={'pre' if pre_keyed else 'session'}"
        )
        for message_type, length, flags2, body in records:
            if only is not None and message_type != only:
                continue
            text = "".join(chr(b) if 32 <= b < 127 else "." for b in body[:24])
            print(
                f"      type=0x{message_type:04x} len={length:3d} class=0x{flags2:02x} "
                f"body={body[:24].hex(' ')}{f'  {text}' if text.strip('.') else ''}"
            )
        shown += 1
        if shown >= limit:
            break


if __name__ == "__main__":
    main()
