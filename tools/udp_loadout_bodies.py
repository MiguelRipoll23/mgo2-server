#!/usr/bin/env python3
"""Check what the gameplay server writes into a loadout record against the capture.

Usage:
    python3 tools/udp_loadout_bodies.py CAPTURE.pcapng [CAPTURE.pcapng ...]

The settings burst answers a joiner with one `0b <slot+1>` loadout record per
player of the room, and the record's slot byte, side, appearance block and skill
pairs are written from that player's own rows now
(`LoadoutFramesUtils.TryWrite`, `UDP_SETTINGS_PAYLOAD.md`). Everything else in the
body is left as a recorded template of the same shape has it, because the bytes
that remain are per-player values nothing names.

This walks every loadout body a capture's host sent, decodes it with
`udp_settings_semantics` — the port of the replay console's own `appearanceRecord`
and `skillRecord` — re-encodes it with a port of the C# writer, using the recorded
templates read straight out of `LoadoutBodiesUtils.cs`, and reports where the two
differ. A body that differs only in the unnamed bytes is the expected result; a
difference on a named field is a bug in the writer.

The last run over the five captures in the repository root:

    20 distinct loadout bodies: 7 byte for byte, 12 differing only in the tail
    (and, for the no-pair bodies, the region byte), 1 refused by the shape rule
    (the 55-byte record of the capture where the player had chosen red, whose
    skill region does not align under the decoder's grammar).
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_settings_semantics import (  # noqa: E402
    SKILL_APPEARANCE,
    is_loadout_record,
    parse_skills,
)

# The recorded templates, read from the source rather than retyped, so the check
# covers the bytes the server actually sends.
BODIES_SOURCE = Path("src/GameplayServer/Rooms/LoadoutBodiesUtils.cs")

TEMPLATE_CONSTANTS = {
    0: "PlayerLoadoutPayloadHex",
    2: "SecondLoadoutPayloadHex",
    3: "ThirdLoadoutPayloadHex",
}

SLOT_OFFSET = 1
SIDE_OFFSET = 2
SKILL_COUNT_OFFSET = 0x16
REGION_OFFSET = 0x17
APPEARANCE = SKILL_APPEARANCE
SETTINGS_TYPE = 0x0B
SKILL_LIST_END = 0xFF
SKILL_REGION_SUFFIX = 0x10
MAXIMUM_SKILLS = 3


def templates() -> dict[int, bytes]:
    """The three recorded loadout bodies, keyed by the skill pairs they carry."""
    text = BODIES_SOURCE.read_text(encoding="utf-8")
    constants = {
        name: bytes.fromhex("".join(re.findall(r'"([0-9a-fA-F]*)"', value)))
        for name, value in re.findall(
            r"private const string (\w+)\s*=\s*((?:\"[0-9a-fA-F]*\"\s*\+?\s*)+);", text
        )
    }
    return {count: constants[name] for count, name in TEMPLATE_CONSTANTS.items()}


def skill_count_of(template: bytes) -> int:
    """Port of `LoadoutFramesUtils.SkillCountOf`: the pairs a body carries, or -1."""
    if len(template) <= REGION_OFFSET + 1 or template[0] != SETTINGS_TYPE:
        return -1

    count = template[SKILL_COUNT_OFFSET]
    end = len(template) - APPEARANCE - count - 1
    if end < REGION_OFFSET or end >= len(template) or template[end] != SKILL_LIST_END:
        return -1

    region = end - REGION_OFFSET
    if region > 0 and template[REGION_OFFSET + region - 1] == SKILL_REGION_SUFFIX:
        region -= 1
    return 0 if region < 2 else region // 2


def try_write(slot: int, team: int, appearance: bytes, skills, template: bytes):
    """Port of `LoadoutFramesUtils.TryWrite`, or None when the shape does not fit."""
    if skills is None or len(skills) != skill_count_of(template) or len(skills) > MAXIMUM_SKILLS:
        return None

    built = bytearray(template)
    built[SLOT_OFFSET] = slot + 1
    built[SIDE_OFFSET] = team
    for index, (identifier, _, level) in enumerate(skills):
        built[REGION_OFFSET + 1 + index * 2] = identifier
        built[REGION_OFFSET + 1 + index * 2 + 1] = level
    built[len(built) - APPEARANCE:] = appearance
    return bytes(built)


def captured_loadouts(capture_path: str) -> dict[bytes, list[str]]:
    """Every distinct loadout body the capture's host sent."""
    capture = Pcapng(capture_path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    if joiner is None:
        raise SystemExit(f"{capture_path}: no peer-to-peer handshake")
    host = next(peer for _, source, peer, _ in datagrams if peer != joiner and source == joiner)
    replay = Replay(datagrams, joiner, host)

    bodies: dict[bytes, list[str]] = {}
    for frame in replay.frames:
        if frame.source != host or not frame.verified:
            continue
        for _, _, _, body in frame.messages:
            body = bytes(body)
            if is_loadout_record(body):
                bodies.setdefault(body, []).append(Path(capture_path).name)
    return bodies


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("captures", nargs="+")
    arguments = parser.parse_args()

    shapes = templates()
    print("recorded templates: " + ", ".join(f"{count} pair(s) {len(body)} B" for count, body in shapes.items()))

    bodies: dict[bytes, list[str]] = {}
    for capture_path in arguments.captures:
        for body, paths in captured_loadouts(capture_path).items():
            bodies.setdefault(body, []).extend(paths)

    matched = differed = refused = 0
    for body, paths in sorted(bodies.items(), key=lambda kv: (len(kv[0]), kv[0])):
        slot = body[1] - 1
        team = body[2]
        skills = parse_skills(body)
        appearance = bytes(body[len(body) - APPEARANCE:])
        count = len(skills) if skills is not None else None
        template = shapes.get(count)
        built = try_write(slot, team, appearance, skills, template) if template else None

        if built is None:
            refused += 1
            print(f"REFUSED {paths[0]:44} {len(body):3} B slot {slot:2} team {team:02x} skills {skills}")
            continue

        if built == body:
            matched += 1
            print(f"match   {paths[0]:44} {len(body):3} B slot {slot:2} team {team:02x} pairs {count}")
            continue

        differed += 1
        count_byte = body[SKILL_COUNT_OFFSET]
        end = len(body) - APPEARANCE - count_byte - 1
        diffs = [(index, body[index], built[index]) for index in range(len(body)) if body[index] != built[index]]
        print(
            f"differs {paths[0]:44} {len(body):3} B slot {slot:2} team {team:02x} pairs {count} -> "
            + ", ".join(f"{index}: capture {actual:02x} built {wanted:02x}" for index, actual, wanted in diffs)
            + f"   (the tail is {end + 1}..{len(body) - APPEARANCE - 1})"
        )

    print(
        f"\n{len(bodies)} distinct captured loadout bodies: {matched} byte for byte, "
        f"{differed} differing, {refused} refused by the shape rule"
    )


if __name__ == "__main__":
    main()
