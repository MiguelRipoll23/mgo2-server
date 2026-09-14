"""Probes the MGO2 account TCP server the way the game client does.

The tool replays the client's handshake against a running server: it signs in
through the public login endpoint, presents the derived session field with a
check-session packet, asks for the character list and decodes the reply, so the
payload layout the server actually sends can be read back off the wire.

    python tools/mgo2_tcp_probe.py --host 192.168.1.15 --name phildunphy23 \
        --password-md5 148ae679428b6c15bc44c5a5aaf5668c

Every cryptographic primitive is mirrored from src/Shared/Utils/CryptoUtility.cs
and the key tables are read from docs/keys, so the frames this tool writes are
byte-identical to the ones the codec writes.
"""

import argparse
import hashlib
import hmac
import socket
import struct
import sys
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
KEY_DIRECTORY = ROOT / "docs" / "keys"

ACCOUNT_PORT = 5732
HEADER_SIZE = 24
PAYLOAD_OFFSET = 0x18
CHECKSUM_OFFSET = 0x08
BLOWFISH_BLOCK_SIZE = 8
BLOWFISH_ROUNDS = 8

CHECK_SESSION = 0x3003
CHECK_SESSION_RESULT = 0x3004
GET_CHARACTER_LIST = 0x3048
CHARACTER_LIST = 0x3049

# Payloads the codec encrypts, from BlowfishEncryptedCommandConstants.
INBOUND_ENCRYPTED = (0x3003, 0x4310, 0x4320, 0x43C0, 0x4700, 0x4990)
OUTBOUND_ENCRYPTED = (0x4305,)

# Character-list grid, verified in MGO2.ELF (see docs/expansion-entitlements.md).
LIST_HEADER_SIZE = 0x17
LIST_MAIN_NAME_SIZE = 0x10
# The client strides its in-memory records by 0x3c, but only 52 bytes of each
# entry travel on the wire.
LIST_ENTRY_SIZE = 52
# The parser at 0x00f031ec compares its destination offset against 0x1a4 and only
# then adds 0x3c, so it fills the record at 0x1a4 too: 0x1a4 / 0x3c + 1 = eight
# entries, matching the 0x1e0 bytes of session records it clears first.
LIST_SLOT_COUNT = 8
LIST_TRAILER_OFFSET = LIST_HEADER_SIZE + (LIST_SLOT_COUNT * LIST_ENTRY_SIZE)
LIST_TRAILER_SIZE = 0x20
LIST_PAYLOAD_SIZE = LIST_TRAILER_OFFSET + LIST_TRAILER_SIZE
# Layout the server sent when the loop bound was misread as seven entries: the
# trailer then sits exactly where the client expects its eighth entry.
LEGACY_SLOT_COUNT = 7
LEGACY_TRAILER_OFFSET = LIST_HEADER_SIZE + (LEGACY_SLOT_COUNT * LIST_ENTRY_SIZE)
LEGACY_PAYLOAD_SIZE = LEGACY_TRAILER_OFFSET + LIST_TRAILER_SIZE


def xor_key() -> bytes:
    """Returns the frame XOR key."""
    return (KEY_DIRECTORY / "tcp-xor-key.bin").read_bytes()


def hmac_key() -> bytes:
    """Returns the checksum key."""
    return (KEY_DIRECTORY / "tcp-hmac-md5-key.bin").read_bytes()


def blowfish_table(name: str) -> bytes:
    """Returns a pre-scheduled Blowfish table.

    Args:
        name: Either "packet" or "auth".
    """
    return (KEY_DIRECTORY / f"blowfish-{name}-key.bin").read_bytes()


def session_initialization_vector() -> bytes:
    """Returns the initialization vector of the session-field derivation."""
    return bytes([0x35, 0xD5, 0xC3, 0x8E, 0xD0, 0x11, 0x0E, 0xA8])


def apply_exclusive_or(buffer: bytearray, key: bytes) -> None:
    """XORs a buffer with a repeating key, in place."""
    for index in range(len(buffer)):
        buffer[index] ^= key[index & (len(key) - 1)]


