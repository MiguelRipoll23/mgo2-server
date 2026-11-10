#!/usr/bin/env python3
"""The Mach side of reading a game process: its task, regions, pages, scanners.

`inspect-game-memory.py` is the command; everything it does to the process lives
here. The split keeps both files inside the repository's 400-line limit and keeps
the parts a second tool would want — the task, the region walk, the readers and
the scanners — apart from the command's argument handling and its report.
"""

from __future__ import annotations

import ctypes
import ctypes.util
import re
import struct
import subprocess
import sys
from collections import Counter

KERN_SUCCESS = 0
VM_REGION_BASIC_INFO_64 = 9
VM_PROT_READ = 0x1
PAGE = 4096

# The values this repository's own traffic uses, so a hit can be tied to a
# record the host wrote: the identifier sets of the last run and the clock it
# carried, the briefing's, and the room's own identity.
DEFAULT_KEYWORDS = [
    "WAIT",
    "GO",
    "Blood Bath",
    "BLOOD BATH",
    "CAP",
    "Deathmatch",
    "server-9999",
    "mgo2",
]
DEFAULT_HEX_VALUES = [
    0x1ADE,  # the round's payload identifier
    0x1A54,  # the round's operations identifier
    0x1A50,  # the round's item-box identifier
    0x1254,  # the briefing's operations identifier
    0x12DE,  # the briefing's payload identifier
    0x084E,  # the vitals record this peer's slot makes the beat write
    0x0954,  # the clock the round's run carried, 2388 units
    0x0960,  # a whole round, 2400 units
]

libc = ctypes.CDLL(ctypes.util.find_library("System") or "/usr/lib/libSystem.B.dylib")

mach_port_t = ctypes.c_uint32
mach_vm_address_t = ctypes.c_uint64
mach_vm_size_t = ctypes.c_uint64


class VmRegionBasicInfo64(ctypes.Structure):
    """`vm_region_basic_info_64` — the region's protection and sharing."""

    _fields_ = [
        ("protection", ctypes.c_int),
        ("max_protection", ctypes.c_int),
        ("inheritance", ctypes.c_uint),
        ("shared", ctypes.c_int),
        ("reserved", ctypes.c_int),
        ("offset", ctypes.c_uint64),
        ("behavior", ctypes.c_int),
        ("user_wired_count", ctypes.c_ushort),
    ]


libc.mach_task_self.restype = mach_port_t
libc.task_for_pid.argtypes = [mach_port_t, ctypes.c_int, ctypes.POINTER(mach_port_t)]
libc.task_for_pid.restype = ctypes.c_int
libc.mach_vm_region.argtypes = [
    mach_port_t,
    ctypes.POINTER(mach_vm_address_t),
    ctypes.POINTER(mach_vm_size_t),
    ctypes.c_int,
    ctypes.c_void_p,
    ctypes.POINTER(ctypes.c_uint),
    ctypes.POINTER(mach_port_t),
]
libc.mach_vm_region.restype = ctypes.c_int
libc.mach_vm_read_overwrite.argtypes = [
    mach_port_t,
    mach_vm_address_t,
    mach_vm_size_t,
    mach_vm_address_t,
    ctypes.POINTER(mach_vm_size_t),
]
libc.mach_vm_read_overwrite.restype = ctypes.c_int


def find_process_id(pattern: str) -> int | None:
    """The pid of the first process whose command line matches `pattern`."""
    try:
        out = subprocess.run(
            ["pgrep", "-f", pattern], capture_output=True, text=True, check=False
        ).stdout
    except OSError:
        return None
    for line in out.split():
        if line.isdigit():
            return int(line)
    return None


def open_task(pid: int) -> mach_port_t:
    """The task port of `pid`, or an exit with what the failure means."""
    task = mach_port_t(0)
    result = libc.task_for_pid(libc.mach_task_self(), pid, ctypes.byref(task))
    if result != KERN_SUCCESS:
        print(
            f"task_for_pid({pid}) failed with kern_return_t {result}: this process may not "
            "inspect that one. Run this again with sudo, or grant the terminal Developer "
            "Tools permission in System Settings > Privacy & Security.",
            file=sys.stderr,
        )
        sys.exit(2)
    return task


def regions(task: mach_port_t, readable_only: bool = True):
    """Every region of the task, as (address, size, protection)."""
    address = mach_vm_address_t(0)
    while True:
        size = mach_vm_size_t(0)
        info = VmRegionBasicInfo64()
        count = ctypes.c_uint(
            ctypes.sizeof(VmRegionBasicInfo64) // ctypes.sizeof(ctypes.c_int)
        )
        name = mach_port_t(0)
        result = libc.mach_vm_region(
            task,
            ctypes.byref(address),
            ctypes.byref(size),
            VM_REGION_BASIC_INFO_64,
            ctypes.byref(info),
            ctypes.byref(count),
            ctypes.byref(name),
        )
        if result != KERN_SUCCESS or size.value == 0:
            return
        if not readable_only or info.protection & VM_PROT_READ:
            yield address.value, size.value, info.protection
        address = mach_vm_address_t(address.value + size.value)


