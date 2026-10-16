#!/usr/bin/env python3
"""Which function reads a given static: a register-tracking scan of MGO2.ELF.

A PPC64 function reaches a static in three steps and never names its address:
`lwz rN, disp(r2)` loads a TOC slot that holds a base, and the field is then
`offset(rN)` — or the address is built from that register with `addi`. So the
reader of a field can only be found by carrying what each register holds, and a
plain scan for the address (or for a displacement) drowns in coincidences across
18 MB of code. This walks the real function boundaries instead — every one is
named by an OPD entry, so the entry points are exact — tracks registers loaded
from the TOC against the values the image stores in those slots, and reports
every access whose resolved address lands in the range asked for.

That is how the module's stream classes were tied to their constructors: the
four vtables are read out of the module's static block, not materialised, so
`--low 0x011a632c --high 0x011a64c0` is the only static route to them
(`docs/analysis/udp-record-dispatch.md` §3.4).

Usage: python tools/mgo2_static_use.py <low-hex> <high-hex> [--elf PATH]
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

DEFAULT_ELF = "MGO2.ELF"
TOC = 0x01222A00
OPD_LOW, OPD_HIGH = 0x011E7358, 0x01222A00
CODE_END = 0x0118E348

LOADS = {"lwz", "lwzu", "lbz", "lbzu", "lhz", "lha", "lfs", "lfd", "ld", "ldu"}
COMPUTES = {"addi", "addic", "addis", "ori", "oris", "xori", "xoris", "li", "lis", "subi"}
ACCESSES = LOADS | {"stw", "stwu", "stb", "stbu", "sth", "stfs", "stfd", "std", "stdu",
                    "lwzx", "lbzx", "lhzx", "stwx", "stbx", "sthx", "ldx", "stdx"}


class ElfImage:
    """The loaded segments of MGO2.ELF, by address."""

    def __init__(self, path):
        with open(path, "rb") as handle:
            self.data = handle.read()
        if self.data[:4] != b"\x7fELF":
            raise ValueError("%s is not an ELF file" % path)
        program_header_offset = struct.unpack(">Q", self.data[0x20:0x28])[0]
        program_header_size = struct.unpack(">H", self.data[0x36:0x38])[0]
        program_header_count = struct.unpack(">H", self.data[0x38:0x3A])[0]
        self.segments = []
        for index in range(program_header_count):
            offset = program_header_offset + (index * program_header_size)
            kind, _flags = struct.unpack(">II", self.data[offset:offset + 8])
            file_offset, virtual, _phys, file_size = struct.unpack(
                ">QQQQ", self.data[offset + 8:offset + 0x28])
            if kind == 1 and file_size:
                self.segments.append((virtual, file_offset, file_size))

    def offset_of(self, address):
        for virtual, file_offset, file_size in self.segments:
            if virtual <= address < virtual + file_size:
                return file_offset + (address - virtual)
        return None

    def word(self, address):
        offset = self.offset_of(address)
        if offset is None:
            return None
        return struct.unpack(">I", self.data[offset:offset + 4])[0]


def register_of(token):
    token = token.strip()
    if not token.startswith("r") or token == "r2":
        return None
    try:
        return int(token[1:], 0)
    except ValueError:
        return None


def function_entries(image):
    """Every function the image names, through the OPD entries in its data."""
    entries = set()
    for address in range(OPD_LOW, OPD_HIGH, 8):
        code = image.word(address)
        if code is not None and 0x10000 <= code < CODE_END:
            entries.add(code)
    return sorted(entries)


def main():
    arguments = sys.argv[1:]
    low = int(arguments[0], 16)
    high = int(arguments[1], 16)
    path = arguments[arguments.index("--elf") + 1] if "--elf" in arguments else DEFAULT_ELF
    image = ElfImage(path)
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    entries = function_entries(image)
    print("%d function entries" % len(entries))
    seen = {}
    for index, start in enumerate(entries):
        offset = image.offset_of(start)
        if offset is None:
            continue
        stop = entries[index + 1] if index + 1 < len(entries) else CODE_END
        span = min(stop - start, 0x4000)
        known = {}
        for instruction in disassembler.disasm(image.data[offset:offset + span], start):
            mnemonic = instruction.mnemonic
            operands = [piece.strip() for piece in instruction.op_str.split(",")]
            if not operands:
                continue
            destination = register_of(operands[0]) if mnemonic in (LOADS | COMPUTES | ACCESSES) else None

            if mnemonic in ACCESSES and len(operands) >= 2 and "(" in operands[-1]:
                immediate, rest = operands[-1].split("(")
                base = register_of(rest.rstrip(")"))
                try:
                    displacement = int(immediate.strip(), 0)
                except ValueError:
                    displacement = None
                if displacement is not None and base in known:
                    field = known[base] + displacement
                    if low <= field < high:
                        seen.setdefault(field, []).append(
                            (instruction.address, mnemonic, instruction.op_str, start))

            if mnemonic in COMPUTES and len(operands) >= 3 and destination is not None:
                source = register_of(operands[1])
                try:
                    immediate = int(operands[2], 0)
                except ValueError:
                    immediate = None
                if immediate is not None and source in known:
                    known[destination] = known[source] + immediate
                elif destination in known:
                    del known[destination]
                continue

            if mnemonic in LOADS and len(operands) == 2 and destination is not None:
                source = operands[1]
                if destination in known:
                    del known[destination]
                if source.endswith("(r2)"):
                    try:
                        displacement = int(source.split("(")[0].strip(), 0)
                    except ValueError:
                        displacement = None
                    if displacement is not None and OPD_LOW <= TOC + displacement < TOC + 0x1000:
                        base = image.word(TOC + displacement)
                        if base is not None:
                            known[destination] = base
                continue

            if destination is not None and destination in known:
                del known[destination]

    for field in sorted(seen):
        records = seen[field]
        functions = sorted({record[3] for record in records})
        print("0x%08x  %d access(es) in %d function(s): %s"
              % (field, len(records), len(functions),
                 " ".join("0x%08x" % function for function in functions[:8])))
        for address, mnemonic, operand, function in records[:8]:
            print("      %08x  %s %s  in 0x%08x" % (address, mnemonic, operand, function))


if __name__ == "__main__":
    main()
