#!/usr/bin/env python3
"""Debug TCP reassembly - check for gaps, overlaps, and byte counts."""
import struct
import sys

from pcap_conversations import Pcapng, TCP

PORT = 5733


def main():
    path = sys.argv[1]
    capture = Pcapng(path)
    capture.read()

    for direction in (0, 1):  # 0 = C->S, 1 = S->C
        label = "C->S" if direction == 0 else "S->C"
        packets = []

        for ts, frame in capture.packets:
            if len(frame) < 20:
                continue
            if capture.link_type == 1:  # ETHERNET
                if len(frame) < 14:
                    continue
                eth_type = struct.unpack_from(">H", frame, 12)[0]
                offset = 14
                while eth_type in (0x8100, 0x88A8) and len(frame) >= offset + 4:
                    eth_type = struct.unpack_from(">H", frame, offset + 2)[0]
                    offset += 4
                if eth_type != 0x0800:
                    continue
                ip = frame[offset:]
            elif capture.link_type == 101:  # RAW
                ip = frame
            else:
                continue

            if len(ip) < 20:
                continue
            ip_version = ip[0] >> 4
            if ip_version != 4:
                continue
            ip_header_len = (ip[0] & 0x0F) * 4
            total_len = struct.unpack_from(">H", ip, 2)[0]
            protocol = ip[9]
            transport = ip[ip_header_len:total_len]

            if protocol != TCP:
                continue
            if len(transport) < 20:
                continue

            src_port, dst_port = struct.unpack_from(">HH", transport, 0)
            if src_port != PORT and dst_port != PORT:
                continue

            tcp_flags = transport[13]
            tcp_header_len = (transport[12] >> 4) * 4
            seq = struct.unpack_from(">I", transport, 4)[0]
            ack = struct.unpack_from(">I", transport, 8)[0]
            payload = transport[tcp_header_len:]

            if not payload:
                continue

            dir_check = 0 if dst_port == PORT else 1
            if dir_check != direction:
                continue

            packets.append((ts, seq, tcp_flags, len(payload), payload))

        if not packets:
            print(f"\n{label}: no packets")
            continue

        packets.sort(key=lambda x: x[1])  # sort by seq
        base_seq = packets[0][1]

        total_payload = sum(p[4] for _, _, _, _, p in packets)

        # Check for overlaps and gaps
        prev_end = 0
        overlaps = 0
        gaps = 0
        for ts, seq, flags, plen, payload in packets:
            rel_seq = seq - base_seq
            if rel_seq < prev_end:
                overlaps += 1
            if rel_seq > prev_end:
                gaps += 1
                # print(f"  GAP: prev_end={prev_end}, rel_seq={rel_seq}, gap={rel_seq - prev_end}")
            prev_end = max(prev_end, rel_seq + plen)

        # Reassemble
        stream = bytearray()
        for ts, seq, flags, plen, payload in packets:
            rel_seq = seq - base_seq
            if rel_seq + plen > len(stream):
                stream.extend(b"\x00" * (rel_seq + plen - len(stream)))
            for i, b in enumerate(payload):
                stream[rel_seq + i] = b

        stream = bytes(stream)

        print(f"\n=== {label} ===")
        print(f"  Packets: {len(packets)}, total payload bytes: {total_payload}, reassembled: {len(stream)}")
        print(f"  Overlaps: {overlaps}, Gaps: {gaps}")
        print(f"  Base seq: {base_seq}")
        print(f"  First 5 packets (rel_seq, flags, len):")
        for ts, seq, flags, plen, _ in packets[:5]:
            print(f"    rel_seq={seq - base_seq}, flags=0x{tcp_flags:02x}, len={plen}")
        print(f"  Last 5 packets (rel_seq, flags, len):")
        for ts, seq, flags, plen, _ in packets[-5:]:
            print(f"    rel_seq={seq - base_seq}, flags=0x{tcp_flags:02x}, len={plen}")


if __name__ == "__main__":
    main()
