#!/usr/bin/env python3
"""Decode MGO2 TCP game-server frames from a capture.

Usage:
    python3 tools/tcp_frame_decode.py <pcap> [port] [--limit N]

Reassembles TCP streams on the given port (default 5733), XOR-decodes the
whole frame with KEY_XOR, verifies the HMAC-MD5 tail, then prints the
command id, sequence, and payload of each frame in capture order.
"""
import hashlib
import hmac
import struct
import sys

from pcap_conversations import Pcapng, TCP

KEY_XOR = 0x5A7085AF
KEY_XOR_BYTES = b"\x5a\x70\x85\xaf"
HMAC_KEY = b"Z7/biJ46TzGF-8yx"
HEADER_SIZE = 24

# Command names for known opcodes (from docs/protocol/TCP_GAME_SERVER_PROTOCOL.md)
COMMAND_NAMES = {
    0x3003: "authBlowfish?",
    0x3004: "session/keepalive",
    0x4101: "getCharacterInfo",
    0x4103: "personalStats?",
    0x4105: "personalStats?",
    0x4107: "personalStats?",
    0x4111: "gameplayOptionsUpdate?",
    0x4113: "uiSettings?",
    0x4120: "settingsBlock?",
    0x4121: "settingsBlock?",
    0x4122: "characterStats?",
    0x4124: "gearCatalogue?",
    0x4125: "unlockCatalogue?",
    0x4131: "updatePersonalInfo?",
    0x4133: "morePersonalInfo?",
    0x4140: "gearSet?",
    0x4141: "gearSet?",
    0x4142: "gearSet?",
    0x4143: "gearSet?",
    0x4301: "lobbyEntry?",
    0x4302: "lobbyEntry?",
    0x4303: "lobbyEntry?",
    0x4305: "hostSettings?",
    0x4311: "checkHostSettings?",
    0x4313: "updateHostSettings?",
    0x4317: "createGame?",
    0x4320: "joinGame?",
    0x4321: "joinGameReply?",
    0x4323: "joinGameAck?",
    0x4341: "playerConnected?",
    0x4343: "playerDisconnected?",
    0x4381: "stats?",
    0x4391: "updateGame?",
    0x4393: "stats?",
    0x4395: "updatePings?",
    0x43a1: "pass?",
    0x43a3: "roundFlow?",
    0x43a5: "roundFlow?",
    0x43a7: "roundFlow?",
    0x43b1: "roundFlow?",
    0x43c0: "checkSession?",
    0x43c1: "lobbyList?",
    0x43c5: "friendList?",
    0x43c9: "blockedList?",
    0x43cb: "startRound?",
    0x4401: "chatMsg?",
    0x4440: "playerPing?",
    0x4441: "playerJoined?",
    0x4502: "lobbyList?",
    0x4512: "lobbyList?",
    0x4581: "friends?",
    0x4583: "blocked?",
    0x4601: "gameRoster?",
    0x4602: "gameRoster?",
    0x4603: "gameRoster?",
    0x4700: "updateConnInfo?",
    0x4701: "updateConnectionInfo?",
    0x4901: "chatMessage?",
    0x4910: "createTeam?",
    0x4990: "chatChannel?",
}


def xor_decode_frame(raw: bytes) -> bytes:
    """XOR the whole frame with the repeating 4-byte key."""
    return bytes(b ^ KEY_XOR_BYTES[i % 4] for i, b in enumerate(raw))


def parse_frame(decrypted: bytes):
    if len(decrypted) < HEADER_SIZE:
        return None
    cmd = struct.unpack_from(">H", decrypted, 0)[0]
    payload_len = struct.unpack_from(">H", decrypted, 2)[0]
    seq = struct.unpack_from(">H", decrypted, 4)[0]
    checksum = decrypted[8:24]
    payload = decrypted[HEADER_SIZE:HEADER_SIZE + payload_len]
    return cmd, payload_len, seq, checksum, payload


def verify_hmac(decrypted: bytes) -> bool:
    """Verify HMAC-MD5. The HMAC is computed over header[0:8] + payload,
    skipping the 16-byte checksum field entirely (not zeroed, just excluded)."""
    if len(decrypted) < HEADER_SIZE:
        return False
    payload_len = struct.unpack_from(">H", decrypted, 2)[0] & 0x3FF
    hmac_data = decrypted[:8] + decrypted[HEADER_SIZE:HEADER_SIZE + payload_len]
    computed = hmac.new(HMAC_KEY, hmac_data, hashlib.md5).digest()
    return hmac.compare_digest(computed, decrypted[8:24])


