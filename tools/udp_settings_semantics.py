#!/usr/bin/env python3
"""The `0b` settings payload a dedicated host sends for each player.

The record a peer reaches the gameplay channel carrying equipment, skills and
settings — the family the capture tools previously read as a roster body and
mis-reported, and the one this module names.

The layout is **ported, not re-derived**, from `rpdt.js` (`?v=ins236`, the
decoder the SaveMGO replay console loads, fetched 2026-10-11). The record is
that decoder's `TYPE_SETTINGS` (`0x0b`), and every offset, length and alignment
rule below comes from its `appearanceRecord` and `skillRecord` functions rather
than from a reading of our captures. What the port buys is that a capture and
the replay console agree on what a body says instead of merely looking alike;
what the captures add is the sample values `docs/protocol/UDP_SETTINGS_PAYLOAD.md`
lists as evidence that the port is right.

Two shapes share the leading byte, told apart by the second byte and the length:

* **Settings / rotation** — second byte `0x00` and length `>= 71`. Fifteen
  three-byte entries at offset `0x1a`: `(map id, mode/rule, unresolved)`. This is
  the host's own payload, the 161-byte `0b 00`.
* **Loadout** — second byte `>= 1` and a length in :data:`LOADOUT_LENGTHS`. The
  slot is the second byte minus one, the team byte follows it, the trailing 23
  bytes are the character's appearance, and the skill pairs sit before it.

Nothing here sends anything; it decodes a capture. The server must not write
these bodies, and does not.
"""

from __future__ import annotations

# rpdt.js: the discriminator and the lengths its settings branch accepts.
TYPE_SETTINGS = 0x0B
LOADOUT_LENGTHS = (50, 51, 53, 54, 55, 56, 57, 58)

# rpdt.js: the appearance block is the trailing 23 bytes, the skill list ends on
# 0xff, and a region may carry a trailing 0x10 that is not part of a pair.
SKILL_APPEARANCE = 23
SKILL_LIST_END = 0xFF
SKILL_REGION_SUFFIX = 0x10

# rpdt.js: the rotation shape. Slot 0 with a body this long carries fifteen
# three-byte entries from offset 0x1a.
MINIMUM_ROTATION_LENGTH = 71
ROTATION_COUNT = 15
ROTATION_OFFSET = 0x1A
ROTATION_ENTRY = 3

# rpdt.js: skill ids are base 1, measured, and the names are its list. The pair
# a record carries is (id, level) with the level in 1..3.
SKILL_NAMES = (
    "HAND_MAS",
    "SBMC_MAS",
    "ASSLT_MAS",
    "SHOT_MAS",
    "SNIPPER_MAS",
    "ZOOM",
    "LOCKON",
    "THROW",
    "TRAP",
    "CQC",
    "KNIFE",
    "FASTMOV",
    "ENEMY",
    "AWARE",
    "ALERT",
    "SCANNING",
    "OFFICER",
    "RECOVER",
    "CBOX",
    "CQC_EX",
    "SHIELD_MAS",
    "SCANNING_EX",
    "SOP_JAMMING",
    "TOUGHNESS",
    "CHARM",
)

# rpdt.js APPEARANCE_FIELDS: the field each of the trailing 23 bytes names, in
# order. The item keys and `gender` (0 = male) are measured; the colour bytes'
# order is inferred by the replay decoder.
APPEARANCE_FIELDS = (
    "head",
    "chest",
    "hand",
    "waist",
    "foot",
    "acc1",
    "acc2",
    "headColor",
    "chestColor",
    "handColor",
    "waistColor",
    "footColor",
    "gender",
    "face",
    "upper",
    "lower",
    "facePaint",
    "upperColor",
    "lowerColor",
    "voice",
    "pitch",
    "acc1Color",
    "acc2Color",
)


def is_settings_payload(body: bytes) -> bool:
    """Whether a body is a `0b` settings record at all, of either shape."""
    return len(body) >= 2 and body[0] == TYPE_SETTINGS


