#!/usr/bin/env python3
"""Extract printable strings from ELF and PE binaries with their memory addresses.

The binaries this project reads are the PS3 client (ELF64, big-endian PowerPC64 —
`readelf` reports it so) and the PC port's PE32 images, whose documented image base is
0x10000000. A string found at
a file offset means nothing on its own; the address a disassembly shows does, so
every line carries the virtual address the bytes are mapped at, derived from the
file's own headers rather than assumed.

Usage:
    python3 tools/extract_strings.py --min 6 --strings out/strings.txt \
        --codepaths out/codepaths.txt a.elf b.dll

Output lines are tab-separated: virtual address, file offset, binary, string.
"""

import argparse
import os
import re
import struct
import sys


def elf_segments(data: bytes):
    """Return [(file_offset, vaddr, file_size)] from an ELF program header table."""
    if data[:4] != b"\x7fELF":
        return None
    is_64 = data[4] == 2
    endian = "<" if data[5] == 1 else ">"
    if is_64:
        e_phoff = struct.unpack_from(endian + "Q", data, 0x20)[0]
        e_phentsize = struct.unpack_from(endian + "H", data, 0x36)[0]
        e_phnum = struct.unpack_from(endian + "H", data, 0x38)[0]
    else:
        e_phoff = struct.unpack_from(endian + "I", data, 0x1C)[0]
        e_phentsize = struct.unpack_from(endian + "H", data, 0x2A)[0]
        e_phnum = struct.unpack_from(endian + "H", data, 0x2C)[0]
    segments = []
    for index in range(e_phnum):
        base = e_phoff + index * e_phentsize
        if is_64:
            p_type = struct.unpack_from(endian + "I", data, base)[0]
            p_offset = struct.unpack_from(endian + "Q", data, base + 8)[0]
            p_vaddr = struct.unpack_from(endian + "Q", data, base + 16)[0]
            p_filesz = struct.unpack_from(endian + "Q", data, base + 32)[0]
        else:
            p_type = struct.unpack_from(endian + "I", data, base)[0]
            p_offset = struct.unpack_from(endian + "I", data, base + 4)[0]
            p_vaddr = struct.unpack_from(endian + "I", data, base + 8)[0]
            p_filesz = struct.unpack_from(endian + "I", data, base + 16)[0]
        if p_type == 1:  # PT_LOAD
            segments.append((p_offset, p_vaddr, p_filesz))
    return segments


def pe_segments(data: bytes):
    """Return [(file_offset, vaddr, file_size)] from a PE32 section table."""
    if data[:2] != b"MZ":
        return None
    e_lfanew = struct.unpack_from("<I", data, 0x3C)[0]
    if data[e_lfanew:e_lfanew + 4] != b"PE\0\0":
        return None
    coff = e_lfanew + 4
    section_count = struct.unpack_from("<H", data, coff + 2)[0]
    optional_size = struct.unpack_from("<H", data, coff + 16)[0]
    optional = coff + 20
    magic = struct.unpack_from("<H", data, optional)[0]
    image_base = struct.unpack_from("<I", data, optional + 28)[0] if magic == 0x10B else 0
    table = optional + optional_size
    segments = []
    for index in range(section_count):
        base = table + index * 40
        virtual_size, virtual_address = struct.unpack_from("<II", data, base + 8)
        raw_size, raw_pointer = struct.unpack_from("<II", data, base + 16)
        size = raw_size or virtual_size
        if raw_pointer:
            segments.append((raw_pointer, image_base + virtual_address, size))
    return segments


def segments_of(data: bytes):
    return elf_segments(data) or pe_segments(data) or []


def address_of(segments, offset: int):
    for file_offset, vaddr, size in segments:
        if file_offset <= offset < file_offset + size:
            return vaddr + (offset - file_offset)
    return None


def is_printable(byte: int) -> bool:
    return 0x20 <= byte < 0x7F or byte == 0x09


def looks_like_source_path(text: str) -> bool:
    """Whether a string is a compiler-emitted source path: one that ends in .cc."""
    return text.lower().endswith(".cc")


def extract(path: str, minimum: int):
    with open(path, "rb") as handle:
        data = handle.read()
    segments = segments_of(data)
    if not segments:
        return None
    start = None
    for index in range(len(data) + 1):
        byte = data[index] if index < len(data) else 0
        if index < len(data) and is_printable(byte):
            if start is None:
                start = index
            continue
        if start is not None:
            if index - start >= minimum:
                text = data[start:index].decode("ascii")
                if text.strip():
                    yield address_of(segments, start), start, text.replace("\t", "\\t")
            start = None


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binaries", nargs="+")
    parser.add_argument("--min", type=int, default=6)
    parser.add_argument("--strings", required=True)
    parser.add_argument("--codepaths", required=True)
    options = parser.parse_args(argv)

    total = 0
    paths = 0
    with open(options.strings, "w", encoding="utf-8") as strings_out, \
            open(options.codepaths, "w", encoding="utf-8") as paths_out:
        for path in options.binaries:
            name = path
            extracted = extract(path, options.min)
            if extracted is None:
                print(f"{path}: no ELF or PE headers, skipped", file=sys.stderr)
                continue
            count = 0
            for address, offset, text in extracted:
                total += 1
                count += 1
                address_text = f"0x{address:08X}" if address is not None else "unmapped"
                strings_out.write(f"{address_text}\t0x{offset:08X}\t{name}\t{text}\n")
                if looks_like_source_path(text):
                    paths += 1
                    paths_out.write(f"{address_text}\t0x{offset:08X}\t{name}\t{text}\n")
            print(f"{path}: {count} strings")
    print(f"total: {total} strings, {paths} source paths")


if __name__ == "__main__":
    main(sys.argv[1:])