def extract_tcp_packets(capture, port):
    """Walk the raw pcap packets, returning (ts, seq, direction, payload)."""
    results = []
    for ts, frame in capture.packets:
        if len(frame) < 20:
            continue

        # Handle link types
        if capture.link_type == 1:  # LINKTYPE_ETHERNET
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
        elif capture.link_type == 101:  # LINKTYPE_RAW
            ip = frame
        else:
            continue

        if len(ip) < 20:
            continue

        ip_version = ip[0] >> 4
        if ip_version == 4:
            ip_header_len = (ip[0] & 0x0F) * 4
            if len(ip) < ip_header_len + 4:
                continue
            total_len = struct.unpack_from(">H", ip, 2)[0]
            protocol = ip[9]
            transport = ip[ip_header_len:total_len]
        elif ip_version == 6:
            if len(ip) < 40:
                continue
            protocol = ip[6]
            transport = ip[40:]
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
        if len(transport) < tcp_header_len:
            continue
        seq = struct.unpack_from(">I", transport, 4)[0]
        payload = transport[tcp_header_len:]

        if not payload:
            continue

        direction = 0 if dst_port == port else 1  # 0 = C->S, 1 = S->C
        results.append((ts, seq, direction, payload))

    return results


def reassemble_stream(packets):
    """Reassemble a byte stream from TCP segments using sequence numbers."""
    if not packets:
        return b""
    packets.sort(key=lambda x: x[1])  # sort by seq
    base_seq = packets[0][1]
    buffer = bytearray()
    for _, seq, _, payload in packets:
        rel_seq = seq - base_seq
        if rel_seq + len(payload) > len(buffer):
            buffer.extend(b"\x00" * (rel_seq + len(payload) - len(buffer)))
        for i, b in enumerate(payload):
            buffer[rel_seq + i] = b
    return bytes(buffer)


def decode_frames(data, label, limit):
    """Decode and print frames from a reassembled byte stream."""
    print(f"\n=== Decoding {label} frames ({len(data)} bytes) ===")

    offset = 0
    frame_num = 0
    while offset + HEADER_SIZE <= len(data):
        encrypted_header = data[offset:offset + HEADER_SIZE]
        # Payload length is at offset 2, XORed with key[i & 3]
        # After XOR: encrypted[2] ^ 0x85, encrypted[3] ^ 0xaf -> payload_len
        payload_len = (struct.unpack_from(">H", encrypted_header, 2)[0] ^ 0x85AF) & 0x3FF

        frame_size = HEADER_SIZE + payload_len
        if offset + frame_size > len(data):
            print(f"  Frame {frame_num}: incomplete (need {frame_size}, have {len(data) - offset})")
            break

        raw_frame = data[offset:offset + frame_size]
        decrypted = xor_decode_frame(raw_frame)

        parsed = parse_frame(decrypted)
        if parsed is None:
            print(f"  Frame {frame_num}: too short after decrypt")
            break

        cmd, plen, seq_no, checksum, payload_bytes = parsed

        hmac_ok = verify_hmac(decrypted)

        frame_num += 1
        if limit and frame_num > limit:
            print(f"  ... (truncated at {limit} frames)")
            break

        cmd_name = COMMAND_NAMES.get(cmd, "???")
        hex_payload = payload_bytes.hex()

        print(f"  [{frame_num:4d}] {label} seq={seq_no:4} cmd=0x{cmd:04X} ({cmd_name}) len={plen:4} hmac={'OK' if hmac_ok else 'FAIL'}")
        if len(payload_bytes) <= 64:
            print(f"          payload: {hex_payload}")
            if payload_bytes:
                text = "".join(chr(b) if 32 <= b < 127 else "." for b in payload_bytes)
                print(f"          ascii:   {text}")
        else:
            print(f"          payload ({len(payload_bytes)} bytes): {hex_payload[:120]}...")

        offset += frame_size


def main() -> None:
    path = sys.argv[1]
    port = int(sys.argv[2]) if len(sys.argv) > 2 and not sys.argv[2].startswith("-") else 5733
    limit = None
    if "--limit" in sys.argv:
        limit = int(sys.argv[sys.argv.index("--limit") + 1])

    capture = Pcapng(path)
    capture.read()

    flows = extract_tcp_packets(capture, port)
    if not flows:
        print(f"No TCP traffic on port {port} found in {path}")
        return

    print(f"Found {len(flows)} TCP packets with payload on port {port}")

    client_flows = [f for f in flows if f[2] == 0]
    server_flows = [f for f in flows if f[2] == 1]
    print(f"  C->S: {len(client_flows)} packets")
    print(f"  S->C: {len(server_flows)} packets")

    client_stream = reassemble_stream(client_flows)
    server_stream = reassemble_stream(server_flows)
    print(f"\nReassembled streams:")
    print(f"  C->S: {len(client_stream)} bytes")
    print(f"  S->C: {len(server_stream)} bytes")

    decode_frames(client_stream, "C->S", limit)
    decode_frames(server_stream, "S->C", limit)


if __name__ == "__main__":
    main()
