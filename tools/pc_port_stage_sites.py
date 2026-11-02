"""Disassemble the native port's sites that name the 0x1001-family fields.

The port's system.dll is a 32-bit PE linked at 0x10000000 (its own PE header
says 0x596000, but every absolute reference in .text carries the 0x10000000
base, which is also the base the game log prints). .text and .rdata have
PointerToRawData == VirtualAddress, so a rebased virtual address minus
0x10000000 is the file offset.
"""

import struct
import sys

from capstone import Cs, CS_ARCH_X86, CS_MODE_32

PATH = r"C:\Users\migue\Desktop\pc-client\system.dll"
BASE = 0x10000000


def main() -> None:
    image = open(PATH, "rb").read()
    print(f"image {len(image)} bytes")
    machine = Cs(CS_ARCH_X86, CS_MODE_32)

    sites = [
        ("SV_GetInitialPosDir failure", 0x17B4E0),
        ("sv_custom_point over action", 0x1795C0),
        ("sv_net actual size", 0x1F09B9),
    ]
    for label, address in sites:
        start = address - 0x180
        offset = start
        print(f"\n===== {label} at {address:#x}, window {start:#x}, file offset {offset:#x}")
        if offset < 0 or offset + 0x240 > len(image):
            print("   outside the image")
            continue
        chunk = image[offset:offset + 0x240]
        decoded = 0
        for instruction in machine.disasm(chunk, BASE + start):
            mark = ">>" if abs(instruction.address - (BASE + address)) < 8 else "  "
            print(f" {mark}{instruction.address:08x}: {instruction.mnemonic:<8} {instruction.op_str}")
            decoded += 1
        print(f"   {decoded} instructions decoded")


if __name__ == "__main__":
    sys.exit(main())