def read_memory(task: mach_port_t, address: int, size: int) -> bytes:
    """`size` bytes at `address`, page by page, with unreadable pages as zeros."""
    buffer = ctypes.create_string_buffer(size)
    got = mach_vm_size_t(0)
    result = libc.mach_vm_read_overwrite(
        task, address, size, ctypes.addressof(buffer), ctypes.byref(got)
    )
    if result == KERN_SUCCESS and got.value == size:
        return buffer.raw
    if result != KERN_SUCCESS:
        # A region can report read and still refuse a read of its whole length;
        # fall back to one page at a time so a single bad page is not the loss.
        out = bytearray(size)
        for offset in range(0, size, PAGE):
            chunk = min(PAGE, size - offset)
            part = ctypes.create_string_buffer(chunk)
            part_got = mach_vm_size_t(0)
            part_result = libc.mach_vm_read_overwrite(
                task,
                address + offset,
                chunk,
                ctypes.addressof(part),
                ctypes.byref(part_got),
            )
            if part_result == KERN_SUCCESS:
                out[offset : offset + part_got.value] = part.raw[: part_got.value]
        return bytes(out)
    return buffer.raw[: got.value]


def context(block: bytes, offset: int, width: int = 48) -> str:
    """Printable text around `offset`, with the surrounding bytes as hex."""
    start = max(0, offset - width)
    end = min(len(block), offset + width)
    window = block[start:end]
    text = "".join(chr(b) if 32 <= b < 127 else "." for b in window)
    return f"{text}\n{' ' * (offset - start)}^"


def scan(block: bytes, needles: list[tuple[str, bytes]]) -> list[tuple[str, int]]:
    """Every hit of each needle in `block`, as (name, offset)."""
    hits = []
    for name, needle in needles:
        start = 0
        while True:
            found = block.find(needle, start)
            if found < 0:
                break
            hits.append((name, found))
            start = found + 1
    return hits


def grep_strings(
    block: bytes, address: int, pattern: re.Pattern[bytes], limit: int
) -> list[tuple[int, str]]:
    """Printable runs in `block` matching `pattern`, as (address, text)."""
    out: list[tuple[int, str]] = []
    for match in re.finditer(rb"[\x20-\x7e]{5,}", block):
        text = match.group()
        if pattern.search(text):
            out.append((address + match.start(), text.decode("ascii", "ignore")))
            if len(out) >= limit:
                break
    return out


def scan_chunk(
    block: bytes,
    address: int,
    needles: list[tuple[str, bytes]],
    grep_arguments: list[str],
    greps: list[re.Pattern[bytes]],
    grep_hits: dict[str, list[tuple[int, str]]],
    anchors: list[tuple[bytes, int]],
    anchor_bases: Counter[int],
    limit: int,
) -> list[tuple[str, int]]:
    """The chunk's needle hits, after adding its string and anchor hits.

    `address` is where `block` starts, so a hit is reported at its absolute
    address and an anchor's candidate base is the text's address less its rva.
    """
    for argument, pattern in zip(grep_arguments, greps, strict=True):
        hits = grep_hits[argument]
        if len(hits) < limit:
            hits.extend(grep_strings(block, address, pattern, limit - len(hits)))

    for text, rva in anchors:
        position = block.find(text)
        while position >= 0 and anchor_bases[address + position - rva] < 4:
            anchor_bases[address + position - rva] += 1
            position = block.find(text, position + 1)

    return scan(block, needles)


def build_needles(keywords: list[str], values: list[int]) -> list[tuple[str, bytes]]:
    """The byte needles to look for: text in both encodings, numbers little-endian."""
    needles: list[tuple[str, bytes]] = []
    for keyword in keywords:
        needles.append((f'"{keyword}"', keyword.encode("ascii", "ignore")))
        needles.append((f'"{keyword}" utf-16', keyword.encode("utf-16-le")))
    for value in values:
        needles.append((f"u16 0x{value:04x}", struct.pack("<H", value)))
        needles.append((f"u32 0x{value:08x}", struct.pack("<I", value)))
    return [needle for needle in needles if needle[1]]


def printable_strings(block: bytes, minimum: int = 8) -> list[str]:
    """Runs of printable characters of at least `minimum` bytes."""
    return [
        match.decode("ascii", "ignore")
        for match in re.findall(rb"[\x20-\x7e]{%d,}" % minimum, block)
    ]


def parse_anchors(anchors: list[str]) -> list[tuple[bytes, int]]:
    """`--anchor` arguments, `text:0xrva`, as (text bytes, rva) pairs."""
    parsed = []
    for anchor in anchors:
        text, _, rva = anchor.rpartition(":")
        if not text or not rva:
            raise SystemExit(f"--anchor wants text:0xrva, not {anchor!r}")
        parsed.append((text.encode("ascii", "ignore"), int(rva, 0)))
    return parsed
