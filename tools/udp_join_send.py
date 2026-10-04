#!/usr/bin/env python3
"""Replay a captured joiner's join datagrams at a live gameplay server.

Usage:
    python3 tools/udp_join_send.py mgo2-game.pcapng \
        --host 192.168.1.15 --port 5730 --bind 192.168.1.20:5730 \
        [--frames 6] [--speed 1.0] [--listen 4]

The captured join was keyed on the captured host's counter base, so every
frame after the handshake is re-keyed here: the sender learns the host's base
from the handshake reply it gets back and rebuilds each captured plaintext
frame with the session key that base implies. The handshake itself is
pre-keyed and goes out byte for byte.

Inter-frame delays are the captured ones, scaled by --speed, because the
capture's timings are part of what the host sees. Everything the host sends
back is decoded against the session key and printed with its records.
"""

from __future__ import annotations

import argparse
import hashlib
import select
import socket
import struct
import sys
import time

from pcap_conversations import UDP, Pcapng
from udp_frame import (
    BASE_DERIVE,
    LCG,
    PRE_KEY,
    decode,
    lzss_decompress,
    parse_messages,
    scramble_header,
)

JOINER = "10.2.0.2:5730"
SERVER = "99.66.131.177:5731"
TAIL = 0xA
HEADER = 2


def digest_key_for(chain_key: int) -> int:
    """The tail digest key for a chain key.

    Pre-keyed frames digest with the bare constant and session-keyed frames
    with the session key exclusive-ORed into it, which is what the server's
    digest gate does. Note this is not the chain key: the pre-handshake chain
    key is a different constant again.
    """
    return BASE_DERIVE if chain_key == PRE_KEY else (chain_key ^ BASE_DERIVE) & 0xFFFFFFFF


def chain(frame: bytearray, counter: int, key: int, encrypt: bool) -> None:
    """The XOR chain of FrameCryptoUtility, both directions."""
    seed = (((counter & 0x7FFF) * LCG + 1) & 0xFFFFFFFF) ^ key
    previous = 0
    position = HEADER
    remaining = len(frame) - HEADER - TAIL
    while remaining > 0:
        seed = (seed + previous) & 0xFFFFFFFF
        count = min(4, remaining)
        word = int.from_bytes(frame[position : position + count], "little")
        # The word is truncated to the bytes it occupies, so the final partial
        # word of a frame keeps only its low bytes.
        result = (word ^ seed) & ((1 << (8 * count)) - 1)
        frame[position : position + count] = result.to_bytes(count, "little")
        previous = word if encrypt else result
        position += count
        remaining -= count


def tail_digest(body: bytes, digest_key: int) -> bytes:
    """The ten tail bytes, as FrameCryptoUtility.BuildTailDigest writes them."""
    key_bytes = struct.pack("<I", digest_key)
    first = hashlib.md5(key_bytes + body).digest()
    second = hashlib.md5(key_bytes + first).digest()
    return bytes(
        second[index] ^ (second[10 + index] if index < 6 else 0)
        for index in range(10)
    )


def encode(plain: bytes, counter: int, key: int) -> bytes:
    """Build a wire datagram from a plaintext frame with a zeroed tail region."""
    frame = bytearray(plain)
    frame[0] = counter & 0xFF
    frame[1] = (counter >> 8) & 0xFF
    chain(frame, counter, key, encrypt=True)
    frame[len(frame) - TAIL :] = tail_digest(bytes(frame[: len(frame) - TAIL]), digest_key_for(key))
    scramble_header(frame, counter)
    return bytes(frame)


def rewrite_handshake_pairs(plain: bytes, address: str, port: int) -> bytes:
    """Point a handshake's advertised pairs at this sender.

    The host answers the endpoint its peer advertises rather than the one the
    datagram came from (`AcceptHandshakeHandler.AdvertisedEndpointOf`), so a
    replay carrying the captured pairs would have every reply sent to
    `217.138.213.5:5730`, which is not this machine. Each pair is four address
    bytes and a 16-bit port, so rewriting them keeps the frame length - and so
    the header scramble and tail digest - exactly as captured.
    """
    frame = bytearray(plain)
    body = 6  # two-byte frame header plus the four-byte message header
    count = frame[body + 15]
    packed = bytes(int(part) for part in address.split(".")) + struct.pack("<H", port)
    for index in range(count):
        offset = body + 16 + (index * 6)
        frame[offset : offset + 6] = packed
    return bytes(frame)


