#!/usr/bin/env python3
"""Recursively disassemble one PPC64 big-endian function from MGO2.ELF.

Capstone stops at the first non-code word (inline jump tables), so this walks
basic blocks from an entry point and follows direct branches, which is how the
MGO2 inline jump tables (e.g. the P2P FSM table at 0xaa1188) are crossed.

Usage: python tools/ppc_function_disasm.py <entry-hex> [elf] [span-bytes]
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

DEFAULT_ELF = "MGO2.ELF"


class ElfImage:
    def __init__(self, path):
        with open(path, "rb") as handle:
            self.data = handle.read()
        phoff = struct.unpack(">Q", self.data[0x20:0x28])[0]
        phent = struct.unpack(">H", self.data[0x36:0x38])[0]
        phnum = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(phnum):
            offset = phoff + (index * phent)
            segment_type, _flags = struct.unpack(">II", self.data[offset:offset + 8])
            file_offset, virtual, _phys, file_size, _mem, _align = struct.unpack(
                ">QQQQQQ", self.data[offset + 8:offset + 56])
            if segment_type == 1 and file_size:
                self.segments.append((virtual, file_offset, file_size))

    def read(self, virtual, count):
        for base, offset, size in self.segments:
            if base <= virtual < base + size:
                return self.data[offset + (virtual - base):offset + (virtual - base) + count]
        return b""


def walk(image, disassembler, entry, end):
    worklist = [entry]
    seen = set()
    while worklist:
        start = worklist.pop()
        if start in seen or not (entry <= start < end):
            continue
        seen.add(start)
        for instruction in disassembler.disasm(image.read(start, end - start), start):
            if instruction.address >= end:
                break
            print(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
            if instruction.mnemonic in ("blr", "bctr", "blrl"):
                break
            if instruction.mnemonic.startswith("b"):
                target = None
                for token in instruction.op_str.split():
                    if token.startswith("0x"):
                        target = int(token, 16)
                if target is not None and entry <= target < end and target not in seen:
                    worklist.append(target)
                if instruction.mnemonic in ("b", "ba"):
                    break


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        raise SystemExit(1)
    entry = int(sys.argv[1], 16)
    image = ElfImage(sys.argv[2] if len(sys.argv) > 2 else DEFAULT_ELF)
    span = int(sys.argv[3], 0) if len(sys.argv) > 3 else 0x400
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    print(f"===== function {entry:#x} (+{span:#x}) =====")
    walk(image, disassembler, entry, entry + span)


if __name__ == "__main__":
    main()