def read_word(table: bytes, offset: int) -> int:
    """Reads a big-endian 32-bit word."""
    return struct.unpack_from(">I", table, offset)[0]


def blowfish_round(table: bytes, value: int) -> int:
    """One round: four substitution-box lookups combined into a 32-bit result."""
    index_a = (value >> 22) & 0x3FC
    index_b = (value >> 14) & 0x3FC
    index_c = (value >> 6) & 0x3FC
    index_d = ((value << 2) | (value >> 30)) & 0x3FC

    first = read_word(table, index_a + 0x48)
    second = read_word(table, index_b + 0x48 + 0x400)
    third = read_word(table, index_c + 0x48 + 0x800)
    fourth = read_word(table, index_d + 0x48 + 0xC00)

    return ((((first + second) & 0xFFFFFFFF) ^ third) + fourth) & 0xFFFFFFFF


def blowfish_encrypt(data: bytes, table: bytes) -> bytes:
    """Encrypts block-aligned data with the custom eight-round Blowfish variant."""
    output = bytearray(data)
    for offset in range(0, len(output), BLOWFISH_BLOCK_SIZE):
        first = read_word(output, offset + 4)
        second = read_word(output, offset) ^ read_word(table, 0x00)

        for round_index in range(BLOWFISH_ROUNDS - 1, -1, -1):
            first ^= read_word(table, 0x3C - (round_index * 8))
            first ^= blowfish_round(table, second)
            second ^= read_word(table, 0x40 - (round_index * 8))
            second ^= blowfish_round(table, first)

        first ^= read_word(table, 0x44)
        struct.pack_into(">I", output, offset, first)
        struct.pack_into(">I", output, offset + 4, second)

    return bytes(output)


def blowfish_decrypt(data: bytes, table: bytes) -> bytes:
    """Decrypts block-aligned data with the custom eight-round Blowfish variant."""
    output = bytearray(data)
    for offset in range(0, len(output), BLOWFISH_BLOCK_SIZE):
        first = read_word(output, offset + 4)
        second = read_word(output, offset) ^ read_word(table, 0x44)

        for round_index in range(BLOWFISH_ROUNDS):
            first ^= read_word(table, 0x40 - (round_index * 8))
            first ^= blowfish_round(table, second)
            second ^= read_word(table, 0x3C - (round_index * 8))
            second ^= blowfish_round(table, first)

        first ^= read_word(table, 0x00)
        struct.pack_into(">I", output, offset, first)
        struct.pack_into(">I", output, offset + 4, second)

    return bytes(output)


def derive_session_field(token: str) -> bytes:
    """Derives the sixteen-byte session field a client presents on check-session."""
    plain = token.encode("latin-1")
    previous = session_initialization_vector()
    output = bytearray()

    for offset in range(0, len(plain), BLOWFISH_BLOCK_SIZE):
        block = plain[offset : offset + BLOWFISH_BLOCK_SIZE]
        transformed = blowfish_decrypt(block, blowfish_table("auth"))
        output += bytes(left ^ right for left, right in zip(transformed, previous))
        previous = block

    return bytes(output)


def pad_to_block(payload: bytes) -> bytes:
    """Pads a payload to Blowfish block alignment with zeroes."""
    remainder = len(payload) % BLOWFISH_BLOCK_SIZE
    if remainder == 0:
        return payload
    return payload + bytes(BLOWFISH_BLOCK_SIZE - remainder)


def encode_frame(command: int, payload: bytes, sequence: int) -> bytes:
    """Encodes one outbound frame exactly as the server's codec would."""
    if command in INBOUND_ENCRYPTED or command in OUTBOUND_ENCRYPTED:
        encoded = blowfish_encrypt(pad_to_block(payload), blowfish_table("packet"))
    else:
        encoded = payload

    frame = bytearray(HEADER_SIZE)
    struct.pack_into(">H", frame, 0, command)
    struct.pack_into(">H", frame, 2, len(encoded))
    struct.pack_into(">I", frame, 4, sequence)

    digest = hmac.new(hmac_key(), bytes(frame[:8]) + encoded, hashlib.md5).digest()
    frame[CHECKSUM_OFFSET:CHECKSUM_OFFSET + 16] = digest
    frame += encoded

    apply_exclusive_or(frame, xor_key())
    return bytes(frame)


