#!/usr/bin/env python3
"""Parse a pcapng capture without tshark and report per-flow statistics.

Usage:
    python3 tools/pcap_conversations.py docs/mgo2-game.pcapng

Prints one line per direction group: protocol, endpoints, packet count, byte count
and the time span, ordered by traffic volume. Pure stdlib - no libpcap, no tshark.
"""

from __future__ import annotations

import struct
import sys
from collections import defaultdict

LINKTYPE_NULL = 0
LINKTYPE_ETHERNET = 1
LINKTYPE_RAW = 101
LINKTYPE_LINUX_SLL = 113
LINKTYPE_LOOP = 108

UDP = 17
TCP = 6


def ReadTimestampResolution(interface_description: bytes) -> float:
    """Ticks per second, from the interface's ``if_tsresol`` option.

    The option is one byte: the high bit picks a power of two over a power of
    ten, and the low seven bits are the exponent. Default is microseconds when
    the option is absent. Assuming microseconds regardless is a 1000x error on
    a capture taken with the default of a Windows capture driver, which is
    nanoseconds.
    """
    offset = 8  # link type, reserved, snap length
    while offset + 4 <= len(interface_description):
        code, length = struct.unpack_from("<HH", interface_description, offset)
        if code == 0:
            break
        if code == 9 and length >= 1:
            raw = interface_description[offset + 4]
            exponent = raw & 0x7F
            return float(1 << exponent) if raw & 0x80 else float(10**exponent)
        offset += 4 + (length + 3) // 4 * 4
    return 1_000_000.0


class Pcapng:
    """Minimal pcapng reader: link type plus timestamped packet bytes."""

    def __init__(self, path: str) -> None:
        self.path = path
        self.link_type = LINKTYPE_ETHERNET
        self.ticks_per_second = 1_000_000
        self.packets: list[tuple[float, bytes]] = []

    def read(self) -> None:
        with open(self.path, "rb") as handle:
            data = handle.read()

        offset = 0
        while offset + 12 <= len(data):
            block_type, block_length = struct.unpack_from("<II", data, offset)
            if block_length < 12 or offset + block_length > len(data):
                break
            body = data[offset + 8 : offset + block_length - 4]
            self._handle_block(block_type, body)
            offset += block_length

    def _handle_block(self, block_type: int, body: bytes) -> None:
        if block_type == 0x0A0D0D0A:  # section header
            pass
        elif block_type == 0x00000001:  # interface description
            if len(body) >= 2:
                link_type, _, _ = struct.unpack_from("<HHI", body, 0)
                self.link_type = link_type
            self.ticks_per_second = ReadTimestampResolution(body)
        elif block_type == 0x00000006:  # enhanced packet block
            interface_id, ts_high, ts_low, captured, original = struct.unpack_from(
                "<IIIII", body, 0
            )
            del interface_id, original
            ts = ((ts_high << 32) | ts_low) / self.ticks_per_second
            packet = body[20 : 20 + captured]
            self.packets.append((ts, packet))
        elif block_type == 0x00000003:  # simple packet block
            original = struct.unpack_from("<I", body, 0)[0]
            self.packets.append((0.0, body[4 : 4 + min(original, len(body) - 4)]))

    def decoded_packets(self):
        """Yield (timestamp, ip_version, protocol, source, destination, payload)."""
        for ts, frame in self.packets:
            link = self.link_type
            if link == LINKTYPE_ETHERNET:
                if len(frame) < 14:
                    continue
                ether_type = struct.unpack_from(">H", frame, 12)[0]
                offset = 14
                while ether_type in (0x8100, 0x88A8) and len(frame) >= offset + 4:
                    ether_type = struct.unpack_from(">H", frame, offset + 2)[0]
                    offset += 4
                if ether_type == 0x0800:
                    frame = frame[offset:]
                elif ether_type == 0x86DD:
                    frame = frame[offset:]
                else:
                    continue
            elif link in (LINKTYPE_RAW, LINKTYPE_NULL):
                pass
            else:
                continue

            if not frame:
                continue
            version = frame[0] >> 4
            if version == 4:
                if len(frame) < 20:
                    continue
                header_length = (frame[0] & 0x0F) * 4
                total_length = struct.unpack_from(">H", frame, 2)[0]
                protocol = frame[9]
                source = ".".join(str(b) for b in frame[12:16])
                destination = ".".join(str(b) for b in frame[16:20])
                transport = frame[header_length:total_length]
            elif version == 6:
                if len(frame) < 40:
                    continue
                protocol = frame[6]
                source = ":".join(
                    format(int.from_bytes(frame[8 + i : 10 + i], "big"), "x") for i in range(0, 16, 2)
                )
                destination = ":".join(
                    format(int.from_bytes(frame[24 + i : 26 + i], "big"), "x")
                    for i in range(0, 16, 2)
                )
                transport = frame[40:]
            else:
                continue

            if protocol == TCP and len(transport) >= 4:
                source_port, destination_port = struct.unpack_from(">HH", transport, 0)
                transport = transport[20:]
            elif protocol == UDP and len(transport) >= 8:
                source_port, destination_port = struct.unpack_from(">HH", transport, 0)
                transport = transport[8:]
            else:
                source_port = destination_port = 0

            yield ts, protocol, source, source_port, destination, destination_port, transport


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return

    capture = Pcapng(sys.argv[1])
    capture.read()
    flows: dict[tuple, list[int]] = defaultdict(lambda: [0, 0])
    for ts, protocol, source, source_port, destination, destination_port, payload in capture.decoded_packets():
        name = {TCP: "TCP", UDP: "UDP"}.get(protocol, str(protocol))
        low = (source, source_port)
        high = (destination, destination_port)
        forward = low <= high
        if forward:
            key = (name, low, high)
        else:
            key = (name, high, low)
        entry = flows[key]
        entry[0] += 1
        entry[1] += len(payload)

    print(f"{capture.path}: link type {capture.link_type}, {len(capture.packets)} packets")
    print(f"{'proto':6} {'endpoints':46} {'pkts':>7} {'bytes':>10}")
    for (name, low, high), (count, byte_count) in sorted(
        flows.items(), key=lambda item: item[1][1], reverse=True
    ):
        endpoints = f"{low[0]}:{low[1]} <-> {high[0]}:{high[1]}"
        print(f"{name:6} {endpoints:46} {count:7} {byte_count:10}")


if __name__ == "__main__":
    main()