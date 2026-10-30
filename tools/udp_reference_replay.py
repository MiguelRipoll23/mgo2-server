#!/usr/bin/env python3
"""Replay a captured client against a live gameplay server and diff the answer.

Usage:
    python3 tools/udp_reference_replay.py mgo2-game1.pcapng \
        --server 127.0.0.1:5336 --bind 127.0.0.1:5400 \
        [--seconds 20] [--speed 4] [--listen 8]

The joiner's datagrams are taken from the capture and re-keyed for the live
session, so the live host is shown the bytes the recorded host was shown. Both
sides are then decoded the same way and compared record by record: the
reference host's own stream as the capture holds it, and the live server's
answer to the same frames.

The comparison is per identifier and per body, counted, because the bodies are
what a host has to write: a count difference says the phase is wrong, a body
difference beside a matching count says the record is.
"""

from __future__ import annotations

import argparse
import select
import socket
import struct
import sys
import time
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402
from udp_join_send import encode, rewrite_handshake_pairs  # noqa: E402

TAIL = 0xA
HEADER = 2


def records_of(counter: int, plain: bytes) -> list[tuple[int, int, int, bytes]]:
    """The records of a decoded frame, decompressed when its marker says so."""
    content = plain[HEADER : len(plain) - TAIL]
    if counter & 0x8000:
        content = lzss_decompress(content)
    return parse_messages(content)


def captured_join(path: str):
    """The captured client's datagrams, re-decodable for a live session.

    The joiner is the side whose handshake presents mode 2 — every captured
    client does and every host presents 1 — so no endpoint is assumed: the
    captures on the bench were recorded on several different ports.
    """
    recording = Pcapng(path)
    recording.read()
    datagrams = [
        (ts, f"{source}:{source_port}", payload)
        for ts, protocol, source, source_port, _, _, payload in recording.decoded_packets()
        if protocol == UDP
    ]
    datagrams.sort(key=lambda row: row[0])

    handshakes: dict[str, tuple[int, bytes]] = {}
    for _, local, payload in datagrams:
        if local in handshakes or len(payload) < 24:
            continue
        _, plain, verified = decode(payload, PRE_KEY)
        if verified and struct.unpack_from("<H", plain, 2)[0] == 0x1000:
            handshakes[local] = (struct.unpack_from("<II", plain, 6)[1], plain)

    if len(handshakes) < 2:
        raise SystemExit(f"no handshake pair found in {path}")

    joiner = next((key for key, (_, plain) in handshakes.items() if plain[19] == 2), None)
    if joiner is None:
        joiner = next(iter(handshakes))
    host = next(key for key in handshakes if key != joiner)
    # The captures on the bench were recorded by two client builds, and the
    # handshake body is not laid out the same in both: a 32-byte one carries its
    # base elsewhere. The key is therefore chosen by which candidate verifies
    # the most of the client's own frames, not by assuming the layout.
    # Zero is on the list because two of the bench's recordings were keyed with
    # it: their handshakes carry no usable base, and their frames verify under a
    # chain key of 0 rather than under the exclusive-OR of two bases.
    candidates = [
        (handshakes[joiner][0] ^ handshakes[host][0]) & 0xFFFFFFFF,
        handshakes[joiner][0],
        handshakes[host][0],
        0,
    ]
    sample = [payload for _, local, payload in datagrams if local == joiner and len(payload) > 16][:300]
    session_key = max(candidates, key=lambda key: sum(decode(payload, key)[2] for payload in sample))

    # The offsets are the client's own from its handshake: a capture can hold
    # minutes of keep-alives before the join, and a replay that counted from the
    # first datagram would spend its whole window on them.
    start = next(
        ts
        for ts, local, payload in datagrams
        if local == joiner and len(payload) >= 24 and decode(payload, PRE_KEY)[2]
    )
    join = []
    for ts, local, payload in datagrams:
        if local != joiner or ts < start:
            continue
        pre_keyed = decode(payload, PRE_KEY)[2]
        counter, plain, verified = decode(payload, PRE_KEY if pre_keyed else session_key)
        if not verified:
            continue
        is_handshake = pre_keyed and struct.unpack_from("<H", plain, 2)[0] == 0x1000
        join.append((ts - start, counter, plain, is_handshake))
    return join, handshakes[joiner][0], session_key


def capture_streams(path: str):
    """Return (joiner key, host key, session key, host frames, joiner frames).

    The host is the sender of the second 0x1000 handshake; its frames are decoded
    with the session key its own base and the joiner's imply.
    """
    recording = Pcapng(path)
    recording.read()
    datagrams = [
        (ts, f"{source}:{source_port}", payload)
        for ts, protocol, source, source_port, _, _, payload in recording.decoded_packets()
        if protocol == UDP
    ]

    bases: dict[str, int] = {}
    order: list[str] = []
    modes: dict[str, int] = {}
    for _, local, payload in datagrams:
        _, plain, verified = decode(payload, PRE_KEY)
        if verified and struct.unpack_from("<H", plain, 2)[0] == 0x1000 and local not in bases:
            bases[local] = struct.unpack_from("<II", plain, 6)[1]
            modes[local] = plain[19]
            order.append(local)

    if len(order) < 2:
        raise SystemExit(f"no handshake pair found in {path}")
    joiner = next((key for key in order if modes[key] == 2), order[0])
    host = next(key for key in order if key != joiner)
    candidates = [
        (bases[joiner] ^ bases[host]) & 0xFFFFFFFF,
        bases[joiner],
        bases[host],
        0,
    ]
    sample = [payload for _, local, payload in datagrams if local == host and len(payload) > 16][:300]
    session_key = max(candidates, key=lambda key: sum(decode(payload, key)[2] for payload in sample))

    host_frames = []
    for ts, local, payload in datagrams:
        if local != host:
            continue
        counter, plain, verified = decode(payload, session_key)
        if verified:
            host_frames.append((ts, counter, plain))
    return joiner, host, session_key, host_frames


