#!/usr/bin/env python3
"""Decrypt TCP and UDP channels from an MGO2 game pcap file.

Usage:
    python3 tools/pcap_decrypt.py <pcap_file> [--port PORT] [--limit N] [--type tcp|udp|both]

Decrypts and displays:
- TCP frames: XOR + HMAC-MD5 verified, Blowfish-decrypted payloads for encrypted commands
- UDP datagrams: header scramble + chain XOR + MD5 tail verified, LZSS-decompressed content

Shows clear timestamp and IN/OUT directions for each packet.
"""

from __future__ import annotations

import argparse
import hashlib
import hmac as hmac_module
import struct
import sys
from collections import defaultdict
from pathlib import Path

# Add tools directory to path for imports
TOOLS_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS_DIR))

from pcap_conversations import TCP, UDP, Pcapng  # noqa: E402
from udp_frame import (  # noqa: E402
    BASE_DERIVE,
    PRE_KEY,
    LCG,
    decode as udp_decode,
    lzss_decompress,
    parse_messages,
    hexdump as udp_hexdump,
)

ROOT = TOOLS_DIR.parent
KEY_DIRECTORY = ROOT / "docs" / "keys"

# TCP constants from docs/keys/tcp-keys.md
TCP_HEADER_SIZE = 24
TCP_PAYLOAD_OFFSET = 0x18
TCP_CHECKSUM_OFFSET = 0x08
TCP_BLOWFISH_BLOCK_SIZE = 8
TCP_BLOWFISH_ROUNDS = 8

# Commands with Blowfish-encrypted payloads (from TCP_GAME_SERVER_PROTOCOL.md)
TCP_ENCRYPTED_COMMANDS = (0x3003, 0x4310, 0x4320, 0x43C0, 0x4700, 0x4990, 0x4910)


def tcp_xor_key() -> bytes:
    """Returns the 4-byte TCP frame XOR key."""
    return (KEY_DIRECTORY / "tcp-xor-key.bin").read_bytes()


def tcp_hmac_key() -> bytes:
    """Returns the 16-byte HMAC-MD5 key."""
    return (KEY_DIRECTORY / "tcp-hmac-md5-key.bin").read_bytes()


def blowfish_table(name: str) -> bytes:
    """Returns a pre-scheduled Blowfish table."""
    return (KEY_DIRECTORY / f"blowfish-{name}-key.bin").read_bytes()


def read_word(table: bytes, offset: int) -> int:
    """Reads a big-endian 32-bit word."""
    return struct.unpack_from(">I", table, offset)[0]


def blowfish_round(table: bytes, value: int) -> int:
    """One round of the custom Blowfish variant."""
    index_a = (value >> 22) & 0x3FC
    index_b = (value >> 14) & 0x3FC
    index_c = (value >> 6) & 0x3FC
    index_d = ((value << 2) | (value >> 30)) & 0x3FC

    first = read_word(table, index_a + 0x48)
    second = read_word(table, index_b + 0x48 + 0x400)
    third = read_word(table, index_c + 0x48 + 0x800)
    fourth = read_word(table, index_d + 0x48 + 0xC00)

    return ((((first + second) & 0xFFFFFFFF) ^ third) + fourth) & 0xFFFFFFFF


def blowfish_decrypt(data: bytes, table: bytes) -> bytes:
    """Decrypts block-aligned data with the custom Blowfish variant."""
    output = bytearray(data)
    for offset in range(0, len(output), TCP_BLOWFISH_BLOCK_SIZE):
        first = read_word(output, offset + 4)
        second = read_word(output, offset) ^ read_word(table, 0x44)

        for round_index in range(TCP_BLOWFISH_ROUNDS):
            first ^= read_word(table, 0x40 - (round_index * 8))
            first ^= blowfish_round(table, second)
            second ^= read_word(table, 0x3C - (round_index * 8))
            second ^= blowfish_round(table, first)

        first ^= read_word(table, 0x00)
        struct.pack_into(">I", output, offset, first)
        struct.pack_into(">I", output, offset + 4, second)

    return bytes(output)


def apply_xor(buffer: bytearray, key: bytes) -> None:
    """XORs a buffer with a repeating key, in place."""
    for index in range(len(buffer)):
        buffer[index] ^= key[index & (len(key) - 1)]


