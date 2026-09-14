#!/usr/bin/env python3
"""Disassemble a range of MGO2.ELF with Capstone.

Usage: python tools/mgo2_disasm.py <start-hex> <end-hex> [elf-path]
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

DEFAULT_ELF = "docs/MGO2.ELF"


class ElfImage:
    """Loads the PT_LOAD segments of an ELF64 PPC64 big-endian image."""

    def __init__(self, path: str) -> None:
        with open(path, "rb") as handle:
            self.data = handle.read()
        if self.data[:4] != b"\x7fELF":
            raise ValueError(f"{path} is not an ELF file")
        program_header_offset = struct.unpack(">Q", self.data[0x20:0x28])[0]
        program_header_size = struct.unpack(">H", self.data[0x36:0x38])[0]
        program_header_count = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(program_header_count):
            offset = program_header_offset + (index * program_header_size)
            header = self.data[offset:offset + 56]
            segment_type, _flags = struct.unpack(">II", header[:8])
            file_offset, virtual_address, _physical, file_size, _memory, _align = struct.unpack(
                ">QQQQQQ", header[8:56])
            if segment_type == 1:  # PT_LOAD
                self.segments.append((virtual_address, file_offset, file_size))

    def to_offset(self, virtual_address: int) -> int | None:
        for virtual, offset, size in self.segments:
            if virtual <= virtual_address < virtual + size:
                return offset + (virtual_address - virtual)
        return None

    def read(self, virtual_address: int, count: int) -> bytes:
        offset = self.to_offset(virtual_address)
        if offset is None:
            raise ValueError(f"0x{virtual_address:x} is not mapped")
        return self.data[offset:offset + count]


def main() -> None:
    if len(sys.argv) < 3:
        print(__doc__)
        raise SystemExit(1)
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    image = ElfImage(sys.argv[3] if len(sys.argv) > 3 else DEFAULT_ELF)
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    for instruction in disassembler.disasm(image.read(start, end - start), start):
        print(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")


if __name__ == "__main__":
    main()
