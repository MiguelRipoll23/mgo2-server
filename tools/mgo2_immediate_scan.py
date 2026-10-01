#!/usr/bin/env python3
"""Scan MGO2.ELF for instructions whose operand mentions one of the given immediates.

Usage: python tools/mgo2_immediate_scan.py 0x5000 0x5001
"""
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
from mgo2_disasm import ElfImage

DEFAULT_ELF = "MGO2.ELF"


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        raise SystemExit(1)
    needles = [value.lower() for value in sys.argv[1:]]
    image = ElfImage(DEFAULT_ELF)
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    disassembler.skipdata = False
    chunk_size = 0x40000
    for virtual, offset, size in image.segments:
        if size == 0:
            continue
        position = 0
        while position < size:
            length = min(chunk_size, size - position)
            block = image.data[offset + position:offset + position + length]
            decoded = 0
            for instruction in disassembler.disasm(block, virtual + position):
                operand = instruction.op_str.lower()
                if any(needle in operand for needle in needles):
                    print(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
                decoded = instruction.address + instruction.size - (virtual + position)
            # capstone stops at the first word it cannot decode; resume after it.
            position += decoded if decoded > 0 else 4


if __name__ == "__main__":
    main()