def captured_join(path: str) -> tuple[list[tuple[float, int, bytes, bool]], int, int]:
    """Return the joiner's datagrams as (offset, counter, plaintext, is_handshake) plus both bases."""
    capture = Pcapng(path)
    capture.read()

    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
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
    session_key = (bases[JOINER] ^ bases[SERVER]) & 0xFFFFFFFF

    start = datagrams[0][0]
    join = []
    for ts, local, payload in datagrams:
        if local != JOINER:
            continue
        # The digest gate, not the length, decides whether a frame is pre-keyed:
        # two 44-byte frames late in the round are session-keyed and would be
        # rebuilt with the wrong key if length decided it.
        pre_keyed_counter = decode(payload, PRE_KEY)[2]
        key = PRE_KEY if pre_keyed_counter else session_key
        counter, plain, _ = decode(payload, key)
        join.append((ts - start, counter, plain, pre_keyed_counter))
    return join, bases[JOINER], bases[SERVER]


def read_handshake_base(payload: bytes) -> int | None:
    """The counter base out of a pre-keyed handshake, or None when it is not one."""
    _, plain, verified = decode(payload, PRE_KEY)
    if not verified or struct.unpack_from("<H", plain, 2)[0] != 0x1000:
        return None
    return struct.unpack_from("<II", plain, 6)[1]


def report(role: str, counter: int, plain: bytes, verified: bool) -> None:
    marker = "ok " if verified else "BAD"
    content = plain[HEADER : len(plain) - TAIL]
    kind = "compressed" if counter & 0x8000 else "plain"
    print(f"    {role:8s} {len(plain):4d} B  hdr=0x{counter:04x}  {kind:10s} tail={marker}")
    if counter & 0x8000:
        content = lzss_decompress(content)
    for message_type, length, flags, body in parse_messages(content):
        print(
            f"        type=0x{message_type:04x} len={length} flags2={flags} "
            f"body[{len(body)}]={body[:40].hex()}{'…' if len(body) > 20 else ''}"
        )


def drain(sock: socket.socket, session_key: int | None, host: tuple[str, int], deadline: float) -> int | None:
    """Print everything the host sends until the deadline; return its counter base."""
    host_base = None
    while time.monotonic() < deadline:
        ready, _, _ = select.select([sock], [], [], max(0.0, deadline - time.monotonic()))
        if not ready:
            break
        payload = sock.recv(2048)
        if payload == b"":
            break
        found = read_handshake_base(payload)
        if found is not None:
            host_base = found
            report("host", 0, decode(payload, PRE_KEY)[1], True)
            continue
        key = session_key if session_key is not None else PRE_KEY
        counter, plain, verified = decode(payload, key)
        report("host", counter, plain, verified)
    return host_base


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture")
    parser.add_argument("--host", default="192.168.1.15")
    parser.add_argument("--port", type=int, default=5730)
    parser.add_argument(
        "--bind",
        default="192.168.1.20:5740",
        help="local address and port to send from and receive replies on",
    )
    parser.add_argument("--frames", type=int, default=6, help="joiner datagrams to send")
    parser.add_argument("--speed", type=float, default=1.0, help="scale on the captured delays")
    parser.add_argument("--listen", type=float, default=4.0, help="seconds to keep listening")
    arguments = parser.parse_args()

    join, joiner_base, _ = captured_join(arguments.capture)
    if not join:
        print("no joiner datagrams in that capture")
        return

    bind_host, bind_port = arguments.bind.rsplit(":", 1)
    bind_port = int(bind_port)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((bind_host, bind_port))
    sock.setblocking(False)
    target = (arguments.host, arguments.port)

    print(
        f"replaying {min(arguments.frames, len(join))} of {len(join)} joiner datagrams\n"
        f"  from      {arguments.bind}\n"
        f"  to        {arguments.host}:{arguments.port}\n"
        f"  joiner counter base 0x{joiner_base:08x} (from the capture)"
    )

    session_key = None
    sent = 0
    previous_offset = 0.0
    for offset, counter, plain, is_handshake in join:
        if sent >= arguments.frames:
            break
        key = PRE_KEY if is_handshake else session_key
        if key is None:
            print("  host never sent a handshake; keyed frames would have no key")
            break

        body = rewrite_handshake_pairs(plain, bind_host, bind_port) if is_handshake else plain

        # The captured delays are what the host saw, so they are reproduced
        # rather than a flat pause between sends.
        time.sleep(max(0.0, (offset - previous_offset) * arguments.speed))
        previous_offset = offset

        sock.sendto(encode(body, counter, key), target)
        print(f"[+{offset * arguments.speed:7.3f}] joiner -> host  {len(plain)} B  hdr=0x{counter:04x}")
        sent += 1

        if is_handshake:
            print("    waiting up to 3s for the host's handshake reply...")
            found = drain(sock, None, target, time.monotonic() + 3.0)
            if found is None:
                print("    no handshake reply; the session key is unknown")
                return
            session_key = (joiner_base ^ found) & 0xFFFFFFFF
            print(f"    host counter base 0x{found:08x}, session key 0x{session_key:08x}")

    print(f"\nlistening {arguments.listen:.1f}s for the rest of the exchange...")
    drain(sock, session_key, target, time.monotonic() + arguments.listen)
    sock.close()


if __name__ == "__main__":
    main()