#!/usr/bin/env python3
"""Replay a captured join against a live gameplay server and print its answer.

Usage:
    python3 tools/udp_replay_join.py [--capture docs/mgo2-game.pcapng]
                                     [--server 127.0.0.1:5730] [--listen 6.0]

The joiner's frames are taken from the reference capture and re-keyed for the
live session, so the host is shown exactly the bytes the recorded host was
shown. Only the encryption changes: the live server announces its own counter
base, so the session key differs and every frame has to be re-encoded. The
bodies, the record order and the compression are the captured ones.

What the host answers is decoded and printed beside nothing else, which is the
point: this is the first place the implemented roster is compared against the
recorded roster rather than against a reconstruction of it.
"""

from __future__ import annotations

import argparse
import socket
import struct
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    BASE_DERIVE,
    LCG,
    PRE_KEY,
    decode,
    hexdump,
    lzss_decompress,
    parse_messages,
    scramble_header,
)


def chain(frame: bytearray, counter: int, key: int) -> None:
    """Apply the plaintext-feedback XOR chain, the exact inverse of dechain()."""
    seed = (((counter & 0x7FFF) * LCG + 1) & 0xFFFFFFFF) ^ key
    previous = 0
    position = 2
    remaining = len(frame) - 2 - 10
    while remaining > 0:
        seed = (seed + previous) & 0xFFFFFFFF
        count = min(4, remaining)
        word = int.from_bytes(frame[position : position + count], "little")
        result = word ^ (seed & ((1 << (8 * count)) - 1))
        frame[position : position + count] = result.to_bytes(count, "little")
        previous = word
        position += count
        remaining -= count


def encode(plain: bytes, counter: int, key: int) -> bytes:
    """Scramble a plaintext frame (zeroed tail) into wire bytes."""
    frame = bytearray(plain)
    chain(frame, counter, key)

    # A pre-keyed frame digests with the bare constant and not with the
    # pre-handshake chain key: only a session-keyed frame gets the key
    # exclusive-ORed in (udp_frame.digest_key_for, and
    # FrameCryptoUtility.ComputeTailDigest on the sender side). Keying the
    # digest on PRE_KEY here produced frames no decoder accepts, which is why
    # the replay never got past the host's first gate.
    digest_key = BASE_DERIVE if key == PRE_KEY else (key ^ BASE_DERIVE) & 0xFFFFFFFF

    import hashlib

    seed = struct.pack("<I", digest_key)
    first = hashlib.md5(seed + bytes(frame[:-10])).digest()
    second = hashlib.md5(seed + first).digest()
    frame[-10:] = bytes(
        second[index] ^ (second[10 + index] if index < 6 else 0) for index in range(10)
    )

    scramble_header(frame, counter)
    return bytes(frame)


def capture_joiner_frames(path: str) -> tuple[bytes, int, list[tuple[int, bytes]]]:
    """Return the joiner's handshake, the captured session key, and its frames.

    Each frame is returned as (captured counter, plaintext datagram) already
    decoded under the capture's own session key, because only the encryption
    changes for the live replay.
    """
    capture = Pcapng(path)
    capture.read()

    datagrams = [
        (source, source_port, payload)
        for _, protocol, source, source_port, _, _, payload in capture.decoded_packets()
        if protocol == UDP
    ]

    # The joiner is the side that sends the first 44-byte handshake; the host
    # answers one. Ordering is the only way to tell them apart.
    joiner = None
    server_base = None
    for source, source_port, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        if joiner is None:
            joiner = (source, source_port)
            continue
        if joiner != (source, source_port):
            _, base = struct.unpack_from("<II", plain, 6)
            server_base = base
            break

    if joiner is None or server_base is None:
        raise SystemExit(f"no handshake pair found in {path}")

    handshake = None
    joiner_base = None
    frames: list[tuple[int, bytes]] = []
    session_key = joiner_base if joiner_base is not None else None

    for source, source_port, payload in datagrams:
        if (source, source_port) != joiner or len(payload) < 16:
            continue
        if len(payload) == 44:
            if handshake is None:
                handshake = payload
                _, joiner_base = struct.unpack_from("<II", decode(payload, PRE_KEY)[1], 6)
                session_key = joiner_base ^ server_base
            continue
        if payload[0:2] == b"\x00\x01":
            continue
        counter, plain, _ = decode(payload, session_key)
        frames.append((counter, plain))

    return handshake, session_key, frames


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", default="docs/mgo2-game.pcapng")
    parser.add_argument("--server", default="127.0.0.1:5730")
    parser.add_argument("--listen", type=float, default=6.0, help="seconds to listen after the join")
    parser.add_argument("--frames", type=int, default=2, help="joiner frames to replay")
    args = parser.parse_args()

    handshake, captured_key, keyed = capture_joiner_frames(args.capture)
    if len(keyed) < args.frames:
        raise SystemExit(f"capture holds {len(keyed)} joiner frames, wanted {args.frames}")

    host, port = args.server.split(":")
    target = (host, int(port))

    joiner_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    joiner_socket.bind(("127.0.0.1", 0))
    joiner_socket.settimeout(1.0)

    print(f"replaying {args.frames} joiner frames from {args.capture} to {args.server}")
    joiner_socket.sendto(handshake, target)

    deadline = time.time() + 3.0
    reply = None
    while time.time() < deadline and reply is None:
        try:
            reply = joiner_socket.recv(4096)
        except socket.timeout:
            continue

    if reply is None:
        raise SystemExit("the host did not answer the handshake")

    _, plain, verified = decode(reply, PRE_KEY)
    peer_id, server_base = struct.unpack_from("<II", plain, 6)
    print(f"host handshake {len(reply)} B peer_id={peer_id} counter_base=0x{server_base:08x} tail={'ok' if verified else 'BAD'}")
    for index in range(2):
        offset = 16 + index * 6
        address = ".".join(str(byte) for byte in plain[offset : offset + 4])
        print(f"  pair {index}: {address}:{struct.unpack_from('<H', plain, offset + 4)[0]}")

    joiner_base = struct.unpack_from("<II", decode(handshake, PRE_KEY)[1], 6)[1]
    session_key = joiner_base ^ server_base
    print(f"session key 0x{session_key:08x}")

    # Re-encode the captured joiner frames under the live session key.
    counter = 0
    for captured_counter, plain_frame in keyed[: args.frames]:
        counter += 1
        joiner_socket.sendto(encode(plain_frame, counter, session_key), target)
        print(f"sent joiner frame counter={counter} (captured as 0x{captured_counter:04x}, {len(plain_frame)} B)")

    joiner_socket.settimeout(0.5)
    deadline = time.time() + args.listen
    while time.time() < deadline:
        try:
            data = joiner_socket.recv(4096)
        except socket.timeout:
            continue

        header_counter, plain, verified = decode(data, session_key)
        print(f"\n[{len(data)} B] counter=0x{header_counter:04x} tail={'ok' if verified else 'BAD'}")
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if header_counter & 0x8000:
            content = lzss_decompress(content)
            print(f"  LZSS -> {len(content)} bytes")
        for message_type, length, flags, body in parse_messages(content):
            print(f"  type=0x{message_type:04x} len={length} flags2={flags}")
            if body:
                print(hexdump(body))


if __name__ == "__main__":
    main()