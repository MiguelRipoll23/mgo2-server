"""Find what writes the native port's session-state byte, the one the lobby's
HUD dispatch reads to decide whether to raise the briefing screen.

The state lives at byte 8 of the object whose pointer sits in the global at
RVA 0x216c3c00 (the patcher reads it as `[[0x316c3c00 + base - 0x10000000] + 8]`).
`.text` encodes that global's address as the immediate 0x316c3c00, so the sites
that mention it are the readers and writers, and the byte-8 stores near each one
say which value is written and under what guard.
"""

from __future__ import annotations

import struct
import sys

from capstone import Cs, CS_ARCH_X86, CS_MODE_32

PATH = r"C:\Users\migue\Desktop\pc-client\system.dll"
BASE = 0x10000000
TEXT_VA = 0x1000
TEXT_RAW = 0x1000
TEXT_SIZE = 5853054

STATE_GLOBAL = 0x316C3C00


def immediate_sites(image: bytes, machine: Cs, value: int) -> list[int]:
    """RVA of every instruction that consumes `value` as an immediate."""
    needle = struct.pack("<I", value)
    text = image[TEXT_RAW:TEXT_RAW + TEXT_SIZE]
    sites: list[int] = []
    index = 0
    while True:
        index = text.find(needle, index)
        if index < 0:
            break
        for back in range(1, 9):
            start = index - back
            if start < 0:
                continue
            hit = False
            for instruction in machine.disasm(text[start:start + back + 4], TEXT_VA + start):
                if instruction.address + instruction.size != TEXT_VA + index + 4:
                    continue
                for operand in instruction.operands:
                    if operand.type == 2 and operand.imm == value:
                        sites.append(TEXT_VA + start)
                        hit = True
                        break
                break
            if hit:
                break
        index += 1
    return sorted(set(sites))


def window(machine: Cs, image: bytes, rva: int, before: int = 0x20, after: int = 0x60) -> None:
    start = max(TEXT_VA, rva - before)
    count = 0
    for instruction in machine.disasm(image[start:rva + after], BASE + start):
        mark = ">>" if instruction.address == BASE + rva else "  "
        print(f"  {mark}{instruction.address:08x}: {instruction.mnemonic:<8} {instruction.op_str}")
        count += 1
        if instruction.address >= BASE + rva + after:
            break


def main() -> None:
    image = open(PATH, "rb").read()
    machine = Cs(CS_ARCH_X86, CS_MODE_32)
    machine.detail = True

    sites = immediate_sites(image, machine, STATE_GLOBAL)
    print(f"{len(sites)} instruction(s) name the state global {STATE_GLOBAL:#x}")
    for site in sites:
        print(f"\n### site {BASE + site:#x}")
        window(machine, image, site)
    return 0


if __name__ == "__main__":
    sys.exit(main())
