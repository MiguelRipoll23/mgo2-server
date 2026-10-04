#!/usr/bin/env python3
"""Read the 0x4302 game-list entries out of a captured lobby TCP stream.

The lobby stream is XOR-obfuscated with the fixed packet key, so both the command
and the payload decode without any session material: reassemble the server-to-
client direction of one conversation, split it into packets on the 24-byte header,
and print each room-list entry with its fields named. The field offsets are the
wire ones in docs/proto/outbound/mgo2_cmd_4302_s2c.ksy, which is also where the
ping field's unit and the client's own thresholds are recorded.

    python tools/room_list_probe.py docs/mgo2-game.pcapng --port 5733 --all-commands
"""
import argparse
import struct
import sys

XOR_KEY = bytes([0x5A, 0x70, 0x85, 0xAF])
HEADER_SIZE = 24
GAME_LIST_PAGE = 0x4302
ENTRY_SIZE = 0x37


def read_pcapng(path):
    """Yields (link_type, direction, payload_bytes) for each packet."""
    data = open(path, "rb").read()
    offset = 0
    endian = "<"
    link_type = None
    while offset + 12 <= len(data):
        block_type = struct.unpack_from(endian + "I", data, offset)[0]
        if block_type == 0x0A0D0D0A:
            # Section Header Block: byte-order magic decides the endianness.
            magic = data[offset + 8:offset + 12]
            endian = "<" if magic == b"\x4d\x3c\x2b\x1a" else ">"
            block_type = struct.unpack_from(endian + "I", data, offset)[0]
            block_length = struct.unpack_from(endian + "I", data, offset + 4)[0]
            offset += block_length
            continue
        block_length = struct.unpack_from(endian + "I", data, offset + 4)[0]
        body = data[offset + 8:offset + block_length - 4]
        if block_type == 1:  # Interface Description Block
            link_type = struct.unpack_from(endian + "H", body, 0)[0]
        elif block_type == 6:  # Enhanced Packet Block
            captured = struct.unpack_from(endian + "I", body, 12)[0]
            packet = body[20:20 + captured]
            yield link_type, packet
        offset += block_length


def ethernet_payload(packet, link_type):
    if link_type == 1:
        return packet[14:]
    if link_type in (101, 228):
        # This capture stores the IP packet itself (LINKTYPE_RAW).
        return packet
    if link_type == 113:
        return packet
    raise ValueError(f"unsupported link type {link_type}")


def tcp_segments(packet, link_type=1):
    """Returns (src_ip, src_port, dst_ip, dst_port, sequence, payload)."""
    frame = ethernet_payload(packet, link_type)
    if len(frame) < 20:
        return None
    if link_type == 1 and frame[12:14] != b"\x08\x00":
        return None
    ip = frame
    ip_header_length = (ip[0] & 0x0F) * 4
    protocol = ip[9]
    if protocol != 6:
        return None
    source = ".".join(str(b) for b in ip[12:16])
    destination = ".".join(str(b) for b in ip[16:20])
    tcp = ip[ip_header_length:]
    source_port, destination_port, sequence = struct.unpack_from(">HHI", tcp, 0)
    data_offset = (tcp[12] >> 4) * 4
    return source, source_port, destination, destination_port, sequence, tcp[data_offset:]


def reassemble(segments):
    """Orders segments by sequence number and concatenates the payloads."""
    ordered = sorted(segments.items())
    stream = bytearray()
    expected = None
    for sequence, payload in ordered:
        if expected is None:
            expected = sequence
        if sequence > expected:
            gap = sequence - expected
            print(f"# {gap} bytes missing before sequence {sequence}", file=sys.stderr)
            expected = sequence
        if sequence + len(payload) <= expected:
            continue  # retransmission
        overlap = max(0, expected - sequence)
        stream.extend(payload[overlap:])
        expected = sequence + len(payload)
    return bytes(stream)


def packets(stream):
    """Splits the stream into packets, decoding each with the XOR key.

    The key is applied to the whole packet, header and payload alike, so the
    payload is decoded before any field is read out of it.
    """
    position = 0
    while position + HEADER_SIZE <= len(stream):
        header = bytearray(stream[position:position + HEADER_SIZE])
        for index in range(HEADER_SIZE):
            header[index] ^= XOR_KEY[index & 3]
        command, payload_length, sequence = struct.unpack_from(">HHI", header, 0)
        if payload_length > 0x400:
            position += 1
            continue
        end = position + HEADER_SIZE + payload_length
        if end > len(stream):
            break
        decoded = bytearray(stream[position:end])
        for index in range(len(decoded)):
            decoded[index] ^= XOR_KEY[index & 3]
        yield position, command, sequence, bytes(decoded), bytes(decoded)
        position = end


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", nargs="?", default="docs/mgo2-game.pcapng")
    parser.add_argument("--port", type=int, default=5731)
    parser.add_argument("--host", default="15.204.239.231")
    parser.add_argument("--all-commands", action="store_true")
    arguments = parser.parse_args()

    inbound = {}
    outbound = {}
    for link_type, packet in read_pcapng(arguments.capture):
        segment = tcp_segments(packet, link_type)
        if segment is None:
            continue
        source, source_port, destination, destination_port, sequence, payload = segment
        if not payload:
            continue
        if destination_port == arguments.port and destination == arguments.host:
            outbound.setdefault((destination, destination_port, source, source_port), {})[sequence] = payload
        elif source_port == arguments.port and source == arguments.host:
            inbound.setdefault((source, source_port, destination, destination_port), {})[sequence] = payload

    for key, segments in inbound.items():
        print(f"== conversation {key[2]}:{key[3]} -> {key[0]}:{key[1]}")
        stream = reassemble(segments)
        counts = {}
        for position, command, sequence, header, raw in packets(stream):
            counts[command] = counts.get(command, 0) + 1
            if command == GAME_LIST_PAGE:
                payload = raw[HEADER_SIZE:]
                for offset in range(0, len(payload) - ENTRY_SIZE + 1, ENTRY_SIZE):
                    entry = payload[offset:offset + ENTRY_SIZE]
                    identifier = struct.unpack_from(">I", entry, 0)[0]
                    name = entry[4:20].split(b"\x00")[0].decode("latin-1")
                    print(
                        f"id={identifier} name={name!r} "
                        f"host_options=0x{entry[0x14]:02x} lobby_subtype=0x{entry[0x15]:02x} "
                        f"rule={entry[0x16]} map={entry[0x17]} round_flags={entry[0x18]} "
                        f"max_players={entry[0x19]} stance={entry[0x1a]} "
                        f"commonA=0x{entry[0x1b]:02x} commonB=0x{entry[0x1c]:02x} "
                        f"players={entry[0x1d]} "
                        f"ping={struct.unpack_from('>I', entry, 0x1e)[0]} "
                        f"friend_block=0x{entry[0x22]:02x} tolerance={entry[0x23]} "
                        f"level_base={struct.unpack_from('>I', entry, 0x24)[0]} "
                        f"avg_exp={struct.unpack_from('>I', entry, 0x28)[0]} "
                        f"host_score={struct.unpack_from('>I', entry, 0x2c)[0]} "
                        f"host_votes={struct.unpack_from('>I', entry, 0x30)[0]} "
                        f"selector_flags=0x{entry[0x34]:02x} "
                        f"tiebreak={struct.unpack_from('>H', entry, 0x35)[0]}")
        if arguments.all_commands:
            print("command histogram:", {f"0x{c:04x}": n for c, n in sorted(counts.items())})


if __name__ == "__main__":
    main()
