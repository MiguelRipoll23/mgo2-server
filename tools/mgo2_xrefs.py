#!/usr/bin/env python3
"""Find callers of a vaddr in MGO2.ELF and show a disassembly window around each.

Usage: python tools/mgo2_xrefs.py <target-hex> [window-hex] [elf-path]
"""
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
from mgo2_disasm import ElfImage

DEFAULT_ELF = "docs/MGO2.ELF"


def executable_ranges(image: ElfImage) -> list[tuple[int, int, int]]:
    ranges = []
    for virtual, offset, size in image.segments:
        ranges.append((virtual, offset, size))
    return ranges


def find_calls(image: ElfImage, target: int, kind: str = "bl") -> list[int]:
    """Return every vaddr whose branch instruction targets `target`."""
    hits = []
    want_lk = 1 if kind.endswith("l") else 0
    for virtual, offset, size in image.segments:
        block = image.data[offset:offset + size]
        for index in range(0, len(block) - 3, 4):
            word = int.from_bytes(block[index:index + 4], "big")
            if (word >> 26) != 18:
                continue
            if (word & 1) != want_lk:
                continue
            address = virtual + index
            displacement = word & 0x03FFFFFC
            if displacement & 0x02000000:
                displacement -= 0x04000000
            absolute = (word >> 1) & 1
            branch_target = displacement if absolute else address + displacement
            if branch_target == target:
                hits.append(address)
    return sorted(hits)


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        raise SystemExit(1)
    target = int(sys.argv[1], 16)
    window = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0x40
    image = ElfImage(sys.argv[3] if len(sys.argv) > 3 else DEFAULT_ELF)
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    hits = find_calls(image, target)
    print(f"{len(hits)} caller(s) of 0x{target:x}")
    for hit in hits:
        print(f"\n--- caller 0x{hit:x}")
        start = hit - window
        for instruction in disassembler.disasm(image.read(start, window * 2), start):
            marker = "  <<<" if instruction.address == hit else ""
            print(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}{marker}")


if __name__ == "__main__":
    main()
