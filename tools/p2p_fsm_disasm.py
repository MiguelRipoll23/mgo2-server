#!/usr/bin/env python3
"""Recursive disassembler for the MGO2.ELF P2P 6-state machine at 0xaa1140.

Linear capstone disassembly stops at the inline jump table at 0xaa1188
(capstone halts on the first undecodable word), so this walks basic blocks
starting from each known handler entry, following direct branches.

Usage: python tools/p2p_fsm_disasm.py [elf]
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

DEFAULT_ELF = "MGO2.ELF"

# handler entry -> state (from the inline table at 0xaa1188, offsets relative to that base)
HANDLERS = {
    0xaa11a0: "state 0",
    0xaa11f4: "state 1",
    0xaa131c: "state 2",
    0xaa1560: "state 3",
    0xaa15c8: "state 4",
    0xaa1680: "state 5",
    0xaa16dc: "state >5 (default)",
}
LIMIT = 0xaa1900


class ElfImage:
    def __init__(self, path):
        with open(path, "rb") as handle:
            self.data = handle.read()
        program_header_offset = struct.unpack(">Q", self.data[0x20:0x28])[0]
        program_header_size = struct.unpack(">H", self.data[0x36:0x38])[0]
        program_header_count = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(program_header_count):
            offset = program_header_offset + (index * program_header_size)
            header = self.data[offset:offset + 56]
            segment_type, _flags = struct.unpack(">II", header[:8])
            file_offset, virtual, _phys, file_size, _mem, _align = struct.unpack(">QQQQQQ", header[8:56])
            if segment_type == 1 and file_size:
                self.segments.append((virtual, file_offset, file_size))

    def read(self, virtual, count):
        for base, offset, size in self.segments:
            if base <= virtual < base + size:
                return self.data[offset + (virtual - base):offset + (virtual - base) + count]
        return b""


def main():
    image = ElfImage(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ELF)
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)

    for entry, name in sorted(HANDLERS.items()):
        print(f"\n===== {name} @ {entry:#x} =====")
        worklist = [entry]
        seen = set()
        while worklist:
            start = worklist.pop()
            if start in seen or not (entry <= start < LIMIT):
                continue
            seen.add(start)
            code = image.read(start, LIMIT - start)
            for instruction in disassembler.disasm(code, start):
                if instruction.address >= LIMIT:
                    break
                print(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
                if instruction.mnemonic in ("blr", "bctr", "blrl"):
                    break
                if instruction.mnemonic.startswith("b"):
                    # conditional branch (e.g. bne/beq/bgt) falls through as well;
                    # plain b/ba does not.
                    target = None
                    for token in instruction.op_str.split():
                        if token.startswith("0x"):
                            target = int(token, 16)
                    if target is not None and entry <= target < LIMIT and target not in seen:
                        worklist.append(target)
                    if instruction.mnemonic in ("b", "ba"):
                        break


if __name__ == "__main__":
    main()
