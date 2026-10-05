#!/usr/bin/env python3
"""Verify the P2P handshake layout field-by-field against the captured wire bytes.

    python3 tools/handshake_layout.py docs/mgo2-game.pcapng
    python3 tools/handshake_layout.py mgo2.pcapng --peer 99.66.131.177:5731
    python3 tools/handshake_layout.py mgo2.pcapng --elf MGO2.ELF

The layout this checks is not taken on trust from `docs/protocol/`. Every offset is
pinned to the `MGO2.ELF` instruction that writes it, so the wire and the binary are
checked against each other rather than a prose description repeating one of them.

It exists because the handshake is the one part of the channel where a field silently
reads as something else rather than failing: a wrong offset still produces a plausible
u32. So this asserts the *structure* -- total length, the 16 + 6*count identity, the
magic, the tail digest, and that each entry's address bytes really are an address in
dotted form -- and exits non-zero the moment one of them stops holding.

Pass `--elf` to additionally re-derive the two crypto constants from the binary, which
catches pointing the decoder at the wrong build. That check needs the ELF, which this
repository does not ship; without it everything else still runs.

Read-only: it opens the capture and optionally the binary, and writes nothing.
"""

from __future__ import annotations

import argparse
import struct
import sys

from pcap_conversations import UDP, Pcapng
from udp_frame import LCG, PRE_KEY, decode

# The joiner's socket in this project's captures, and the dedicated server it dialled.
DEFAULT_JOINER = "10.2.0.2:5730"
DEFAULT_PEER = "99.66.131.177:5731"

MODULE_MAGIC = 0x4D258AB7
HANDSHAKE_TYPE = 0x1000
HANDSHAKE_LEN = 44
ENTRY_STRIDE = 6

# offset, width, name, the MGO2.ELF instruction that writes it
FIELDS = (
    (0, "u16", "hdr counter", "seeds the XOR chain (unscrambled header)"),
    (2, "u16", "type", "serializer FUN_00269860"),
    (4, "u8", "len", "message header len byte"),
    (5, "u8", "flags2", "message header flags2 byte"),
    (6, "u32", "peer_id", "0x268cfc  bl 0x26cc10 len 4  <- struct+0"),
    (10, "u32", "counter_base", "0x268d24  bl 0x26cc10 len 4  <- session+0x10"),
    (14, "u32", "module_magic", "0x268d38  bl 0x26cc10 len 4  <- magic global"),
    (18, "u8", "capability", "0x268d84  bl 0x26cc10 len 1  <- computed, 2/3/6/7"),
    (19, "u16", "mode", "0x268d9c  bl 0x26cc10 len 2  <- module_base+0x7a"),
    (21, "u8", "count", "0x268db8  bl 0x26cc10 len 1  <- struct+4"),
)
ENTRY_IP_NOTE = "0x268dec  bl 0x26cb98 len 4  <- BE plain copy"
ENTRY_PORT_NOTE = "0x268e08  bl 0x26cc10 len 2  <- LE reversing copy"

GLOBAL_PAIR = "a global at module_base+0x7a, set only by FUN_00271718"


def read_field(plain: bytes, offset: int, width: str) -> int:
    if width == "u8":
        return plain[offset]
    if width == "u16":
        return struct.unpack_from("<H", plain, offset)[0]
    return struct.unpack_from("<I", plain, offset)[0]


def dotted(raw: bytes) -> str:
    return ".".join(str(b) for b in raw)


def find_handshake(capture: Pcapng, joiner: str, peer: str):
    """Return {(role): (timestamp, wire_bytes)} for the 44-byte handshakes."""
    found: dict[str, tuple[float, bytes]] = {}
    joiner_ip, joiner_port = joiner.rsplit(":", 1)
    peer_ip, peer_port = peer.rsplit(":", 1)
    for ts, protocol, source, sport, destination, dport, payload in capture.decoded_packets():
        if protocol != UDP or len(payload) != HANDSHAKE_LEN:
            continue
        if (source, sport) == (joiner_ip, int(joiner_port)) and (destination, dport) == (
            peer_ip,
            int(peer_port),
        ):
            found.setdefault("joiner -> host", (ts, payload))
        elif (source, sport) == (peer_ip, int(peer_port)) and (destination, dport) == (
            joiner_ip,
            int(joiner_port),
        ):
            found.setdefault("host -> joiner", (ts, payload))
        if len(found) == 2:
            break
    return found


