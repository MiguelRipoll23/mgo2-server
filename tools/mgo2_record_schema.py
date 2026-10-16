#!/usr/bin/env python3
"""Read the 26-state schema out of MGO2.ELF.

`0x27E8B0` (the module object's vtable slot 0) reads one byte, indexes a table
of 26 descriptors with it (`(byte << 2) & 0x3FC`, so the code is written for a
256-entry table while the module fills 26), reads `ceil(count / 8)` bytes of
presence map and moves each present field between the state block and the
stream (`0x27EC48`). The 26 entries are **states**, not the operations stream's
opcodes:

* `0x01`-`0x18` share one 164-field table, and their state blocks are
  consecutive 1248-byte allocations (`0x01876F88 + k * 0x4E0`), which is the
  session's 24 slots;
* their `count * 2`-bit maps do too (`0x0187E488 + k * 0x29`, 41 bytes), ending
  where `0x19`'s map begins; and
* `0x00` (168 bytes) and `0x19` (284 bytes) are states of their own.

Each descriptor is `0x1c` bytes:

    +0x00  the number of fields the state has
    +0x04  the field table: that many 10-byte entries, contiguous
    +0x08  the state block, a run-time pointer
    +0x0c  the state's own map, `count * 2` bits wide, run time
    +0x10  the handler object (`0x27EC48` is its slot 0)
    +0x14  the size of the state block the field entries address
    +0x18  a run-time word

A field entry is

    +0  u16  offset into the state block
    +2  u16  length in bytes
    +4  u16  a multiplier `0x27EC48` applies to the length
    +6  u16  flags:
              bit `0x200`  the field is a container: its own bytes do not move,
                           the fields nested in its range are walked instead
                           (`0x27EC98`, out through `0x27EE10`)
              bit `0x100`  its bytes move as one block of `length * multiplier`
                           (`0x27ED28`, `0x27ED5C`)
              neither      `multiplier` elements of `length` bytes each
                           (`0x27EDBC`), and nothing at all when the multiplier
                           is 0; every path then marks the field in the state's
                           map with the two-bit code 2 (`0x27ED68`)
              the low byte (`0x24`, `0x25`, `0x35`, `0x40`, `0x64`) is read by
              none of those branches; the one-field setter `0x27E578` tests
              `0x0001` and `0x0040` of it (`0x27E6A8`, `0x27E6BC`), which is a
              per-field publication gate
              (`docs/analysis/udp-handlers-and-senders.md` §2,
              `docs/analysis/udp-record-dispatch.md` §3.6)
    +8  u16  spare

`+0x14` is what makes the tables checkable: a state's fields end exactly at its
block's size, which is why the summary prints both. The dispatch table itself is
a run-time allocation (`*(0x011A646C)`) whose other two segments are a 24-entry
pool and a third array, not descriptors; `tools/mgo2_state_wire_check.py` tests
this schema against the captured record bodies.

Usage:
    python tools/mgo2_record_schema.py              # one line per state
    python tools/mgo2_record_schema.py --op 0x01    # one state's fields
    python tools/mgo2_record_schema.py --op 0x01 --all
"""
import struct
import sys

DEFAULT_ELF = "MGO2.ELF"
DESCRIPTORS = 0x012298A8
DESCRIPTOR_STRIDE = 0x1C
OPERATIONS = 26
FIELD_STRIDE = 10


class ElfImage:
    """The loaded segments of MGO2.ELF, by address."""

    def __init__(self, path):
        with open(path, "rb") as handle:
            self.data = handle.read()
        if self.data[:4] != b"\x7fELF":
            raise ValueError("%s is not an ELF file" % path)
        program_header_offset = struct.unpack(">Q", self.data[0x20:0x28])[0]
        program_header_size = struct.unpack(">H", self.data[0x36:0x38])[0]
        program_header_count = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(program_header_count):
            offset = program_header_offset + (index * program_header_size)
            kind, _flags = struct.unpack(">II", self.data[offset:offset + 8])
            file_offset, virtual, _phys, file_size = struct.unpack(
                ">QQQQ", self.data[offset + 8:offset + 0x28])
            if kind == 1 and file_size:
                self.segments.append((virtual, file_offset, file_size))

    def offset_of(self, address):
        for virtual, file_offset, file_size in self.segments:
            if virtual <= address < virtual + file_size:
                return file_offset + (address - virtual)
        return None

    def word(self, address):
        offset = self.offset_of(address)
        if offset is None:
            raise ValueError("0x%08x is not a loaded segment" % address)
        return struct.unpack(">I", self.data[offset:offset + 4])[0]

    def halfword(self, address):
        offset = self.offset_of(address)
        if offset is None:
            raise ValueError("0x%08x is not a loaded segment" % address)
        return struct.unpack(">H", self.data[offset:offset + 2])[0]


def schema(image, operation):
    """One state's descriptor, and the fields its table lists."""
    base = DESCRIPTORS + (operation * DESCRIPTOR_STRIDE)
    count = image.word(base)
    table = image.word(base + 4)
    block = image.word(base + 0x14)
    fields = []
    for index in range(count):
        entry = table + (index * FIELD_STRIDE)
        fields.append((
            index,
            image.halfword(entry),
            image.halfword(entry + 2),
            image.halfword(entry + 4),
            image.halfword(entry + 6),
        ))
    return count, table, block, fields


def coverage(fields):
    """The last byte an operation's fields address: the state block it needs."""
    return max(offset + (length * multiplier)
               for _index, offset, length, multiplier, _flags in fields)


def main():
    arguments = sys.argv[1:]
    path = DEFAULT_ELF
    if "--elf" in arguments:
        path = arguments[arguments.index("--elf") + 1]
    image = ElfImage(path)
    operation = None
    if "--op" in arguments:
        operation = int(arguments[arguments.index("--op") + 1], 16)

    if operation is not None:
        count, table, block, fields = schema(image, operation)
        print("state 0x%02x: %d fields, table 0x%08x, state block %d bytes"
              % (operation, count, table, block))
        for index, offset, length, multiplier, flags in fields:
            if "--all" not in arguments and index > 16:
                continue
            print("   [%3d] state+%-5d %5d byte(s) x%d  flags 0x%04x"
                  % (index, offset, length, multiplier, flags))
        print("   fields end at %d of the %d-byte block" % (coverage(fields), block))
        print("   flags: 0x200 the field is a container (its nested fields move instead),")
        print("          0x100 its bytes move as one block of length x multiplier,")
        print("          neither: multiplier elements of length bytes; the low byte is [U]")
        return

    print("idx fields  table       block  fields end  flags")
    distinct = []
    for index in range(OPERATIONS):
        count, table, block, fields = schema(image, index)
        if not distinct or distinct[-1][0] != table:
            distinct.append((table, count))
        flags = sorted({entry[4] for entry in fields})
        print("0x%02x %6d  0x%08x  %5d  %9d  %s"
              % (index, count, table, block, coverage(fields),
                 " ".join("%04x" % flag for flag in flags[:6])))
    print()
    for table, count in distinct:
        print("table 0x%08x: %3d fields, 0x%08x..0x%08x"
              % (table, count, table, table + (count * FIELD_STRIDE)))
    ends = [table + (count * FIELD_STRIDE) for table, count in distinct]
    starts = [table for table, _count in distinct]
    print("the tables are back to back: %s"
          % ("yes" if ends[:-1] == starts[1:] else "no (ends %s against starts %s)"
             % (["0x%08x" % end for end in ends[:-1]], ["0x%08x" % start for start in starts[1:]])))


if __name__ == "__main__":
    main()
