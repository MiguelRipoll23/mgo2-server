#!/usr/bin/env python3
"""Find code references to a string in MGO2.ELF.

MGO2.ELF is a PPC64 ELFv1 image, so a global is reached from r2 (the
table-of-contents pointer carried by every function descriptor) as
`lwz rX, disp(r2)`. That load yields a *pointer* to a table, not to the global
itself, and the string pool is reached one step further on: the pointed-at
table holds **32-bit** string addresses, and the code indexes it with a signed
displacement off the register the first load filled.

    lwz   r30, -0x6530(r2)      # r30 = 0x11eecd0, a pointer table
    ...
    lwz   r9,  -0x7ff8(r30)     # r9  = *(u32*)(0x11ecd8) = 0xff8fc8 = the string

so a reference to a string is three dereferences, and only the last one is the
address being looked for. This tool performs all three:

    python tools/mgo2_strrefs.py 0xfc2b98 --elf MGO2.ELF      # RPDT by address
    python tools/mgo2_strrefs.py RPDT --elf MGO2.ELF           # by text
    python tools/mgo2_strrefs.py RPDT --text 0x300            # dump the referencing code
"""
import argparse
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

from mgo2_disasm import ElfImage

DEFAULT_ELF = "MGO2.ELF"

# Instruction opcodes, from the PowerPC encoding. `lbz` shares opcode 34 with
# `lwz` and is told apart by the extended-opcode field; this scanner does not
# need to, because a byte read of a pointer table would not be a pointer read
# at all, so treating it as a 32-bit read is harmless.
OP_ADDI = 14
OP_ADDIS = 15
OP_LOAD_BYTE = 34
OP_LOAD_HALF = 40
OP_LOAD_WORD = 32
OP_LOAD_DOUBLE = 58
OP_ORI = 24

# Used only when the descriptor table cannot be read.
TOC_FALLBACK = 0x01222A00


def signed16(value: int) -> int:
    """Interpret a 16-bit instruction immediate as signed."""
    return value - 0x10000 if value & 0x8000 else value


def find_toc(image: ElfImage) -> int:
    """Return the r2 value, read out of the function descriptor table.

    A descriptor is `[entry u32][toc u32][env u32]` and every descriptor in the
    image carries the same TOC. Requiring the value to recur across many
    entries keeps a stray word of code that happens to look like a pointer from
    being taken for the table.
    """
    text_ranges = [(va, va + size) for va, _off, size in image.segments]
    candidates: dict[int, int] = {}
    for _virtual, offset, size in image.segments:
        block = image.data[offset:offset + size]
        for index in range(0, len(block) - 24, 4):
            entry = int.from_bytes(block[index:index + 4], "big")
            if not any(low <= entry < high for low, high in text_ranges):
                continue
            toc = int.from_bytes(block[index + 4:index + 8], "big")
            if 0x10000 <= toc < 0x2000000:
                candidates[toc] = candidates.get(toc, 0) + 1
    if not candidates:
        return TOC_FALLBACK
    return max(candidates, key=lambda key: candidates[key])


def executable_ranges(image: ElfImage) -> list[tuple[int, int]]:
    """Return the (start, end) of every executable section, from the section table.

    The image's section names are stripped, so the ranges are picked by the
    SHF_EXECINSTR flag. Scanning only these matters: the loadable segments also
    carry the ELF header, the string pool and the pointer tables, and a
    linear disassembly across those stops at the first word that is not an
    instruction — which is the very first word of the file.
    """
    header = image.data
    section_offset = struct.unpack(">Q", header[0x28:0x30])[0]
    section_entry_size = struct.unpack(">H", header[0x3A:0x3C])[0]
    section_count = struct.unpack(">H", header[0x3C:0x3E])[0]

    ranges: list[tuple[int, int]] = []
    for index in range(section_count):
        offset = section_offset + (index * section_entry_size)
        fields = struct.unpack(">IIQQQQIIQQ", header[offset:offset + 64])
        _name, section_type, flags, address, _file_offset, size = fields[:6]
        if section_type != 1 or not flags & 0x4:  # SHT_PROGBITS, SHF_EXECINSTR
            continue
        if size:
            ranges.append((address, address + size))
    return ranges


def resolve_string_reference(image: ElfImage, toc: int, table: int, displacement: int) -> int | None:
    """Follow the third dereference: the 32-bit string address in a table."""
    offset = image.to_offset((table + displacement) & 0xFFFFFFFF)
    if offset is None or offset + 4 > len(image.data):
        return None
    return int.from_bytes(image.data[offset:offset + 4], "big")


