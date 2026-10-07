#!/usr/bin/env python3
"""Debug HMAC computation on the first frames of each direction."""
import hashlib
import hmac
import struct
import sys

from pcap_conversations import Pcapng, TCP

KEY_XOR_BYTES = b"\x5a\x70\x85\xaf"
HMAC_KEY = b"Z7/biJ46TzGF-8yx"
HEADER_SIZE = 24
PORT = 5733


def xor_decode_frame(raw: bytes) -> bytes:
    return bytes(b ^ KEY_XOR_BYTES[i % 4] for i, b in enumerate(raw))


def extract_packets(capture, port):
    results = []
    for ts, frame in capture.packets:
        if len(frame) < 20:
            continue
        # LINKTYPE_RAW (101) - no ethernet header
        ip = frame
        ip_version = ip[0] >> 4
        if ip_version == 4:
            ip_header_len = (ip[0] & 0x0F) * 4
            total_len = struct.unpack_from(">H", ip, 2)[0]
            protocol = ip[9]
            transport = ip[ip_header_len:total_len]
        else:
            continue
        if protocol != TCP:
            continue
        if len(transport) < 20:
            continue
        src_port, dst_port = struct.unpack_from(">HH", transport, 0)
        if src_port != port and dst_port != port:
            continue
        tcp_header_len = (transport[12] >> 4) * 4
        seq = struct.unpack_from(">I", transport, 4)[0]
        payload = transport[tcp_header_len:]
        if not payload:
            continue
        direction = 0 if dst_port == port else 1
        results.append((ts, seq, direction, payload))
    return results


def reassemble(packets):
    if not packets:
        return b""
    packets.sort(key=lambda x: x[1])
    base_seq = packets[0][1]
    buffer = bytearray()
    for _, seq, _, payload in packets:
        rel_seq = seq - base_seq
        if rel_seq + len(payload) > len(buffer):
            buffer.extend(b"\x00" * (rel_seq + len(payload) - len(buffer)))
        for i, b in enumerate(payload):
            buffer[rel_seq + i] = b
    return bytes(buffer)


def main():
    path = sys.argv[1]
    port = int(sys.argv[2]) if len(sys.argv) > 2 else PORT

    capture = Pcapng(path)
    capture.read()

    flows = extract_packets(capture, port)
    client = [f for f in flows if f[2] == 0]
    server = [f for f in flows if f[2] == 1]

    c_stream = reassemble(client)
    s_stream = reassemble(server)

    for label, data in [("C->S", c_stream), ("S->C", s_stream)]:
        if len(data) < HEADER_SIZE:
            print(f"\n=== {label}: only {len(data)} bytes ===")
            continue

        # Read the first frame's encrypted header
        enc_cmd_len = struct.unpack_from(">HH", data, 0)[0]  # encrypted cmd and len
        enc_full = data[:HEADER_SIZE]

        # XOR-decrypt just the header
        dec_header = xor_decode_frame(enc_full)
        cmd = struct.unpack_from(">H", dec_header, 0)[0]
        plen = struct.unpack_from(">H", dec_header, 2)[0] & 0x3FF
        seq = struct.unpack_from(">H", dec_header, 4)[0]
        checksum = dec_header[8:24]

        print(f"\n=== {label} first frame ===")
        print(f"  Encrypted header: {enc_full.hex()}")
        print(f"  Decrypted cmd: 0x{cmd:04X}, len: {plen}, seq: {seq}")
        print(f"  Checksum: {checksum.hex()}")

        frame_size = HEADER_SIZE + plen
        raw_frame = data[:frame_size]
        decrypted = xor_decode_frame(raw_frame)
        payload = decrypted[HEADER_SIZE:HEADER_SIZE + plen]
        print(f"  Payload: {payload.hex()}")
        print(f"  ASCII: {''.join(chr(b) if 32 <= b < 127 else '.' for b in payload)}")

        # Try various HMAC computations
        print(f"\n  --- HMAC attempts ---")

        # Method 1: HMAC over header[0:8] + zeroed checksum + payload
        data1 = decrypted[:8] + b"\x00" * 16 + decrypted[24:24 + plen]
        hmac1 = hmac.new(HMAC_KEY, data1, hashlib.md5).digest()
        print(f"  M1 (hdr0:8 + zero + payload): {hmac1.hex()} {'== OK' if hmac1 == checksum else '!= FAIL'}")

        # Method 2: HMAC over the entire decrypted frame with checksum zeroed
        data2 = bytearray(decrypted[:frame_size])
        data2[8:24] = b"\x00" * 16
        hmac2 = hmac.new(HMAC_KEY, bytes(data2[:frame_size]), hashlib.md5).digest()
        print(f"  M2 (full frame, checksum zeroed): {hmac2.hex()} {'== OK' if hmac2 == checksum else '!= FAIL'}")

        # Method 3: HMAC over header[0:8] + payload (without the zeroed checksum area)
        data3 = decrypted[:8] + decrypted[24:24 + plen]
        hmac3 = hmac.new(HMAC_KEY, data3, hashlib.md5).digest()
        print(f"  M3 (hdr0:8 + payload only): {hmac3.hex()} {'== OK' if hmac3 == checksum else '!= FAIL'}")

        # Method 4: HMAC over just payload
        data4 = decrypted[24:24 + plen]
        hmac4 = hmac.new(HMAC_KEY, data4, hashlib.md5).digest()
        print(f"  M4 (payload only): {hmac4.hex()} {'== OK' if hmac4 == checksum else '!= FAIL'}")

        # Method 5: HMAC over command + length + payload
        data5 = decrypted[:4] + decrypted[24:24 + plen]
        hmac5 = hmac.new(HMAC_KEY, data5, hashlib.md5).digest()
        print(f"  M5 (cmd + len + payload): {hmac5.hex()} {'== OK' if hmac5 == checksum else '!= FAIL'}")

        # Method 6: HMAC over the ENCRYPTED data (re-XOR the whole frame)
        data6 = raw_frame[:8] + b"\x00" * 16 + raw_frame[24:24 + plen]
        hmac6 = hmac.new(HMAC_KEY, data6, hashlib.md5).digest()
        dec6 = xor_decode_frame(hmac6)
        print(f"  M6 (enc hdr0:8 + zero + enc payload -> xor): {dec6.hex()} {'== OK' if hmac6 == checksum else '!= FAIL'}")

        # Let's also check: maybe HMAC is over the encrypted form as-is
        data7 = bytearray(raw_frame[:frame_size])
        data7[8:24] = b"\x00" * 16
        hmac7 = hmac.new(HMAC_KEY, bytes(data7[:frame_size]), hashlib.md5).digest()
        print(f"  M7 (full enc frame, checksum zeroed): {hmac7.hex()} {'== OK' if hmac7 == checksum else '!= FAIL'}")


if __name__ == "__main__":
    main()
