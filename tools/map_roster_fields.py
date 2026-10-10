#!/usr/bin/env python3
"""Correlate the roster fields our server writes as zero against the working
captures, using the peer handshakes and the RPDT rules-roster grammar.

    python3 tools/map_roster_fields.py mgo2-game1.pcapng mgo2-game2.pcapng mgo2-game3.pcapng

For each capture it prints:
  * every peer handshake (peer_id, counter_base, capability, mode, count, entries)
  * every roster entry (roster index, handle bytes at 0x05/0x07, character id,
    endpoint pair, name)
  * every rules-roster record (0x0261/0x0a61) with a non-empty member set
and then tests the handle byte against the character id, the handshake peer_id,
the source port and the address octets -- the fields the handle could be derived
from, which is what "map the unknown field" means here.
"""

from __future__ import annotations

import struct
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "tools"))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, lzss_decompress, parse_messages  # noqa: E402

HANDSHAKE_LEN = 44
HANDSHAKE_TYPE = 0x1000
RULES_IDS = (0x0261, 0x0A61)
ROSTER_IDS = (0x9001, 0x1001)


def handshake_fields(plain: bytes) -> dict:
    return {
        "peer_id": struct.unpack_from("<I", plain, 6)[0],
        "counter_base": struct.unpack_from("<I", plain, 10)[0],
        "magic": struct.unpack_from("<I", plain, 14)[0],
        "capability": plain[18],
        "mode": struct.unpack_from("<H", plain, 19)[0],
        "count": plain[21],
        "entries": [
            (
                ".".join(str(b) for b in plain[22 + i * 6 : 26 + i * 6]),
                struct.unpack_from("<H", plain, 26 + i * 6)[0],
            )
            for i in range(plain[21])
        ],
    }


def collect(capture: Pcapng) -> list[tuple]:
    return [
        (ts, f"{source}:{source_port}", f"{destination}:{destination_port}", payload)
        for ts, protocol, source, source_port, destination, destination_port, payload
        in capture.decoded_packets()
        if protocol == UDP and 3478 not in (source_port, destination_port)
    ]


def analyze(path: str) -> None:
    capture = Pcapng(path)
    capture.read()
    datagrams = collect(capture)

    # Handshakes, keyed by the unordered endpoint pair.
    handshakes: dict[frozenset, dict[str, dict]] = defaultdict(dict)
    for _, local, remote, payload in datagrams:
        if not 22 <= len(payload) <= 64:
            continue
        _, plain, verified = decode(payload, PRE_KEY)
        if not verified or struct.unpack_from("<H", plain, 2)[0] != HANDSHAKE_TYPE:
            continue
        if 22 + plain[21] * 6 > len(plain):
            continue
        handshakes[frozenset((local, remote))][local] = handshake_fields(plain)

    print("=" * 78)
    print(f"{path}: {len(handshakes)} handshake flow(s)")
    for pair, sides in handshakes.items():
        for side, fields in sorted(sides.items()):
            print(
                f"  {side:<22} peer_id=0x{fields['peer_id']:08x} base=0x{fields['counter_base']:08x} "
                f"cap={fields['capability']:02x} mode={fields['mode']} count={fields['count']} "
                f"entries={fields['entries']}"
            )

    # Every roster entry and rules-roster record, decoded under the flow key.
    keys: dict[frozenset, int] = {}
    for pair, sides in handshakes.items():
        if len(sides) != 2:
            continue
        values = list(sides.values())
        keys[pair] = (values[0]["counter_base"] ^ values[1]["counter_base"]) & 0xFFFFFFFF

    roster: list[tuple] = []
    rules: list[tuple] = []
    joiner_records: list[tuple] = []
    for ts, local, remote, payload in datagrams:
        pair = frozenset((local, remote))
        if pair not in keys or len(payload) < 16:
            continue
        tested = []
        for candidate in (keys[pair], PRE_KEY):
            counter, plain, verified = decode(payload, candidate)
            tested.append((verified, counter, plain))
            if verified:
                break
        else:
            continue
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if counter & 0x8000:
            try:
                content = lzss_decompress(content)
            except Exception:
                continue
        for message_type, length, fourth, body in parse_messages(content):
            if message_type in ROSTER_IDS and body[:1] == b"\x07" and len(body) >= 0x46:
                roster.append((ts, local, message_type, fourth, bytes(body)))
            elif message_type in RULES_IDS and body[:1] == b"\xff":
                rules.append((ts, local, message_type, bytes(body)))
            elif message_type in ROSTER_IDS and body[:1] in (b"\x02", b"\x00"):
                joiner_records.append((ts, local, message_type, fourth, bytes(body)))

    print(f"\n  -- roster entries ({len(roster)}) --")
    entries = []
    for ts, local, message_type, fourth, body in roster:
        index = body[4]
        handle = body[5]
        handle2 = body[7]
        char_id = struct.unpack_from("<I", body, 8)[0]
        addresses = body[0x13:0x1F].hex()
        name = body[0x44 : body.index(0, 0x44)].decode("latin-1")
        host = local
        entries.append((ts, local, index, handle, handle2, char_id, name))
        print(
            f"    t+{ts:9.3f} from={host:<22} type=0x{message_type:04x} b4={fourth:3d} "
            f"index=0x{index:02x} handle=0x{handle:02x} handle2=0x{handle2:02x} "
            f"char=0x{char_id:08x} addr={addresses} name={name!r}"
        )

    print(f"\n  -- join-request / control records, both directions ({len(joiner_records)}) --")
    for ts, local, message_type, fourth, body in joiner_records[:24]:
        print(
            f"    t+{ts:9.3f} from={local:<22} type=0x{message_type:04x} b4={fourth:3d} "
            f"len={len(body)} body={body[:64].hex(' ')}"
        )

    print(f"\n  -- rules-roster records with members ({len(rules)}) --")
    for ts, local, message_type, body in rules[:40]:
        members = body[1 : body.rindex(b"\xfe")].hex(" ")
        print(f"    t+{ts:9.3f} local={local:<22} type=0x{message_type:04x} body={body.hex(' ')} members=[{members}]")

    # The correlation: is the handle a function of a field we already have?
    print("\n  -- handle correlations --")
    for label, index in (("b5", 3), ("b7", 4)):
        print(f"    by {label}:")
        for ts, local, roster_index, handle, handle2, char_id, name in entries:
            value = handle if index == 3 else handle2
            port = int(local.rsplit(":", 1)[1])
            octets = [int(o) for o in local.split(":")[0].split(".")]
            print(
                f"      handle=0x{value:02x} char=0x{char_id:08x} "
                f"char_lo=0x{char_id & 0xff:02x} char_hi=0x{(char_id >> 8) & 0xff:02x} "
                f"index=0x{roster_index:02x} port={port} a3={octets[-1]} name={name!r}"
            )


def main() -> None:
    for path in sys.argv[1:]:
        analyze(path)


if __name__ == "__main__":
    main()
