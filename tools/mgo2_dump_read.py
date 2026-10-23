#!/usr/bin/env python3
"""Read guest memory out of an RPCS3 Windows minidump.

Usage:
    python3 tools/mgo2_dump_read.py regions rpcs3.dmp
    python3 tools/mgo2_dump_read.py read rpcs3.dmp 0x011A6308 32
    python3 tools/mgo2_dump_read.py find rpcs3.dmp 85f3fe00ff
    python3 tools/mgo2_dump_read.py find rpcs3.dmp 85f3fe00ff --limit 20

The dump is a standard `MDMP` file whose Memory64List (stream type 9) carries the
whole of the process's committed memory, so RPCS3's guest mappings are in it at
the host address `guest + 0x300000000` -- a guest address is the dump address minus
that constant (`docs/analysis/udp-record-dispatch.md` §1).

Every subcommand takes the dump path first because the file is large and a reader
that opened it implicitly would hide how much of it is being walked.
"""

from __future__ import annotations

import struct
import sys

GUEST_BIAS = 0x300000000

MEMORY64_STREAM = 9


def memory_ranges(path: str) -> list[tuple[int, int, int]]:
    """Return (start, size, file offset) for every range the dump carries."""
    with open(path, "rb") as dump:
        signature, _, stream_count, directory = struct.unpack("<4sIII", dump.read(16))
        if signature != b"MDMP":
            raise SystemExit(f"{path}: not a minidump")

        dump.seek(directory)
        entries = [struct.unpack("<III", dump.read(12)) for _ in range(stream_count)]

        for stream_type, _, offset in entries:
            if stream_type != MEMORY64_STREAM:
                continue
            dump.seek(offset)
            count, data = struct.unpack("<QQ", dump.read(16))
            ranges, cursor = [], data
            for _ in range(count):
                start, size = struct.unpack("<QQ", dump.read(16))
                ranges.append((start, size, cursor))
                cursor += size
            return ranges

    raise SystemExit(f"{path}: no Memory64List")


def read(ranges: list[tuple[int, int, int]], address: int, size: int) -> bytes:
    """Read guest bytes, or fewer when the range ends or the address is unmapped."""
    start, length, offset = address + GUEST_BIAS, size, None
    for range_start, range_size, range_offset in ranges:
        if range_start <= start < range_start + range_size:
            offset = range_offset + start - range_start
            length = min(size, range_start + range_size - start)
            break
    if offset is None:
        return b""

    with open(DUMP, "rb") as dump:
        dump.seek(offset)
        return dump.read(length)


def find(ranges: list[tuple[int, int, int]], pattern: bytes, limit: int) -> list[int]:
    """Return the guest addresses a byte pattern sits at, lowest first."""
    hits, chunk_size = [], 1 << 24
    for range_start, range_size, range_offset in ranges:
        if not GUEST_BIAS <= range_start < GUEST_BIAS + 0x100000000:
            continue
        cursor = 0
        with open(DUMP, "rb") as dump:
            dump.seek(range_offset)
            while cursor < range_size and len(hits) < limit:
                # Each read starts a pattern's length behind the cursor so a match
                # straddling two chunks is seen whole.
                dump.seek(range_offset + cursor)
                chunk = dump.read(min(chunk_size, range_size - cursor))
                if not chunk:
                    break
                at = chunk.find(pattern)
                while at >= 0 and len(hits) < limit:
                    hits.append(range_start - GUEST_BIAS + cursor + at)
                    at = chunk.find(pattern, at + 1)
                cursor += chunk_size - len(pattern) + 1
        if len(hits) >= limit:
            break
    return sorted(hits)


def main(argv: list[str]) -> int:
    global DUMP

    if len(argv) < 3:
        print(__doc__)
        return 2

    command, DUMP = argv[1], argv[2]
    ranges = memory_ranges(DUMP)

    if command == "regions":
        for address, size, _ in ranges:
            if GUEST_BIAS <= address < GUEST_BIAS + 0x100000000:
                print(f"guest {address - GUEST_BIAS:>#011x} size {size:#x}")
        return 0

    if command == "read":
        address = int(argv[3], 0)
        size = int(argv[4], 0) if len(argv) > 4 else 16
        data = read(ranges, address, size)
        if not data:
            print(f"{address:#x}: not mapped")
            return 1
        for at in range(0, len(data), 16):
            row = data[at : at + 16]
            print(f"{address + at:#011x}  {row.hex(' '):<47}  {''.join(chr(b) if 32 <= b < 127 else '.' for b in row)}")
        return 0

    if command == "find":
        pattern = bytes.fromhex(argv[3])
        limit = int(argv[5]) if len(argv) > 5 and argv[4] == "--limit" else 20
        for address in find(ranges, pattern, limit):
            print(f"{address:#011x}")
        return 0

    print(__doc__)
    return 2


DUMP = ""

if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