def decode_frame(raw: bytes) -> tuple[int, int, bytes]:
    """Decodes one frame, returning its command, sequence and payload."""
    frame = bytearray(raw)
    apply_exclusive_or(frame, xor_key())

    command = struct.unpack_from(">H", frame, 0)[0]
    payload_length = struct.unpack_from(">H", frame, 2)[0]
    sequence = struct.unpack_from(">I", frame, 4)[0]
    payload = bytes(frame[PAYLOAD_OFFSET : PAYLOAD_OFFSET + payload_length])

    expected = hmac.new(hmac_key(), bytes(frame[:8]) + payload, hashlib.md5).digest()
    if expected != bytes(frame[CHECKSUM_OFFSET:CHECKSUM_OFFSET + 16]):
        raise RuntimeError("checksum mismatch on the inbound frame")

    if command in INBOUND_ENCRYPTED or command in OUTBOUND_ENCRYPTED:
        payload = blowfish_decrypt(pad_to_block(payload), blowfish_table("packet"))[:payload_length]

    return command, sequence, payload


class Connection:
    """A socket that reads and writes whole frames."""

    def __init__(self, host: str, port: int, timeout: float):
        self.socket = socket.create_connection((host, port), timeout=timeout)
        self.sequence = 0

    def send(self, command: int, payload: bytes = b"") -> None:
        """Writes one frame and advances the outbound sequence."""
        self.socket.sendall(encode_frame(command, payload, self.sequence))
        self.sequence += 1

    def receive(self) -> tuple[int, bytes]:
        """Reads one whole frame, returning its command and payload."""
        header = self.read_exactly(HEADER_SIZE)
        decrypted = bytearray(header)
        apply_exclusive_or(decrypted, xor_key())
        payload_length = struct.unpack_from(">H", decrypted, 2)[0]
        body = self.read_exactly(payload_length)
        command, _, payload = decode_frame(header + body)
        return command, payload

    def read_exactly(self, length: int) -> bytes:
        """Reads exactly a number of bytes, failing when the peer closes first."""
        chunks = bytearray()
        while len(chunks) < length:
            chunk = self.socket.recv(length - len(chunks))
            if not chunk:
                raise RuntimeError("the server closed the connection")
            chunks += chunk
        return bytes(chunks)

    def close(self) -> None:
        """Closes the socket."""
        self.socket.close()


def login(http_base: str, name: str, password_md5: str) -> tuple[int, str]:
    """Signs in through the public login endpoint, returning the account and token."""
    form = urllib.parse.urlencode(
        {
            "name": name,
            "passwd": password_md5,
            "product": "1",
            "lang": "1",
            "tz": "0",
            "disk": "0",
            "ps3": "1",
            "stime": "0",
            "seed": "0",
        }
    )
    url = http_base.rstrip("/") + "/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/"
    request = urllib.request.Request(
        url,
        data=form.encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )

    with urllib.request.urlopen(request, timeout=20) as response:
        body = response.read().decode("latin-1")

    fields = body.split(",")
    if len(fields) != 4 or fields[0] != "0":
        raise RuntimeError(f"login rejected: {body.strip()}")

    return int(fields[1]), fields[3]


