#!/usr/bin/env python3
"""Test the 26-state schema against the record bodies a capture actually carries.

`FUN_0027E8B0` reads one byte (an index into the state table at
`*(0x011A646C)`), then `ceil(count/8)` bytes of a one-bit-per-field presence map,
then for every set bit the field's bytes: `0x27EC48` moves `length * multiplier`
of them, and every field ends by marking a two-bit code in the state's own map.
So a body that parse can consume has

    1 + ceil(count/8) + sum(length * multiplier over the set bits)  ==  its length

against the state the first byte names. This reads the descriptors out of
`MGO2.ELF`, decodes every record of every capture named (whole bodies, via
`udp_frame`), and reports what fits.

Two things about how it counts, because both decide whether a fit means anything:

* **distinct bodies, not records.** The captures repeat one body hundreds of
  times, so a fit found in a repeated record is one coincidence, not hundreds.
  Every figure below is a distinct-body count unless it says *records*.
* **the simplification.** A field whose flags carry `0x200` is a *container*
  whose own bytes do not move (its nested fields move instead), and this counts
  it as if they did. That is exact for a state with no container field, can only
  over-count otherwise, and cannot rescue the operations records: their bodies are
  2-3 bytes against the `1 + presence map` floor of 8. `0x00`'s 90-byte state
  record and the 21/41-byte maps are why the captures, not the listings, are the
  source — a listing prints 24 bytes of a body and so cannot answer for them.

The answer for the reference captures is **nothing**: over 406 144 bodies from
`mgo2-game-joined-when-red-team-then-briefing`, `mgo2-game1` and `mgo2-game2`
(177 179 distinct), 2 490 distinct bodies (1.41%) fit *some* index — unrelated
tick bodies whose arithmetic coincides — and **none** of the operations
identifier's 2 214 records does. Both ends of that range say why: the clock and
the kill feed are 2-3 bytes against a `1 + presence map` floor of 8, and the
state record carries 90 bytes where its own first byte read as a state index
with the next seven as a map wants 28. That is the measurement behind
`docs/analysis/udp-record-dispatch.md` §3.6:
the presence-map parse is a *state* transfer (state block to stream, 24 slot
states plus two others), not the wire path of the operations records, whose
opcodes select per-record handlers in the module's registry instead.

Usage:
    python tools/mgo2_state_wire_check.py [capture.pcapng ...] [--elf PATH]
    python tools/mgo2_state_wire_check.py [listing.txt ...]
"""
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mgo2_record_schema as schema_module  # noqa: E402

LINE = re.compile(
    r"type=(0x[0-9a-f]+)\s+len=\s*(\d+)\s+class=(0x[0-9a-f]+)\s+body=([0-9a-f ]*)")
STATES = 26
OPERATIONS = 0x254


def descriptors(image):
    """Per state index: the field count, and each field's (length, multiplier, flags)."""
    table = []
    for index in range(STATES):
        count, _field_table, _block, fields = schema_module.schema(image, index)
        table.append((count, [(length, multiplier, flags)
                              for _i, _o, length, multiplier, flags in fields]))
    return table