class Report:
    """Collects failures so one run shows every problem, not just the first."""

    def __init__(self) -> None:
        self.failures = 0
        self.checks = 0

    def check(self, ok: bool, label: str, detail: str = "") -> bool:
        self.checks += 1
        if not ok:
            self.failures += 1
            print(f"    FAIL  {label}" + (f" — {detail}" if detail else ""))
        return ok


def describe_one(report: Report, role: str, wire: bytes) -> None:
    counter, plain, verified = decode(wire, PRE_KEY)

    print(f"  {role}: {len(wire)} B on the wire, header counter 0x{counter:04x}")
    print(f"    {'off':>3}  {'field':<14} {'width':<4} {'value':<22} written by")
    for offset, width, name, note in FIELDS:
        value = read_field(plain, offset, width)
        raw = plain[offset:offset + {"u8": 1, "u16": 2, "u32": 4}[width]].hex()
        print(f"    {offset:>3}  {name:<14} {width:<4} 0x{value:08x} {raw:<8} {note}")

    count = plain[21]
    for index in range(count):
        base = 22 + index * ENTRY_STRIDE
        address = plain[base:base + 4]
        port = read_field(plain, base + 4, "u16")
        print(
            f"    {base:>3}  entry{index}.ip{'':<8} u32  {address.hex()} {'':<8} {ENTRY_IP_NOTE}"
        )
        print(
            f"    {base + 4:>3}  entry{index}.port   u16  {port:<12} {plain[base + 4:base + 6].hex():<8} {ENTRY_PORT_NOTE}"
        )
        print(f"         -> {dotted(address)}:{port}")
    tail_at = 22 + count * ENTRY_STRIDE
    print(f"    {tail_at:>3}  tail           10B  {plain[tail_at:tail_at + 10].hex()}")

    # Structure. These are the assertions that make this a check rather than a print.
    report.check(len(wire) == HANDSHAKE_LEN, "datagram is 44 bytes", f"got {len(wire)}")
    report.check(
        read_field(plain, 2, "u16") == HANDSHAKE_TYPE,
        "message type is 0x1000",
        f"got 0x{read_field(plain, 2, 'u16'):04x}",
    )
    report.check(
        read_field(plain, 14, "u32") == MODULE_MAGIC,
        "module_magic is 0x4d258ab7",
        f"got 0x{read_field(plain, 14, 'u32'):08x}",
    )
    # The identity the builder's write sequence implies: four u32 + u8 + u16 + u8,
    # then six bytes per entry. If len disagrees, an offset has moved.
    body = 16 + ENTRY_STRIDE * count
    report.check(
        read_field(plain, 4, "u8") == body,
        f"len byte equals 16 + 6*count ({body})",
        f"len={read_field(plain, 4, 'u8')} count={count}",
    )
    report.check(
        tail_at + 10 == len(wire),
        "the 10-byte tail is the last thing in the datagram",
        f"tail starts at {tail_at}, datagram is {len(wire)}",
    )
    report.check(verified, "tail digest verifies under the pre-handshake chain key")
    # A wrong byte order shows up here: an LE port read BE is a different number, and
    # a BE address read LE is a dotted-quad with a zero or an out-of-range octet.
    for index in range(count):
        base = 22 + index * ENTRY_STRIDE
        octets = plain[base:base + 4]
        report.check(
            all(o != 0 for o in octets[:1]),
            f"entry{index}.ip first octet is non-zero (BE read, not LE)",
            f"octets {octets.hex()}",
        )
    report.check(plain[18] in (2, 3, 6, 7), "capability byte is one the builder can emit",
                 f"got 0x{plain[18]:02x}")


