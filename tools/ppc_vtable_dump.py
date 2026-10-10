#!/usr/bin/env python3
"""Resolve a PS3 object's vtable in MGO2.ELF, through the OPDs its slots hold.

The client's C++ objects keep a vtable pointer in their first word, and each
vtable slot holds the address of an *official procedure descriptor*, not a code
address: the descriptor's first word is the code, the second is the TOC the
function expects in r2. Reading a slot therefore takes two loads, and a document
citing "vtable +0xc" is only checkable with both.

The TOC is read from the entry point, which is itself a descriptor, and every
`off(r2)` operand of a disassembled function is annotated with the address it
names and the word stored there — which is how the connection record's fields
(+0xa0c, +0xa14, +0xa18) were located.

Usage:
  python tools/ppc_vtable_dump.py 0x011db824
  python tools/ppc_vtable_dump.py 0x011db824 --disasm 0xc --count 16
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
from ppc_function_disasm import ElfImage

DEFAULT_ELF = "MGO2.ELF"
DEFAULT_SLOTS = 16


def toc_of(image):
    """The value the module keeps in r2, from its entry descriptor."""
    entry = struct.unpack(">Q", image.data[0x18:0x20])[0]
    code, toc = struct.unpack(">II", image.read(entry, 8))
    return entry, code, toc


def main():
    arguments = sys.argv[1:]
    if not arguments or arguments[0].startswith("-"):
        print(__doc__)
        raise SystemExit(1)
    cell = int(arguments[0], 16)
    slots = DEFAULT_SLOTS
    disassemble_slot = None
    count = 24
    elf = DEFAULT_ELF
    index = 1
    while index < len(arguments):
        token = arguments[index]
        if token == "--slots":
            slots = int(arguments[index + 1], 0)
            index += 2
        elif token == "--disasm":
            disassemble_slot = int(arguments[index + 1], 0)
            index += 2
        elif token == "--count":
            count = int(arguments[index + 1], 0)
            index += 2
        else:
            elf = token
            index += 1

    image = ElfImage(elf)
    entry, entry_code, toc = toc_of(image)
    print(f"entry {entry:#010x} -> code {entry_code:#010x}, r2 = {toc:#010x}")

    vtable = struct.unpack(">I", image.read(cell, 4))[0]
    print(f"{cell:#010x} holds {vtable:#010x} (the vtable)")

    machine = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    machine.detail = True

    for slot in range(slots):
        raw = image.read(vtable + slot * 4, 4)
        if len(raw) < 4:
            print(f"  +{slot * 4:#04x}  (past the end of the vtable)")
            break
        descriptor = struct.unpack(">I", raw)[0]
        if descriptor == 0:
            print(f"  +{slot * 4:#04x}  (null slot)")
            continue
        raw = image.read(descriptor, 4)
        if len(raw) < 4:
            print(f"  +{slot * 4:#04x}  opd {descriptor:#010x}  (the descriptor is unreadable)")
            continue
        code = struct.unpack(">I", raw)[0]
        print(f"  +{slot * 4:#04x}  opd {descriptor:#010x}  code {code:#010x}")

    if disassemble_slot is None:
        return

    raw = image.read(vtable + disassemble_slot, 8)
    if len(raw) < 8:
        print(f"\nvtable +{disassemble_slot:#x} is not a readable slot")
        return
    descriptor = struct.unpack(">I", raw[:4])[0]
    code, slot_toc = struct.unpack(">II", image.read(descriptor, 8))
    print(f"\n=== code {code:#010x} (vtable +{disassemble_slot:#x}, rtoc {slot_toc:#010x}) ===")
    for instruction in machine.disasm(image.read(code, count * 4), code):
        note = ""
        for operand in instruction.operands:
            base = instruction.reg_name(operand.mem.base) if operand.mem.base else ""
            if base == "r2":
                target = (toc + operand.mem.disp) & 0xFFFFFFFF
                word = struct.unpack(">I", image.read(target, 4))[0]
                note = f"   ; r2{operand.mem.disp:+#x} -> {target:#010x} = {word:#010x}"
        print(f"{instruction.address:08x}  {instruction.mnemonic:<10} {instruction.op_str}{note}")
        if instruction.mnemonic in ("blr", "bctr"):
            break


if __name__ == "__main__":
    main()