class StringReferenceScanner:
    """Walks the text tracking r2-relative loads to their final string address."""

    def __init__(self, image: ElfImage, toc: int) -> None:
        self.image = image
        self.toc = toc
        # register -> table base, for loads off r2 that resolved to a table
        self.tables: dict[int, int] = {}

    def _read_u32(self, address: int) -> int | None:
        offset = self.image.to_offset(address)
        if offset is None or offset + 4 > len(self.image.data):
            return None
        return int.from_bytes(self.image.data[offset:offset + 4], "big")

    def scan(self, start: int, end: int) -> dict[int, int]:
        """Return {instruction address: string address} over [start, end)."""
        code = self.image.read(start, end - start)
        self.tables = {}
        found: dict[int, int] = {}
        disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
        for instruction in disassembler.disasm(code, start):
            self._step(instruction.address, found)
        return found

    def scan_text(self, start: int, end: int) -> dict[int, int]:
        """Scan a whole code range, restarting the register map at each function.

        A table base lives in a callee-saved register, so it is only meaningful
        between a function's prologue and its return. Carrying the map across
        function boundaries lets one function's register leak into the next and
        invents references that were never in the source, so the map is cleared
        at every return.

        Instructions are decoded one word at a time rather than handed to a
        single `disasm` call, because a linear decode stops for good at the
        first word it cannot decode — and an executable section still holds
        jump tables and literal pools. Stepping word by word walks straight
        over them.
        """
        self.tables = {}
        found: dict[int, int] = {}
        disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
        for address in range(start, end, 4):
            decoded = next(disassembler.disasm(self.image.read(address, 4), address), None)
            if decoded is None:
                continue
            if decoded.mnemonic == "blr":
                self.tables = {}
                continue
            self._step(address, found)
        return found

    def _step(self, address: int, found: dict[int, int]) -> None:
        offset = self.image.to_offset(address)
        if offset is None:
            return
        word = int.from_bytes(self.image.data[offset:offset + 4], "big")
        opcode = word >> 26
        # D-form layout: opcode[31:26] rt[25:21] ra[20:16] displacement[15:0].
        destination = (word >> 21) & 0x1F
        source = (word >> 16) & 0x1F
        displacement = signed16(word & 0xFFFF)

        if opcode not in (OP_LOAD_WORD, OP_LOAD_BYTE, OP_LOAD_HALF, OP_LOAD_DOUBLE):
            return

        if source == 2:
            # The first dereference: a table pointer stored in the data segment.
            table = self._read_u32((self.toc + displacement) & 0xFFFFFFFF)
            if table is not None:
                self.tables[destination] = table
            else:
                self.tables.pop(destination, None)
            return

        table = self.tables.get(source)
        if table is None:
            return

        # The second dereference reads the string address out of the table. The
        # register keeps the table: a function indexes one table many times,
        # so consuming it here would lose every reference after the first.
        target = resolve_string_reference(
            self.image, self.toc, table, displacement * (2 if opcode == OP_LOAD_HALF else 1))
        if target is not None:
            found[address] = target


def disassemble_window(image: ElfImage, address: int, size: int) -> str:
    lines = []
    disassembler = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    for instruction in disassembler.disasm(image.read(address, size), address):
        lines.append(f"{instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
    return "\n".join(lines)


def locate(image: ElfImage, needle: str) -> int:
    """Virtual address of a NUL-terminated string given its text."""
    offset = image.data.find(needle.encode())
    if offset < 0:
        raise SystemExit(f"{needle!r} is not in the image")
    for virtual, segment_offset, size in image.segments:
        if segment_offset <= offset < segment_offset + size:
            return virtual + (offset - segment_offset)
    raise SystemExit(f"{needle!r} is not inside a loaded segment")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("target", help="string text, or its virtual address in hex")
    parser.add_argument("--elf", default=DEFAULT_ELF)
    parser.add_argument("--limit", type=int, default=20)
    parser.add_argument("--text", type=lambda value: int(value, 0), default=0,
                        help="bytes of context to dump around each reference")
    arguments = parser.parse_args()

    image = ElfImage(arguments.elf)
    toc = find_toc(image)
    address = (int(arguments.target, 16) if arguments.target.lower().startswith("0x")
               else locate(image, arguments.target))
    print(f"# toc (r2) = {toc:#x}", file=sys.stderr)
    print(f"# target {arguments.target} at {address:#x}", file=sys.stderr)

    scanner = StringReferenceScanner(image, toc)
    references: dict[int, int] = {}
    for start, end in executable_ranges(image):
        references.update(scanner.scan_text(start, end))

    exact = sorted(hit for hit, value in references.items() if value == address)
    print(f"{len(exact)} reference(s) to {address:#x}")
    for hit in exact[:arguments.limit]:
        print(f"  {hit:#x}")
        if arguments.text:
            print(disassemble_window(image, hit - arguments.text, arguments.text * 2))

    if not exact:
        near = sorted((abs(value - address), hit, value)
                      for hit, value in references.items()
                      if value != address and abs(value - address) < 0x1000)
        print(f"no exact reference; {len(near)} near miss(es)")
        for delta, hit, value in near[:arguments.limit]:
            print(f"  {hit:#x} -> {value:#x} (delta {value - address:+#x})")


if __name__ == "__main__":
    main()
