"""Resolve string references in the native port's system.dll and disassemble the
functions that use them.

The port is a 32-bit PE built at 0x10000000 (the base its own log prints; the
PE header still says 0x596000). `.text` and `.rdata` have PointerToRawData ==
VirtualAddress, so RVA == file offset for both, and a runtime address is
`0x10000000 + RVA`.

A raw search for a 32-bit literal finds data as well as code, so each hit is
accepted only when some instruction boundary within eight bytes consumes it as
an immediate operand.
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
RDATA_VA = 0x596000
RDATA_RAW = 0x596000
RDATA_SIZE = 11708507


def string_rva(image: bytes, text: str) -> int | None:
    offset = image.find(text.encode())
    if offset < 0:
        return None
    if RDATA_RAW <= offset < RDATA_RAW + RDATA_SIZE:
        return RDATA_VA + (offset - RDATA_RAW)
    return None


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
            for instruction in machine.disasm(text[start:start + back + 4], TEXT_VA + start):
                if instruction.address + instruction.size != TEXT_VA + index + 4:
                    continue
                for operand in instruction.operands:
                    if operand.type == 2 and operand.imm == value:  # X86_OP_IMM
                        sites.append(TEXT_VA + start)
                        break
                break
            else:
                continue
            break
        index += 1
    return sorted(set(sites))


def function_start(image: bytes, rva: int) -> int:
    """Walk back to the alignment padding MSVC leaves before a function."""
    limit = rva - 0x200
    for candidate in range(rva, max(limit, TEXT_VA), -1):
        offset = candidate
        if image[offset - 1] == 0xCC and image[offset] != 0xCC:
            return candidate
    return rva - 0x60


def main() -> None:
    image = open(PATH, "rb").read()
    machine = Cs(CS_ARCH_X86, CS_MODE_32)
    machine.detail = True

    probes = [
        "SV_GetInitialPosDir Failed :: teamno %d",
        "IRREGAL RESPAWN POSITION %f %f %f >>> FLOOR NOT FOUND !!!!",
        "PL_NetSetRespawn :: main player not found !! %d",
        "over action %d ",
        "actual size over %d %d ",
    ]
    for text in probes:
        rva = string_rva(image, text)
        if rva is None:
            print(f"\n### {text!r}: string not found in .rdata")
            continue
        value = BASE + rva
        sites = immediate_sites(image, machine, value)
        print(f"\n### {text!r} rva {rva:#x} value {value:#x}: {len(sites)} site(s)")
        for site in sites[:4]:
            start = function_start(image, site)
            print(f"  site {BASE + site:#x}  function starts {BASE + start:#x}")
            for instruction in machine.disasm(image[start:start + 0x120], BASE + start):
                mark = ">>" if abs(instruction.address - (BASE + site)) < 8 else "  "
                print(f"   {mark}{instruction.address:08x}: {instruction.mnemonic:<8} {instruction.op_str}")
            print()


if __name__ == "__main__":
    sys.exit(main())
