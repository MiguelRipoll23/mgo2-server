#!/usr/bin/env python3
"""Analyze the character-info packet handler (0x4101) of MGO2.ELF with Capstone.

Usage: python tools/mgo2_stats_analysis.py [elf-path]

FUN_00f08a18 is the live parser: the dispatcher FUN_00f0494c routes 0x4101 to
it and its only call site is the dispatcher stub at 0xf052ac. Field accessors
are the same positional stream readers the personal-stats handlers use.
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

DEFAULT_ELF = "docs/MGO2.ELF"

HANDLERS = {
    "0x4101 charinfo FUN_00f08a18": 0x00F08A18,
}

READER_MNEMONICS = {"bl", "b", "ba", "bla", "bctr", "bctrl"}

READER_NAMES = {
    0x00F2DDEC: "f2ddec: reset stream (state=8, pos=0)",
    0x00F2DE00: "f2de00: mark processed (state=9)",
    0x00F2E20C: "f2e20c: read 4 bytes -> buf",
    0x00F2E280: "f2e280: read 4 bytes -> buf",
    0x00F2E134: "f2e134: read 1 byte -> buf",
    0x00F2E0FC: "f2e0fc: read 1 byte -> buf",
    0x00F2E1BC: "f2e1bc: read 2 bytes -> buf",
    0x00F2E5C0: "f2e5c0: copy r5 bytes -> buf",
    0x00F04920: "f04920: payload accessor",
    0x00FA4D48: "fa4d48: memset(r3, r4, r5)",
}


class ElfImage:
    """Loads the PT_LOAD segments of an ELF64 PPC64 big-endian image."""

    def __init__(self, path: str) -> None:
        with open(path, "rb") as handle:
            self.data = handle.read()
        if self.data[:4] != b"\x7fELF":
            raise ValueError(f"{path} is not an ELF file")
        phoff = struct.unpack(">Q", self.data[0x20:0x28])[0]
        phentsize = struct.unpack(">H", self.data[0x36:0x38])[0]
        phnum = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(phnum):
            base = phoff + (index * phentsize)
            header = self.data[base:base + 56]
            seg_type, _flags = struct.unpack(">II", header[:8])
            file_offset, vaddr, _paddr, filesz, _memsz, _align = struct.unpack(">QQQQQQ", header[8:56])
            if seg_type == 1:
                self.segments.append((vaddr, file_offset, filesz))

    def to_offset(self, vaddr: int) -> int | None:
        for virtual, offset, size in self.segments:
            if virtual <= vaddr < virtual + size:
                return offset + (vaddr - virtual)
        return None

    def read(self, vaddr: int, count: int) -> bytes:
        offset = self.to_offset(vaddr)
        if offset is None:
            raise ValueError(f"0x{vaddr:x} is not mapped")
        return self.data[offset:offset + count]


def extract_call_target(text: str) -> int | None:
    parts = text.replace(",", " ").split()
    for part in parts:
        if part.startswith("0x"):
            try:
                return int(part, 16)
            except ValueError:
                return None
    return None


def analyze(image: ElfImage, start: int, max_instructions: int) -> None:
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    code = image.read(start, max_instructions * 4)
    for instruction in disassembler.disasm(code, start):
        call = ""
        if instruction.mnemonic in READER_MNEMONICS:
            target = extract_call_target(instruction.op_str)
            if target is not None:
                call = f"   ; <== {READER_NAMES.get(target, hex(target))}"
        print(f"  {instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}{call}")


def main() -> None:
    image = ElfImage(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ELF)
    for label, code_address in HANDLERS.items():
        print(f"== {label}: code 0x{code_address:08x} ==")
        analyze(image, code_address, 700)
        print()


if __name__ == "__main__":
    main()