def replay(capture: str, server: str, bind: str, seconds: float, speed: float, listen: float):
    join, joiner_base, _ = captured_join(capture)
    _, _, _, host_frames = capture_streams(capture)

    host, port = server.rsplit(":", 1)
    bind_host, bind_port = bind.rsplit(":", 1)
    bind_port = int(bind_port)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((bind_host, bind_port))
    sock.setblocking(False)
    target = (host, int(port))

    # The reference window is the captured offsets actually replayed, so both
    # sides are compared over the same stretch of the exchange.
    played = [row for row in join if row[0] <= seconds]
    window = played[-1][0] if played else 0.0
    start = host_frames[0][0] if host_frames else 0.0
    reference_host = Counter()
    for ts, counter, plain in host_frames:
        if ts - start > window:
            break
        for message_type, length, flags, body in records_of(counter, plain):
            reference_host[(message_type, flags, body)] += 1

    live = Counter()
    session_key = None
    previous = 0.0
    sent = 0
    for offset, counter, plain, is_handshake in played:
        key = PRE_KEY if is_handshake else session_key
        if key is None:
            break
        time.sleep(max(0.0, (offset - previous) / speed))
        previous = offset
        # Only the 44-byte shape states its pairs where the rewrite can reach
        # them; the 32-byte one carries no pair list this tool can point at its
        # own socket, and the live host then answers the source address.
        rewritable = is_handshake and len(plain) == 44 and 0 < plain[21] <= 3
        body = rewrite_handshake_pairs(plain, bind_host, bind_port) if rewritable else plain
        sock.sendto(encode(body, counter, key), target)
        sent += 1
        if is_handshake:
            deadline = time.monotonic() + 5.0
            while time.monotonic() < deadline:
                ready, _, _ = select.select([sock], [], [], 0.2)
                if not ready:
                    continue
                payload = sock.recv(4096)
                _, handshake_plain, verified = decode(payload, PRE_KEY)
                if not verified or struct.unpack_from("<H", handshake_plain, 2)[0] != 0x1000:
                    continue
                server_base = struct.unpack_from("<II", handshake_plain, 6)[1]
                session_key = (joiner_base ^ server_base) & 0xFFFFFFFF
                break
            if session_key is None:
                raise SystemExit("the live server did not answer the handshake")

    deadline = time.monotonic() + listen
    while time.monotonic() < deadline:
        ready, _, _ = select.select([sock], [], [], 0.2)
        if not ready:
            continue
        counter, plain, verified = decode(sock.recv(4096), session_key)
        if not verified:
            continue
        for message_type, length, flags, body in records_of(counter, plain):
            live[(message_type, flags, body)] += 1

    sock.close()
    return reference_host, live, sent, window


def by_id(tally: Counter) -> Counter:
    """Record count per identifier, over both classes."""
    counts: Counter = Counter()
    for (message_type, _flags, _body), count in tally.items():
        counts[message_type] += count
    return counts


def bodies_by_id(tally: Counter) -> dict[tuple[int, int], Counter]:
    """The distinct bodies of each (identifier, class), counted."""
    grouped: dict[tuple[int, int], Counter] = {}
    for (message_type, flags, body), count in tally.items():
        grouped.setdefault((message_type, flags), Counter())[body] += count
    return grouped


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--server", default="127.0.0.1:5336")
    parser.add_argument("--bind", default="127.0.0.1:5400")
    parser.add_argument("--seconds", type=float, default=20.0, help="captured seconds to replay")
    parser.add_argument("--speed", type=float, default=4.0)
    parser.add_argument("--listen", type=float, default=8.0)
    parser.add_argument("--limit", type=int, default=6)
    arguments = parser.parse_args()

    print(f"replaying {arguments.capture} at {arguments.server}")
    reference, live, sent, window = replay(
        arguments.capture,
        arguments.server,
        arguments.bind,
        arguments.seconds,
        arguments.speed,
        arguments.listen,
    )
    print(
        f"  {sent} datagrams sent, captured window {window:.1f} s, "
        f"reference {sum(reference.values())} records, live {sum(live.values())}"
    )

    reference_ids, live_ids = by_id(reference), by_id(live)
    print("\n  identifier                     ref   live")
    for identifier in sorted(set(reference_ids) | set(live_ids)):
        print(f"    0x{identifier:04x}  {reference_ids[identifier]:8d} {live_ids[identifier]:6d}")

    reference_bodies, live_bodies = bodies_by_id(reference), bodies_by_id(live)
    print("\n  bodies, per (identifier, class):")
    for key in sorted(set(reference_bodies) | set(live_bodies)):
        old, new = reference_bodies.get(key, Counter()), live_bodies.get(key, Counter())
        shared = sum((old & new).values())
        print(f"    0x{key[0]:04x} class={key[1]}: ref {sum(old.values())}, live {sum(new.values())}, same-body {shared}")
        for body, count in old.most_common(arguments.limit):
            mark = "also live" if body in new else "MISSING in live"
            print(f"      ref  {count:5d} x {body[:32].hex()}{'…' if len(body) > 32 else ''}  {mark}")
        for body, count in new.most_common(arguments.limit):
            if body not in old:
                print(f"      live {count:5d} x {body[:32].hex()}{'…' if len(body) > 32 else ''}  NOT IN REF")


if __name__ == "__main__":
    main()