def is_rotation_record(body: bytes) -> bool:
    """Whether a body is the host's own settings/rotation record.

    Slot byte zero with a body long enough to hold the fifteen entries. The
    slot zero is what makes it the host's: the loadout shape starts at slot one.
    """
    return (
        len(body) >= MINIMUM_ROTATION_LENGTH
        and body[0] == TYPE_SETTINGS
        and body[1] == 0x00
    )


def is_loadout_record(body: bytes) -> bool:
    """Whether a body is a player's loadout record.

    Slot byte one or above and one of the lengths the replay decoder accepts.
    The length set is the decoder's, not a guess: 52 does not parse there and is
    left alone here for the same reason.
    """
    return (
        len(body) in LOADOUT_LENGTHS
        and body[0] == TYPE_SETTINGS
        and body[1] >= 1
    )


def parse_rotation(body: bytes) -> list[tuple[int, int, int]] | None:
    """The fifteen `(map id, mode, unresolved)` entries, or `None`.

    The first two are named by the replay decoder — it picks the entry whose
    map equals the header map and reads that as the round's mode — and the third
    is carried through unnamed because nothing names it.
    """
    if not is_rotation_record(body):
        return None
    return [
        (
            body[ROTATION_OFFSET + ROTATION_ENTRY * index],
            body[ROTATION_OFFSET + ROTATION_ENTRY * index + 1],
            body[ROTATION_OFFSET + ROTATION_ENTRY * index + 2],
        )
        for index in range(ROTATION_COUNT)
    ]


def parse_appearance(body: bytes) -> dict[str, int] | None:
    """The trailing 23 bytes, named by :data:`APPEARANCE_FIELDS`, or `None`."""
    if not is_loadout_record(body):
        return None
    start = len(body) - SKILL_APPEARANCE
    return {
        field: body[start + index]
        for index, field in enumerate(APPEARANCE_FIELDS)
    }


def parse_skills(body: bytes) -> list[tuple[int, str, int]] | None:
    """The equipped skills as `(id, name, level)`, or `None` when unreadable.

    The alignment is the replay decoder's: a per-record count byte at offset
    `0x16` sizes the skill region, the region ends on `0xff`, an optional `0x10`
    suffix is dropped, and the pairs are then read from the region's end as
    three, two or one `(id, level)` pairs — the first count whose ids and levels
    are all in range wins. An empty list is a record that really carries no
    skills; `None` is a record whose region could not be aligned, which the
    decoder never collapses to "no skills".
    """
    if not is_loadout_record(body):
        return None

    count = body[22]
    end = len(body) - SKILL_APPEARANCE - count - 1
    if not 23 <= end < len(body) or body[end] != SKILL_LIST_END:
        return None

    region = end - 23
    if region > 0 and body[23 + region - 1] == SKILL_REGION_SUFFIX:
        region -= 1
    if region < 2:
        return []

    for pairs in (3, 2, 1):
        if 2 * pairs > region:
            continue
        start = 23 + region - 2 * pairs
        skills: list[tuple[int, str, int]] = []
        for index in range(pairs):
            skill_id = body[start + 2 * index]
            level = body[start + 2 * index + 1]
            if not 1 <= skill_id <= len(SKILL_NAMES) or not 1 <= level <= 3:
                break
            skills.append((skill_id, SKILL_NAMES[skill_id - 1], level))
        else:
            return skills
    return None


def parse_settings(body: bytes) -> dict | None:
    """Decode a `0b` payload into the shape it carries, or `None`.

    A rotation record answers `{"kind": "rotation", "rotation": [...]}`. A
    loadout record answers `{"kind": "loadout", "slot", "team", "appearance",
    "skills"}` — its slot already shifted down by one, so slot 0 is the same
    first joining player the roster counts from zero, and the host itself is the
    rotation record rather than a loadout. Anything else is `None`.
    """
    if is_rotation_record(body):
        return {"kind": "rotation", "rotation": parse_rotation(body)}
    if is_loadout_record(body):
        return {
            "kind": "loadout",
            "slot": body[1] - 1,
            "team": body[2],
            "appearance": parse_appearance(body),
            "skills": parse_skills(body),
        }
    return None