def needed(count, fields, presence_map):
    """Bytes the parse consumes for one presence map, index 1 being the first field."""
    total = 1 + len(presence_map)
    for index, (length, multiplier, _flags) in enumerate(fields):
        if index // 8 >= len(presence_map):
            break
        if presence_map[index // 8] >> (index % 8) & 1:
            total += length * multiplier
    return total


def fits(table, plain, body):
    """The index whose parse consumes this body exactly, or None."""
    if not body:
        return None
    for candidate in range(STATES):
        count, fields = table[candidate]
        presence_bytes = (count + 7) // 8
        if len(body) >= 1 + presence_bytes and needed(
                count, fields, body[1:1 + presence_bytes]) == len(body):
            return candidate if candidate in plain else -1
    return None


def bodies_from_capture(path):
    """Every (type, flags2, body) a capture carries, whole, in decode order."""
    from udp_frame import decode_frame, lzss_decompress
    from udp_session_records import collect, key_for, parse

    datagrams, bases, _joiners, _origins = collect(path)
    for _ts, local, remote, payload in datagrams:
        counter, plain, verified, _pre = decode_frame(payload, key_for(bases, local, remote))
        if not verified:
            continue
        content = plain[2:len(plain) - 0xA]
        if counter & 0x8000:
            content = lzss_decompress(content)
        for message_type, _length, flags2, body in parse(content):
            yield "0x%04x" % message_type, "0x%02x" % flags2, body


def bodies_from_listing(path):
    """Every (type, flags2, body) a listing carries whole: its 24-byte cut drops the rest."""
    for line in open(path, encoding="utf-8", errors="replace"):
        match = LINE.search(line)
        if not match:
            continue
        body = bytes(int(pair, 16) for pair in match.group(4).split())
        if len(body) != int(match.group(2)):
            continue
        yield match.group(1), match.group(3), body


def main():
    arguments = sys.argv[1:]
    path = arguments[arguments.index("--elf") + 1] if "--elf" in arguments else "MGO2.ELF"
    captures = [a for a in arguments if a.endswith(".pcapng")]
    listings = [a for a in arguments if a.endswith(".txt")]
    if not captures and not listings:
        captures = sorted(glob.glob("*.pcapng"))
        listings = sorted(glob.glob("tmpout/*.txt"))
    if captures:
        sources = [(name, bodies_from_capture) for name in captures]
        print("reading %d capture(s), whole bodies" % len(captures))
    else:
        sources = [(name, bodies_from_listing) for name in listings]
        print("reading %d listing(s); a body cut at 24 bytes is skipped" % len(listings))

    image = schema_module.ElfImage(path)
    table = descriptors(image)
    plain = [index for index in range(STATES)
             if not any(flags & 0x200 for _length, _multiplier, flags in table[index][1])]

    records = 0
    seen = {}
    operations = {}
    for name, reader in sources:
        for kind, flags2, body in reader(name):
            records += 1
            key = (kind, flags2, body)
            entry = seen.get(key)
            if entry is None:
                entry = seen[key] = [0, fits(table, plain, body)]
            entry[0] += 1
            if int(kind, 16) & 0xFFF == OPERATIONS:
                bucket = operations.setdefault(len(body), [0, 0, body.hex(" ")])
                bucket[0] += 1
                if entry[1] is not None:
                    bucket[1] += 1

    distinct = len(seen)
    fitting = [key for key, value in seen.items() if value[1] is not None]
    exact = [key for key in fitting if seen[key][1] >= 0]
    print("%d record bodies, %d distinct" % (records, distinct))
    print("  %d distinct bodies name a state index the table has (<= 0x%02x)"
          % (sum(1 for key in seen if key[2] and key[2][0] < STATES), STATES - 1))
    print("  %d distinct bodies are consumed exactly by some index (%.2f%%)"
          % (len(fitting), 100.0 * len(fitting) / distinct if distinct else 0.0))
    print("  %d of those against a state with no container field (the simplification exact)"
          % len(exact))
    print()
    operations_records = sum(bucket[0] for bucket in operations.values())
    operations_fit = sum(bucket[1] for bucket in operations.values())
    print("the operations identifier (`id & 0xfff == 0x%03x`): %d records, %d fitting"
          % (OPERATIONS, operations_records, operations_fit))
    print("  body lengths: %s" % dict(sorted(operations.items() and
                                             ((length, bucket[0]) for length, bucket in
                                              operations.items()))))
    print()
    print("the busiest fitting bodies (a repeat is one body, so its count is not evidence):")
    for key in sorted(fitting, key=lambda k: -seen[k][0])[:10]:
        kind, flags2, body = key
        print("  %d x type=%s flags2=%s len=%-3d index=%s  %s"
              % (seen[key][0], kind, flags2, len(body),
                 "none-container" if seen[key][1] >= 0 else "container", body.hex(" ")[:50]))


if __name__ == "__main__":
    main()