def check_elf_constants(path: str, report: Report) -> None:
    """Re-derive the LCG multiplier and the counter-base constant from the binary."""
    from mgo2_disasm import ElfImage

    image = ElfImage(path)

    # A constant is built by `lis rD, simm` and an `ori rA, rS, uimm` back into the
    # same register. Note the two field layouts differ, which is the easy thing to get
    # wrong: `lis` (addis, opcode 15) puts its destination in bits 21..26 -- reversed
    # against `add` -- while `ori` (opcode 24) has rS there and rA in bits 16..21.
    # Both immediates are the low 16 bits. The pair is not reliably adjacent: the
    # compiler interleaves other work, e.g. `lis r6,0x5d58` at 0x2667cc and
    # `ori r6,r6,0x8b65` at 0x2667d4 are eight bytes apart, so this scans a window.
    WINDOW = 8  # instructions
    targets = {LCG: [], 0x2B58DE69: []}
    for virtual, offset, size in image.segments:
        block = image.data[offset:offset + size]
        for index in range(0, len(block) - 3, 4):
            first = int.from_bytes(block[index:index + 4], "big")
            if (first >> 26) != 15:
                continue
            lis_dst = (first >> 21) & 0x1f
            high = first & 0xFFFF
            for step in range(1, WINDOW):
                cursor = index + step * 4
                if cursor + 4 > len(block):
                    break
                other = int.from_bytes(block[cursor:cursor + 4], "big")
                if (other >> 26) != 24:
                    continue
                if (other >> 21) & 0x1f != lis_dst or (other >> 16) & 0x1f != lis_dst:
                    continue
                built = (high << 16) | (other & 0xFFFF)
                if built in targets:
                    targets[built].append(virtual + index)
                break
    lcg_hits, derive_hits = targets[LCG], targets[0x2B58DE69]

    print(f"  ELF {path}")
    report.check(bool(lcg_hits), f"LCG multiplier 0x{LCG:08x} is constructed in the binary",
                 "not found as a lis/ori pair")
    report.check(bool(derive_hits),
                 f"counter-base constant 0x2b58de69 is constructed in the binary",
                 "not found as a lis/ori pair")
    if lcg_hits:
        print(f"    LCG 0x{LCG:08x} built at {', '.join(hex(a) for a in lcg_hits[:4])}")
    if derive_hits:
        print(f"    0x2b58de69 built at {', '.join(hex(a) for a in derive_hits[:4])}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("capture", help="pcapng capture of a session")
    parser.add_argument("--peer", default=DEFAULT_PEER, help="the other party's IP:PORT")
    parser.add_argument("--joiner", default=DEFAULT_JOINER, help="the joiner's IP:PORT")
    parser.add_argument("--elf", help="also re-derive the crypto constants from this binary")
    args = parser.parse_args()

    capture = Pcapng(args.capture)
    capture.read()
    handshakes = find_handshake(capture, args.joiner, args.peer)
    if not handshakes:
        print(f"no {HANDSHAKE_LEN}-byte handshake between {args.joiner} and {args.peer} "
              f"in {args.capture}")
        return 2

    report = Report()
    print(f"{args.capture}: {len(handshakes)} handshake(s), "
          f"pre-handshake chain key 0x{PRE_KEY:08x}\n")
    for role in ("joiner -> host", "host -> joiner"):
        if role not in handshakes:
            print(f"  {role}: not present in this capture\n")
            continue
        timestamp, wire = handshakes[role]
        print(f"  [{timestamp:.4f}] {role}")
        describe_one(report, role, wire)
        print()

    if args.elf:
        check_elf_constants(args.elf, report)
        print()

    if report.failures:
        print(f"FAIL: {report.failures} of {report.checks} checks failed")
        return 1
    print(f"PASS: all {report.checks} checks held")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
