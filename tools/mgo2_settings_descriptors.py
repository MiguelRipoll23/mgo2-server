#!/usr/bin/env python3
"""Decode the settings descriptor records walked by FUN_0027e480.

The accessor at 0x27e480 walks a 26-entry table of settings objects; each entry
names a record array, and every record is ten bytes:

    u16 key    byte offset of the field inside the settings object
    u16 length field width in bytes
    u16 stride always 1 in every record read so far
    u16 flags  a per-field mask (0x125, 0x25, 0x64, ...)
    u16 zero

Readers ask for a field by (key, length), so this table is the object's field
map: it is what turns the 283 call sites of the accessor into names.

Usage: python tools/mgo2_settings_descriptors.py [elf-path]
"""
import struct
import sys

from mgo2_disasm import ElfImage

ANCHOR = b"\x00\x15\x00\x30"  # key 0x15, length 48: the rotation triple block
RECORD_SIZE = 10
DEFAULT_ELF = ".game/ps3/MGO2.ELF"


def find_all(data: bytes, pattern: bytes) -> list[int]:
    hits, position = [], 0
    while True:
        index = data.find(pattern, position)
        if index < 0:
            return hits
        hits.append(index)
        position = index + 1


def to_va(image: ElfImage, offset: int) -> int:
    for virtual, start, size in image.segments:
        if start <= offset < start + size:
            return virtual + (offset - start)
    return 0


def record_at(data: bytes, offset: int) -> tuple[int, int, int, int, int]:
    return struct.unpack(">5H", data[offset:offset + RECORD_SIZE])


def is_record(values: tuple[int, ...]) -> bool:
    key, length, stride, _flags, zero = values
    return 0 <= key <= 0x1000 and 0 < length <= 0x1000 and stride in (1, 2, 4) and zero == 0


def main() -> None:
    image = ElfImage(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ELF)
    data = image.data

    anchors = find_all(data, ANCHOR)
    if not anchors:
        print("no rotation record found; the anchor key 0x15/48 is missing")
        raise SystemExit(1)

    starts = []
    for anchor in anchors:
        start = anchor
        while start - RECORD_SIZE >= 0 and is_record(record_at(data, start - RECORD_SIZE)):
            start -= RECORD_SIZE
        end = anchor + RECORD_SIZE
        while end + RECORD_SIZE <= len(data) and is_record(record_at(data, end)):
            end += RECORD_SIZE
        starts.append((start, end))

    for start, end in starts:
        print(f"== record table: file 0x{start:07x}..0x{end:07x} "
              f"(va 0x{to_va(image, start):08x}), "
              f"{(end - start) // RECORD_SIZE} records")
        for index in range((end - start) // RECORD_SIZE):
            offset = start + index * RECORD_SIZE
            key, length, stride, flags, _zero = record_at(data, offset)
            note = ""
            if (key, length) == (0x15, 48):
                note = "  <- rotation: 16 triples of map, rule, third byte"
            elif (key, length) == (0x52, 1):
                note = "  <- current rotation slot"
            print(f"  record[{index:3}] key=0x{key:02x}({key:3}) "
                  f"len={length:3} stride={stride} flags=0x{flags:04x}{note}")


if __name__ == "__main__":
    main()
