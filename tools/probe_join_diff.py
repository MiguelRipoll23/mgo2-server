#!/usr/bin/env python3
"""Compare our host's roster entry with the captured dedicated server's.

Usage:
    python3 tools/probe_join_diff.py <capture> [--joiner A] [--server B]

The live capture and the reference capture hold the same two records at join --
the host's own entry under 0x9001 and the joiner's under 0x1001 -- so the two
can be set beside each other field by field. That is the comparison that says
what the client is actually being told about us, which a log line cannot.

Full bodies are printed; a truncated hex string hides the fields past the cut,
which is exactly where a roster record's name lives.
"""

from __future__ import annotations

import struct
import sys

from pcap_conversations import UDP, Pcapng
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages

DEFAULT_JOINER = "10.2.0.2:5730"
DEFAULT_SERVER = "99.66.131.177:5731"

FIELDS = [
    (0x00, "record version"),
    (0x01, "sub-type"),
    (0x04, "roster index field"),
    (0x05, "per-player value"),
    (0x07, "per-player value (2)"),
    (0x08, "character id (u32)"),
    (0x0C, "block marker pair"),
    (0x0D, "block marker pair (2)"),
    (0x10, "team?"),
    (0x12, "block constant"),
    (0x43, "name marker"),
]


def collect(path: str, joiner: str, server: str):
    capture = Pcapng(path)
    capture.read()
    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in capture.decoded_packets():
        if protocol != UDP:
            continue
        local = f"{source}:{source_port}"
        remote = f"{destination}:{destination_port}"
        if {local, remote} != {joiner, server}:
            continue
        datagrams.append((ts, local, payload))
    datagrams.sort(key=lambda row: row[0])

    bases = {}
    for _, local, payload in datagrams:
        if len(payload) != 44:
            continue
        _, plain, _ = decode(payload, PRE_KEY)
        if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
            continue
        bases[local] = struct.unpack_from("<II", plain, 6)[1]
    if joiner not in bases or server not in bases:
        return []
    key = (bases[joiner] ^ bases[server]) & 0xFFFFFFFF

    records = []
    for ts, local, payload in datagrams:
        if local != server or len(payload) == 44:
            continue
        _, plain, verified = decode(payload, key)
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if len(content) and content[0] & 0x80:
            pass
        counter = struct.unpack_from("<H", plain, 0)[0]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for message_type, _, flags, body in parse_messages(content):
            if message_type == 0x9001 and len(body) > 40 and body[0] == 0x07:
                records.append((ts, message_type, flags, body))
                if len(records) >= 1:
                    return records
    return records


def main() -> None:
    path = sys.argv[1]
    joiner = sys.argv[sys.argv.index("--joiner") + 1] if "--joiner" in sys.argv else DEFAULT_JOINER
    server = sys.argv[sys.argv.index("--server") + 1] if "--server" in sys.argv else DEFAULT_SERVER

    found = collect(path, joiner, server)
    if not found:
        print(f"{path}: no 0x9001 host entry decoded")
        return
    _, message_type, flags, body = found[0]

    print(f"{path}")
    print(f"host entry: type=0x{message_type:04x} flags2=0x{flags:02x} length={len(body)}\n")
    for start in range(0, len(body), 16):
        chunk = body[start : start + 16]
        print(f"  0x{start:02x}: {chunk.hex()}")
    print()
    for offset, label in FIELDS:
        if offset + 4 <= len(body) and offset == 0x08:
            value = int.from_bytes(body[offset : offset + 4], "little")
            print(f"  0x{offset:02x} {label:<22} = {value} (0x{value:08x})")
        elif offset < len(body):
            print(f"  0x{offset:02x} {label:<22} = {body[offset]}")
        else:
            print(f"  0x{offset:02x} {label:<22} = ABSENT (record is {len(body)} bytes)")


if __name__ == "__main__":
    main()