def decode_tcp_frame(raw: bytes) -> tuple[int, int, bytes, bool]:
    """Decode one TCP frame.

    Returns (command, sequence, payload, verified).
    """
    frame = bytearray(raw)
    apply_xor(frame, tcp_xor_key())

    command = struct.unpack_from(">H", frame, 0)[0]
    payload_length = struct.unpack_from(">H", frame, 2)[0]
    sequence = struct.unpack_from(">I", frame, 4)[0]

    if payload_length > 0x400 or payload_length == 0:
        return command, sequence, b"", False

    payload = bytes(frame[TCP_PAYLOAD_OFFSET:TCP_PAYLOAD_OFFSET + payload_length])

    # Verify HMAC-MD5 checksum
    digest_input = bytes(frame[:8]) + payload
    expected = hmac_module.new(tcp_hmac_key(), digest_input, hashlib.md5).digest()
    actual = bytes(frame[TCP_CHECKSUM_OFFSET:TCP_CHECKSUM_OFFSET + 16])
    verified = expected == actual

    # Blowfish-decrypt if needed
    if command in TCP_ENCRYPTED_COMMANDS and payload_length > 0:
        try:
            padded = payload + bytes(TCP_BLOWFISH_BLOCK_SIZE - (payload_length % TCP_BLOWFISH_BLOCK_SIZE)) if payload_length % TCP_BLOWFISH_BLOCK_SIZE else payload
            decrypted = blowfish_decrypt(padded, blowfish_table("packet"))
            payload = decrypted[:payload_length]
        except Exception:
            pass  # Leave payload as-is if decryption fails

    return command, sequence, payload, verified


def format_tcp_payload(command: int, payload: bytes) -> str:
    """Format TCP payload for display."""
    lines = []
    lines.append(f"    payload ({len(payload)} B):")

    # Try to show as text if mostly printable
    text_chars = sum(1 for b in payload if 32 <= b < 127 or b in (9, 10, 13))
    if text_chars > len(payload) * 0.7 and len(payload) < 200:
        try:
            text = payload.decode("latin-1")
            lines.append(f"    text: {text!r}")
        except Exception:
            pass

    # Always show hex
    hex_part = " ".join(f"{b:02x}" for b in payload[:64])
    if len(payload) > 64:
        hex_part += " ..."
    lines.append(f"    hex: {hex_part}")

    return "\n".join(lines)


