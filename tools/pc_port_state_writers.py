"""Find what writes the native port's session-state byte.

`0x100a0500(slot)` returns the address of the session record's data area
(`[[0x316c3c00 + slot*4] + 8]`), and the state the lobby's HUD dispatch compares
at `0x10152310` is byte 0 of it (`0x100a1660` is `0x100a0500(0)` + `[eax]`).
Calls to the accessor are direct, so every writer is a byte store to `[eax]`
(or a reloaded register) within a few instructions of a call to it.
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

ACCESSOR = 0x100A0500


def call_sites(image: bytes, target: int) -> list[int]:
    text = image[TEXT_RAW:TEXT_RAW + TEXT_SIZE]
    sites: list[int] = []
    for index in range(len(text) - 5):
        if text[index] != 0xE8:
            continue
        rel = struct.unpack_from("<i", text, index + 1)[0]
        if BASE + TEXT_VA + index + 5 + rel == target:
            sites.append(TEXT_VA + index)
    return sites


def main() -> None:
    image = open(PATH, "rb").read()
    machine = Cs(CS_ARCH_X86, CS_MODE_32)
    machine.detail = True

    sites = call_sites(image, ACCESSOR)
    print(f"{len(sites)} call(s) to the session accessor")

    stores = 0
    for site in sites:
        body = image[site:site + 0x120]
        instructions = list(machine.disasm(body, BASE + site))
        for position, instruction in enumerate(instructions):
            if instruction.mnemonic != "mov" or len(instruction.operands) != 2:
                continue
            destination, source = instruction.operands
            if destination.type != 3:  # X86_OP_MEM
                continue
            if destination.size != 1 or source.type != 2:
                continue
            if not 0 <= source.imm <= 8:
                continue
            stores += 1
            print(f"\n### store {instruction.address:#x}: {instruction.mnemonic} {instruction.op_str}"
                  f"   (accessor call at {BASE + site:#x})")
            start = max(TEXT_VA, site - 0x30)
            for nearby in machine.disasm(image[start:site + 0x60], BASE + start):
                mark = ">>" if nearby.address in (instruction.address, BASE + site) else "  "
                print(f"  {mark}{nearby.address:08x}: {nearby.mnemonic:<8} {nearby.op_str}")
                if nearby.address >= BASE + site + 0x50:
                    break
    print(f"\n{stores} byte store(s) to the session record near an accessor call")
    return 0


if __name__ == "__main__":
    sys.exit(main())
