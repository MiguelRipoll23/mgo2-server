"""Trace the port's initial-position routine and the client's respawn check.

`SV_GetInitialPosDir Failed :: teamno %d` sits inside the supervisor routine
that produces a team's initial position and direction. This finds that routine,
prints its frame, and prints every caller of it, which is where the fields are
packed into a record.
"""

from __future__ import annotations

import struct
import sys

from capstone import Cs, CS_ARCH_X86, CS_MODE_32

PATH = r"C:\Users\migue\Desktop\pc-client\system.dll"
BASE = 0x10000000
TEXT_VA = 0x1000
TEXT_SIZE = 5853054


def function_start(image: bytes, rva: int, text: bytes) -> int:
    """The nearest MSVC prologue before `rva`, else the nearest padding gap."""
    for candidate in range(rva, rva - 0x600, -1):
        offset = candidate - TEXT_VA
        if text[offset:offset + 3] in (b"\x55\x8b\xec", b"\x55\x89\xe5"):
            return candidate
    for candidate in range(rva, rva - 0x600, -1):
        offset = candidate - TEXT_VA
        if text[offset - 1] == 0xCC and text[offset] != 0xCC:
            return candidate
    return rva


def callers(text: bytes, target_rva: int) -> list[int]:
    hits = []
    for offset in range(0, len(text) - 5):
        if text[offset] != 0xE8:
            continue
        (rel,) = struct.unpack_from("<i", text, offset + 1)
        if (TEXT_VA + offset + 5 + rel) & 0xFFFFFFFF == target_rva:
            hits.append(TEXT_VA + offset)
    return hits


def show(machine: Cs, image: bytes, rva: int, length: int, mark: int | None = None) -> None:
    for instruction in machine.disasm(image[rva:rva + length], BASE + rva):
        flag = ">>" if mark is not None and abs(instruction.address - (BASE + mark)) < 6 else "  "
        print(f" {flag}{instruction.address:08x}: {instruction.mnemonic:<8} {instruction.op_str}")


def main() -> None:
    image = open(PATH, "rb").read()
    text = image[TEXT_VA:TEXT_VA + TEXT_SIZE]
    machine = Cs(CS_ARCH_X86, CS_MODE_32)

    for label, site in (("SV_GetInitialPosDir", 0x17A4DF), ("client respawn check", 0x26E95A)):
        start = function_start(image, site, text)
        print(f"\n===== {label}: site {BASE + site:#x}, function {BASE + start:#x}")
        show(machine, image, start, 0x300, site)
        found = callers(text, start)
        print(f"   {len(found)} caller(s): {[hex(BASE + c) for c in found][:6]}")
        for call in found[:3]:
            print(f"\n   --- caller around {BASE + call:#x}")
            caller_start = function_start(image, call, text)
            show(machine, image, caller_start, min(0x300, call - caller_start + 0x60), call)


if __name__ == "__main__":
    sys.exit(main())