def format_udp_content(counter: int, content: bytes, verified: bool) -> str:
    """Format UDP datagram content for display."""
    lines = []
    state = "ok" if verified else "BAD"
    lines.append(f"    tail: {state}")

    if counter & 0x8000:
        decompressed = lzss_decompress(content)
        if len(decompressed) > len(content):
            content = decompressed
            lines.append(f"    LZSS decompressed: {len(content)} bytes")

    messages = parse_messages(content)
    if messages:
        lines.append(f"    records: {len(messages)}")
        for msg_type, length, flags, body in messages:
            lines.append(f"      type=0x{msg_type:04x} len={length} flags2={flags}")
            if body and len(body) <= 64:
                hex_part = " ".join(f"{b:02x}" for b in body)
                text = "".join(chr(b) if 32 <= b < 127 else "." for b in body)
                lines.append(f"        hex: {hex_part}")
                lines.append(f"        text: {text}")
            elif body:
                lines.append(f"        hex: {udp_hexdump(body)}")
    else:
        if content:
            lines.append(f"    raw ({len(content)} B):")
            lines.append(f"    hex: {udp_hexdump(content)}")

    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("pcap_file", help="Path to pcap/pcapng file")
    parser.add_argument("--port", type=int, default=None,
                        help="Filter by TCP/UDP port (shows only this port's traffic)")
    parser.add_argument("--limit", type=int, default=100,
                        help="Maximum packets to display (default: 100)")
    parser.add_argument("--type", choices=["tcp", "udp", "both"], default="both",
                        help="Which channel to decrypt (default: both)")
    parser.add_argument("--json", action="store_true",
                        help="Output as JSON for machine parsing")

    args = parser.parse_args()

    capture = Pcapng(args.pcap_file)
    try:
        capture.read()
    except Exception as e:
        print(f"Error reading pcap: {e}", file=sys.stderr)
        sys.exit(1)

    print(f"Decrypting {args.pcap_file}: {len(capture.packets)} packets, "
          f"link type {capture.link_type}")
    print("=" * 80)

    shown = 0
    tcp_packets = []
    udp_packets = []

    # Collect packets by type
    for ts, protocol, source, source_port, destination, destination_port, payload in capture.decoded_packets():
        if args.port and source_port != args.port and destination_port != args.port:
            continue

        if protocol == TCP:
            tcp_packets.append((ts, source, source_port, destination, destination_port, payload))
        elif protocol == UDP:
            udp_packets.append((ts, source, source_port, destination, destination_port, payload))

    if args.type in ("tcp", "both"):
        print("\n=== TCP Channel (port 5732/5733) ===\n")

        for ts, source, source_port, destination, destination_port, payload in tcp_packets:
            if len(payload) < TCP_HEADER_SIZE:
                continue

            direction = "OUT" if source_port in (5732, 5733) else "IN "
            relative_ts = ts - (tcp_packets[0][0] if tcp_packets else ts)

            try:
                command, sequence, decrypted_payload, verified = decode_tcp_frame(payload)
                state = "ok" if verified else "BAD"
            except Exception as e:
                command, sequence, decrypted_payload, state = 0, 0, b"", f"error: {e}"

            if args.json:
                print(f'{{"ts": {ts:.6f}, "rel": {relative_ts:.3f}, "dir": "{direction}", '
                      f'"src": "{source}:{source_port}", "dst": "{destination}:{destination_port}", '
                      f'"cmd": "0x{command:04x}", "seq": {sequence}, "len": {len(decrypted_payload)}, '
                      f'"verify": "{state}", "payload_hex": "{decrypted_payload.hex()}"}}')
            else:
                print(f"[{relative_ts:9.3f}] {direction} {source}:{source_port} -> {destination}:{destination_port} "
                      f"({len(payload)} B)")
                print(f"    cmd=0x{command:04x} seq={sequence} payload={len(decrypted_payload)} B verify={state}")
                if decrypted_payload:
                    print(format_tcp_payload(command, decrypted_payload))

            shown += 1
            if shown >= args.limit:
                break

    if args.type in ("udp", "both"):
        print("\n=== UDP Channel (port 5730) ===\n")

        # Find session keys per peer pair
        bases = defaultdict(dict)
        for ts, source, source_port, destination, destination_port, payload in udp_packets:
            if len(payload) != 44:
                continue
            try:
                counter, plain, _ = udp_decode(payload, PRE_KEY)
                if struct.unpack_from("<H", plain, 2)[0] != 0x1000:
                    continue
                _, counter_base = struct.unpack_from("<II", plain, 6)
                local = f"{source}:{source_port}"
                remote = f"{destination}:{destination_port}"
                bases[(local, remote)][remote] = counter_base
                bases[(remote, local)][remote] = counter_base
            except Exception:
                continue

        shown_udp = 0
        for ts, source, source_port, destination, destination_port, payload in udp_packets:
            local = f"{source}:{source_port}"
            remote = f"{destination}:{destination_port}"

            # Skip STUN
            if len(payload) < 16 or (payload[0:2] == b"\x00\x01" and payload[2:4] == b"\x00\x00"):
                continue

            own_base = bases[(local, remote)].get(local)
            peer_base = bases[(local, remote)].get(remote)

            if own_base is None or peer_base is None:
                key = PRE_KEY
            else:
                key = (own_base ^ peer_base) & 0xFFFFFFFF

            direction = "OUT" if source_port == 5730 else "IN "
            relative_ts = ts - (udp_packets[0][0] if udp_packets else ts)

            try:
                counter, plain, verified = udp_decode(payload, key)
                state = "ok" if verified else "BAD"
            except Exception as e:
                counter, plain, verified, state = 0, b"", False, f"error: {e}"

            content = plain[2:len(plain) - 0xA] if len(plain) > 0xA else b""

            if args.json:
                print(f'{{"ts": {ts:.6f}, "rel": {relative_ts:.3f}, "dir": "{direction}", '
                      f'"src": "{source}:{source_port}", "dst": "{destination}:{destination_port}", '
                      f'"len": {len(payload)}, "counter": "0x{counter:04x}", '
                      f'"key": "0x{key:08x}", "verify": "{state}", '
                      f'"content_hex": "{content.hex()}"}}')
            else:
                print(f"[{relative_ts:9.3f}] {direction} {source}:{source_port} -> {destination}:{destination_port} "
                      f"({len(payload)} B)")
                print(f"    counter=0x{counter:04x} key=0x{key:08x} tail={state}")
                if content:
                    print(format_udp_content(counter, content, verified))

            shown_udp += 1
            if shown_udp >= args.limit:
                break

    if args.type == "both":
        print("\n" + "=" * 80)
        print(f"Total shown: {shown} TCP + {shown_udp} UDP packets")


if __name__ == "__main__":
    main()
