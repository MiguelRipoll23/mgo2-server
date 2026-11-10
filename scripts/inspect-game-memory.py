#!/usr/bin/env python3
"""Read a running game process's memory on macOS and scan it for known values.

The client is a Windows build run under Wine, so there are no symbols to lean on:
the only way to see what it holds is to read its pages and look for the strings
and the wire values this repository already knows.

Usage:
    python3 scripts/inspect-game-memory.py                     # find the game's pid
    python3 scripts/inspect-game-memory.py --pid 12345
    python3 scripts/inspect-game-memory.py --keyword 0x1ade --keyword "Blood Bath"
    python3 scripts/inspect-game-memory.py --regions           # region map only
    python3 scripts/inspect-game-memory.py --dump-strings      # printable strings
    python3 scripts/inspect-game-memory.py --self-test         # no process needed

`task_for_pid` needs the caller to be allowed to inspect the target: if the run
reports `KERN_FAILURE`, run it again with `sudo`, or grant the terminal
Developer Tools permission in System Settings > Privacy & Security.
"""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter

from process_memory_utils import (
    DEFAULT_HEX_VALUES,
    DEFAULT_KEYWORDS,
    build_needles,
    context,
    find_process_id,
    open_task,
    parse_anchors,
    printable_strings,
    read_memory,
    regions,
    scan_chunk,
)


def self_test() -> int:
    """Scans a synthetic buffer, so a broken run is not one sudo prompt away."""
    prefix = b"....WAIT...."
    block = prefix + rb"?.\object\itembox_mng.cc" + b"\xde\x1a"
    address = 0x1_000_000
    grep_hits: dict[str, list[tuple[int, str]]] = {"WAIT": []}
    bases: Counter[int] = Counter()

    needle_hits = scan_chunk(
        block,
        address,
        build_needles(["WAIT"], [0x1ADE]),
        ["WAIT"],
        [re.compile(b"WAIT")],
        grep_hits,
        [(rb"?.\object\itembox_mng.cc", 0xFD14F3)],
        bases,
        4,
    )

    expected = address + len(prefix) - 0xFD14F3
    names = [name for name, _ in needle_hits]
    print(f"needle hits: {names}")
    print(f"grep hits: {grep_hits}")
    print(f"anchor base: {bases.most_common(1)[0][0]:#x} (want {expected:#x})")
    ok = (
        "u16 0x1ade" in names
        and bool(grep_hits["WAIT"])
        and bases.most_common(1)[0][0] == expected
    )
    print("self-test:", "ok" if ok else "FAILED")
    return 0 if ok else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, help="pid to inspect; found by name otherwise")
    parser.add_argument("--name", default="mgo2", help="process name pattern to find")
    parser.add_argument("--keyword", action="append", default=[], help="text to find")
    parser.add_argument("--value", action="append", default=[], help="number to find, e.g. 0x1ade")
    parser.add_argument("--regions", action="store_true", help="print the region map only")
    parser.add_argument("--dump-strings", action="store_true", help="print printable strings")
    parser.add_argument("--hits", type=int, default=6, help="hits to show per needle")
    parser.add_argument(
        "--grep",
        action="append",
        default=[],
        help="print printable strings matching this regex, e.g. 'WAIT|ROUND'",
    )
    parser.add_argument(
        "--anchor",
        action="append",
        default=[],
        help="locate the module: 'text:0xrva' from docs/strings/strings.txt",
    )
    parser.add_argument(
        "--at",
        action="append",
        default=[],
        help="dump 256 bytes at module base + 0xrva, using the anchored base",
    )
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="scan a synthetic buffer, without touching a process",
    )
    parser.add_argument("--min-region", type=int, default=4096, help="skip smaller regions")
    parser.add_argument("--max-mb", type=int, default=4096, help="stop after this much read")
    parser.add_argument("--chunk-mb", type=int, default=32, help="bytes read per scan pass")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    pid = args.pid or find_process_id(args.name)
    if pid is None:
        print(f"no process matching {args.name!r}", file=sys.stderr)
        return 1

    keywords = args.keyword or DEFAULT_KEYWORDS
    values = [int(value, 0) for value in args.value] or DEFAULT_HEX_VALUES
    needles = build_needles(keywords, values)
    print(f"pid {pid}; looking for {len(needles)} needle(s)")

    task = open_task(pid)
    found: Counter[str] = Counter()
    examples: dict[str, list[str]] = {}
    total = 0
    seen_strings: Counter[str] = Counter()

    greps = [re.compile(grep.encode("ascii", "ignore")) for grep in args.grep]
    grep_hits: dict[str, list[tuple[int, str]]] = {grep: [] for grep in args.grep}
    anchors = parse_anchors(args.anchor)
    anchor_bases: Counter[int] = Counter()

    chunk = args.chunk_mb * 1024 * 1024
    for address, size, protection in regions(task):
        if size < args.min_region or total >= args.max_mb * 1024 * 1024:
            continue
        if args.regions:
            print(
                f"  {address:#014x}-{address + size:#014x} "
                f"{size // 1024:>8} KiB prot={protection:#x}"
            )
        # A region can be a multi-gigabyte reservation with a few pages behind it,
        # so it is read and scanned a chunk at a time rather than whole.
        for start in range(0, size, chunk):
            length = min(chunk + 16, size - start)
            block = read_memory(task, address + start, length)
            total += len(block)
            if args.dump_strings:
                for text in printable_strings(block):
                    seen_strings[text] += 1
            for name, offset in scan_chunk(
                block,
                address + start,
                needles,
                args.grep,
                greps,
                grep_hits,
                anchors,
                anchor_bases,
                args.hits * 4,
            ):
                found[name] += 1
                if len(examples.setdefault(name, [])) < args.hits:
                    examples[name].append(
                        f"  {name} at {address + start + offset:#014x}\n"
                        f"    {context(block, offset)}"
                    )
            if total >= args.max_mb * 1024 * 1024:
                break

    print(f"read {total / (1024 * 1024):.1f} MiB across the readable regions")
    for grep, hits in grep_hits.items():
        print(f"=== strings matching /{grep}/: {len(hits)} hit(s)")
        for address, text in hits[: args.hits * 4]:
            print(f"  {address:#014x}  {text[:130]}")
    if anchors:
        print("=== module base candidates (anchor text - rva):")
        for base, count in anchor_bases.most_common(5):
            print(f"  {base:#014x}  agreed by {count} anchor hit(s)")
        if anchor_bases:
            base = anchor_bases.most_common(1)[0][0]
            for rva in args.at:
                target = base + int(rva, 0)
                print(f"=== {target:#014x} (base + {rva}):")
                block = read_memory(task, target, 256)
                print("   " + " ".join(f"{byte:02x}" for byte in block))
                print("   " + "".join(chr(b) if 32 <= b < 127 else "." for b in block))
    if args.dump_strings:
        for text, count in seen_strings.most_common(40):
            print(f"  x{count}  {text[:120]}")
    for name, count in found.most_common():
        print(f"=== {name}: {count} hit(s)")
        for example in examples[name]:
            print(example)
    return 0


if __name__ == "__main__":
    sys.exit(main())