def describe_character_list(payload: bytes) -> None:
    """Prints what the character-list payload says against both known layouts."""
    print(f"payload length: {len(payload)} bytes (0x{len(payload):x})")

    if len(payload) < LIST_HEADER_SIZE:
        print("payload is too short to hold the grid header")
        return

    result = struct.unpack_from(">I", payload, 0)[0]
    slots = payload[4]
    count = payload[5]
    main_index = payload[6]
    main_name = payload[7 : 7 + LIST_MAIN_NAME_SIZE].split(b"\x00")[0].decode("latin-1")

    print(f"result word  : {result} (0 would mean the client parses the grid)")
    print(f"slots        : {slots}")
    print(f"count        : {count}")
    print(f"main index   : {main_index}")
    print(f"main name    : {main_name!r}")

    for index in range(LIST_SLOT_COUNT):
        offset = LIST_HEADER_SIZE + (index * LIST_ENTRY_SIZE)
        if offset + LIST_ENTRY_SIZE > len(payload):
            break
        entry = payload[offset : offset + LIST_ENTRY_SIZE]
        identifier = struct.unpack_from(">I", entry, 1)[0]
        name = entry[5 : 5 + 0x10].split(b"\x00")[0].decode("latin-1")
        marker = "*" if name.startswith("*") else " "
        print(f"slot {index} {marker}: id={identifier} name={name!r}")

    for label, trailer_offset, expected_size in (
        ("eight-slot layout", LIST_TRAILER_OFFSET, LIST_PAYLOAD_SIZE),
        ("seven-slot layout", LEGACY_TRAILER_OFFSET, LEGACY_PAYLOAD_SIZE),
    ):
        trailer = payload[trailer_offset : trailer_offset + LIST_TRAILER_SIZE]
        if len(trailer) < LIST_TRAILER_SIZE:
            print(f"{label}: no trailer at 0x{trailer_offset:x} (payload too short)")
            continue
        words = struct.unpack(">8I", trailer)
        verdict = "MATCHES" if len(payload) == expected_size else "does not match"
        print(
            f"{label}: length {verdict} (expected {expected_size}/0x{expected_size:x}), "
            f"trailer at 0x{trailer_offset:x} = {trailer.hex()} -> "
            f"[1]=0x{trailer[1]:02x} [3]=0x{trailer[3]:02x} words={[hex(word) for word in words[:4]]}"
        )


def parse_arguments() -> argparse.Namespace:
    """Reads the command line."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", required=True, help="server address")
    parser.add_argument("--account-port", type=int, default=ACCOUNT_PORT, help="account TCP port")
    parser.add_argument("--http-url", default=None, help="HTTP API base URL, derived from the host when omitted")
    parser.add_argument("--name", help="account login name")
    parser.add_argument("--password-md5", help="MD5 hash of the account password")
    parser.add_argument("--user-id", type=int, help="skip the HTTP login and use this account identifier")
    parser.add_argument("--field", help="skip the HTTP login and use this session field (hex)")
    parser.add_argument("--timeout", type=float, default=15.0, help="socket timeout in seconds")
    parser.add_argument("--raw", action="store_true", help="dump the whole character-list payload in hex")
    return parser.parse_args()


def main() -> int:
    """Runs the probe."""
    arguments = parse_arguments()

    user_identifier = arguments.user_id
    session_field = bytes.fromhex(arguments.field) if arguments.field else None

    if session_field is None:
        if not arguments.name or not arguments.password_md5:
            print("give --name and --password-md5, or pass --user-id and --field", file=sys.stderr)
            return 2

        http_base = arguments.http_url or f"http://{arguments.host}"
        user_identifier, token = login(http_base, arguments.name, arguments.password_md5)
        session_field = derive_session_field(token)
        print(f"login ok: user id {user_identifier}, token {token}")
        print(f"derived session field: {session_field.hex()}")

    connection = Connection(arguments.host, arguments.account_port, arguments.timeout)
    print(f"connected to {arguments.host}:{arguments.account_port}")

    try:
        connection.send(CHECK_SESSION, struct.pack(">I", user_identifier) + session_field)
        command, payload = connection.receive()
        result = struct.unpack_from(">I", payload, 0)[0] if len(payload) >= 4 else None
        print(f"check-session reply: 0x{command:04x} result={result} (0 means signed in)")

        if command != CHECK_SESSION_RESULT or result != 0:
            print("check-session was refused; the character list cannot be read", file=sys.stderr)
            return 1

        connection.send(GET_CHARACTER_LIST)
        command, payload = connection.receive()
        print(f"character-list reply: 0x{command:04x}")
        if command != CHARACTER_LIST:
            print("unexpected reply", file=sys.stderr)
            return 1

        describe_character_list(payload)
        if arguments.raw:
            print(payload.hex())
    finally:
        connection.close()

    return 0


if __name__ == "__main__":
    sys.exit(main())
