#!/usr/bin/env python3
"""Compare two gameplay captures section by section, in wire order.

Reads the working host's capture and this server's, decodes both the same way
(`pcap_gameplay_log.Replay`), and prints one report for each: the roster it
answered with, its control and actor-record inventory, a byte-by-byte field map
per record (which offsets are constant and which move), the spawn point each
slot starts from, the item-box records, and a timeline of what happened in the
order the datagrams carried it — aiming, damage, vitals, deaths and kill feeds.
Then it diffs the two.

    python3 tools/compare_captures.py .pcapngs/actions/actions.pcapng \
                                      .pcapngs/myserver.pcapng
    python3 tools/compare_captures.py A.pcapng B.pcapng --markdown out.md --timeline 120

Every number here is read off the wire. Nothing is inferred from the C#; the
constants below are the identifier sets the replay parser and
`docs/protocol/DATA_PHASE_DELIVERY.md` name, and anything outside them is
printed as unknown rather than guessed at.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_bot_combat import slot_of  # noqa: E402
from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_record_semantics import is_roster_entry, parse_profile  # noqa: E402

# The two rounds' identifier sets (MatchRecordGenerationUtils.First and its
# 0x800 sibling), each also with the payload-class bit a live client sets.
def _shapes(values) -> set[int]:
    return {value | bit for value in values for bit in (0x0000, 0x8000)}


OPERATIONS = _shapes([0x1A54, 0x1254])
RULES_ROSTER = _shapes([0x0A61, 0x0261])
ITEM_BOX = _shapes([0x1A50, 0x1250])
ITEM_BOX_STATE = _shapes([0x1A5A, 0x125A])
JOIN_STATE = _shapes([0x0A5C, 0x025C])
JOIN_PAYLOAD = _shapes([0x1ADE, 0x12DE])
MATCH_START = _shapes([0x5ADE, 0x52DE])
ROOM_COUNT = _shapes([0x1A58, 0x1258])
BEAT_VITALS = _shapes([0x090C, 0x010C])

CONTROL_NAMES = {
    0x1000: "handshake", 0x5000: "keep-alive", 0x1001: "player-profile",
    0x9001: "join-request", 0x5001: "roster-head", 0xD001: "in-game control",
    0x0002: "game-state blob", 0x0709: "join probe",
}
FAMILIES = [
    (OPERATIONS, "operations"), (RULES_ROSTER, "rules roster"),
    (ITEM_BOX, "item box"), (ITEM_BOX_STATE, "item box state"),
    (JOIN_STATE, "join state"), (JOIN_PAYLOAD, "join payload"),
    (MATCH_START, "match start"), (ROOM_COUNT, "room count"),
    (BEAT_VITALS, "beat vitals"),
]
OPS_OPCODES = {0x09: "kill feed", 0x01: "round clock", 0x0A: "state block",
               0x0B: "state write", 0x0D: "state short"}
ACTOR_CLASSES = {0x00: "damage", 0x01: "state", 0x02: "position", 0x03: "vitals",
                 0x04: "weapon pair", 0x05: "aim", 0x06: "weapon byte"}
HIT_SUBTYPES = {0x11: "body", 0x12: "headshot", 0x42: "stun", 0x91: "knife",
                0x13: "blast"}
HIT_LENGTHS = (15, 28)
COMBAT_KINDS = {"aim", "damage", "vitals", "kill", "item box", "item box state"}
# The damage record of slot zero, seven above its vitals identifier: the one
# block the 1-based slot walk cannot name, because it starts at slot 1.
SLOT_ZERO_DAMAGE = {0x084A, 0x004A}


def family_of(identifier: int) -> str:
    """What a control identifier is, or `unknown`."""
    for identifiers, name in FAMILIES:
        if identifier in identifiers:
            return name
    if identifier & 0x4000:
        return "answer"
    return CONTROL_NAMES.get(identifier, "")


def ascii_of(body: bytes) -> str:
    return "".join(chr(byte) if 0x20 <= byte < 0x7F else "." for byte in body)


def signed(body: bytes, offset: int) -> int:
    value = body[offset] | (body[offset + 1] << 8)
    return value - 0x10000 if value & 0x8000 else value


class Capture:
    """One capture, decoded and indexed the way the report needs it."""

    def __init__(self, path: str):
        self.path = path
        capture = Pcapng(path)
        capture.read()
        datagrams = collect_datagrams(capture)
        joiner = find_joiner(datagrams)
        self.joiner = joiner
        self.host = next(
            (destination for _, source, destination, _ in datagrams if source == joiner), joiner)
        replay = Replay(datagrams, joiner, self.host)
        self.frames = [frame for frame in replay.frames if frame.verified]
        self.start = self.frames[0].ts if self.frames else 0.0

        self.roster: list[tuple[int, str, bytes]] = []
        self.inventory: Counter[tuple[int, int, str]] = Counter()
        self.bodies: dict[tuple[int, int, str], list[bytes]] = defaultdict(list)
        self.offsets: dict[tuple[int, int, str], list[set[int]]] = defaultdict(list)
        self.events: list[tuple] = []
        self.spawn: dict[int, tuple[float, float]] = {}
        self.membership: list[tuple] = []
        self._read()

    def _read(self) -> None:
        aim: dict[int, int] = {}
        vitals: dict[int, int] = {}
        for frame in self.frames:
            when = frame.ts - self.start
            side = "host" if frame.source == self.host else "client"
            for identifier, length, flags, body in frame.messages:
                # The round's identifier block decides, not the size of the number:
                # 0x0a61 (the rules roster) and 0x0a5c (the join state) sit below
                # 0x1000 and are session records, and their fourth byte is an
                # ordinal rather than an actor class.
                if self._is_actor(identifier, flags):
                    self._actor(when, side, identifier, length, flags, body, aim, vitals)
                else:
                    self._control(when, side, identifier, length, body)

    def _note(self, key: tuple[int, int, str], body: bytes) -> None:
        self.inventory[key] += 1
        if len(self.bodies[key]) < 4:
            self.bodies[key].append(body)
        while len(self.offsets[key]) < len(body):
            self.offsets[key].append(set())
        for offset, byte in enumerate(body):
            self.offsets[key][offset].add(byte)

    def _control(self, when: float, side: str, identifier: int,
                 length: int, body: bytes) -> None:
        family = family_of(identifier)
        self._note((identifier, length, family or "unknown"), body)
        if identifier in (0x1001, 0x9001, 0x5001) and is_roster_entry(body):
            slot = body[5]
            name = parse_profile(body)[0]
            if all(existing != slot for existing, _, _ in self.roster):
                self.roster.append((slot, name, body))
            self.events.append((when, side, "roster", slot,
                                f"slot {slot} '{name}' ({len(body)}B)", identifier, length, body))
            return
        if identifier in OPERATIONS and body:
            opcode = OPS_OPCODES.get(body[0], f"op 0x{body[0]:02x}")
            if body[0] == 0x09 and len(body) == 3:
                self.events.append((when, side, "kill", body[2],
                                    f"09 killer={body[1]:02d} victim={body[2]:02d}",
                                    identifier, length, body))
            else:
                self.events.append((when, side, "operations", 0, opcode,
                                    identifier, length, body))
        elif identifier in RULES_ROSTER and body:
            groups = self._membership(body)
            if groups:
                self.membership.append((when, groups))
            self.events.append((when, side, "rules", 0, f"membership {groups}",
                                identifier, length, body))
        elif identifier in ITEM_BOX or identifier in ITEM_BOX_STATE:
            self.events.append((when, side, "item box", 0, family,
                                identifier, length, body))
        elif identifier in MATCH_START:
            self.events.append((when, side, "match start", 0, "round marker",
                                identifier, length, body))
        elif identifier in ROOM_COUNT:
            self.events.append((when, side, "room count", body[-1] if body else -1,
                                f"population {body[-1] if body else -1}",
                                identifier, length, body))
        elif not family and identifier not in CONTROL_NAMES:
            self.events.append((when, side, "unknown control", 0,
                                f"unclassified 0x{identifier:04x}", identifier, length, body))
        else:
            self.events.append((when, side, "control", 0, family or
                                CONTROL_NAMES.get(identifier, "?"), identifier, length, body))

    def _actor(self, when: float, side: str, identifier: int, length: int,
               flags: int, body: bytes, aim: dict, vitals: dict) -> None:
        kind = ACTOR_CLASSES.get(flags, f"class 0x{flags:02x}")
        slot = self._slot_from(identifier)
        self._note((identifier, length, f"actor {kind}"), body)
        if flags not in ACTOR_CLASSES:
            self.events.append((when, side, "unknown actor", slot,
                                f"{kind} on {slot_of(identifier)}", identifier, length, body))
            return
        if flags == 0x01 and length == 9:
            target = body[1]
            if aim.get(slot) != target:
                aim[slot] = target
                self.events.append((when, side, "aim", slot,
                                    f"aims at {'none' if target == 0xFF else target}"
                                    f"{' (firing)' if body[2] else ''}",
                                    identifier, length, body))
        elif flags == 0x00 and length in HIT_LENGTHS:
            subtype = HIT_SUBTYPES.get(body[0], f"0x{body[0]:02x}")
            points = (body[2] | (body[3] << 8)) / 128.0
            self.events.append((when, side, "damage", slot,
                                f"{subtype} {points:.1f} on slot {slot}",
                                identifier, length, body))
        elif flags == 0x03 and length == 2:
            if vitals.get(slot) != body[0]:
                previous, vitals[slot] = vitals.get(slot, 250), body[0]
                self.events.append((when, side, "vitals", slot,
                                    f"health {previous} -> {body[0]} on slot {slot}",
                                    identifier, length, body))
        elif flags == 0x02 and length == 16 and slot >= 0 and slot not in self.spawn:
            self.spawn[slot] = (signed(body, 6) * 10.0, signed(body, 10) * 10.0)

    @staticmethod
    def _is_actor(identifier: int, flags: int) -> bool:
        """Whether a record sits in an actor's block: a slot's identifier, a class byte."""
        if identifier in SLOT_ZERO_DAMAGE:
            return flags == 0x00
        where = slot_of(identifier)
        if "/" not in where or flags > 0x06:
            return False
        return 1 <= int(where.split("/")[0]) <= 23

    @staticmethod
    def _slot_from(identifier: int) -> int:
        """The slot a record's identifier names, slot zero included."""
        if identifier in SLOT_ZERO_DAMAGE:
            return 0
        where = slot_of(identifier)
        return int(where.split("/")[0]) if "/" in where else -1

    @staticmethod
    def _membership(body: bytes) -> list[tuple[int, list[int]]]:
        groups, current, members = [], None, []
        for byte in body[1:]:
            if byte == 0xFE:
                if current is not None:
                    groups.append((current, members))
                current, members = None, []
            elif current is None:
                current = byte
            else:
                members.append(byte)
        return groups

    def field_map(self, key: tuple[int, int, str], limit: int = 40) -> str:
        """The record's bytes with the constant offsets written and the rest dotted."""
        bodies = self.bodies[key]
        count = self.inventory[key]
        width = min(limit, max((len(body) for body in bodies), default=0))
        cells = []
        for offset in range(width):
            values = self.offsets[key][offset]
            cells.append(f"{next(iter(values)):02x}" if len(values) == 1 and count > 1 else "..")
        return " ".join(cells)


def report(app: Capture, title: str, timeline: int) -> list[str]:
    lines = [f"### {title}", "", f"`{app.path}`", ""]
    lines.append(f"- frames: {len(app.frames)}; host {app.host}; client {app.joiner}")
    lines.append(f"- span: {(app.frames[-1].ts - app.start) if app.frames else 0:.2f} s")
    lines.append(f"- roster entries: {len(app.roster)}; spawn points: {len(app.spawn)}")
    lines.append(f"- events: {len(app.events)}")

    lines += ["", "#### Roster, in the order the host wrote it", "",
              "| slot | entry | name | bytes |", "|---|---|---|---|"]
    for slot, name, body in app.roster:
        lines.append(f"| {slot} | {len(body)}B | {name or '(no name)'} | "
                     f"`{body.hex(' ')[:60]}{'…' if len(body) > 20 else ''}` |")

    lines += ["", "#### Control records: count, first bodies, field map", "",
              "| identifier | family | length | count | first body | constant/varies |",
              "|---|---|---|---|---|---|"]
    keys = sorted((key for key in app.inventory if key[0] >= 0x1000),
                  key=lambda key: (key[2], key[1], key[0]))
    for key in keys:
        identifier, length, family = key
        sample = app.bodies[key][0].hex(" ") if app.bodies[key] else ""
        lines.append(f"| 0x{identifier:04x} | {family} | {length} | {app.inventory[key]} | "
                     f"`{sample[:48]}` | `{app.field_map(key)}` |")

    lines += ["", "#### Actor records per class and length", "",
              "| class | length | count | field map |", "|---|---|---|---|"]
    for key in sorted((key for key in app.inventory if not key[0] >= 0x1000),
                      key=lambda key: (key[2], key[1])):
        lines.append(f"| {key[2]} | {key[1]} | {app.inventory[key]} | `{app.field_map(key)}` |")

    lines += ["", "#### Spawn point per slot (first position record)", "",
              "| slot | x | z |", "|---|---|---|"]
    for slot in sorted(app.spawn):
        x, z = app.spawn[slot]
        lines.append(f"| {slot} | {x:.0f} | {z:.0f} |")

    unknown = sorted((key for key in app.inventory if key[2] == "unknown"),
                     key=lambda key: (-app.inventory[key], key[0]))
    lines += ["", "#### Unknown identifiers (no family named)", ""]
    if not unknown:
        lines.append("none: every record falls in a family this script names")
    else:
        lines += ["| identifier | length | count | first body |", "|---|---|---|---|"]
        for key in unknown:
            sample = app.bodies[key][0].hex(" ") if app.bodies[key] else ""
            lines.append(f"| 0x{key[0]:04x} | {key[1]} | {app.inventory[key]} | `{sample[:48]}` |")

    lines += ["", "#### Room state records, in order", "",
              "| t | side | record | body |", "|---|---|---|---|"]
    for when, side, kind, _slot, what, identifier, _length, body in app.events:
        if kind in ("room count", "match start"):
            lines.append(f"| {when:8.2f} | {side} | 0x{identifier:04x} {what} | "
                         f"`{body.hex(' ')[:40]}` |")

    if app.membership:
        when, groups = app.membership[-1]
        lines += ["", f"#### Latest rules membership (t+{when:.2f})", ""]
        for group, members in groups:
            lines.append(f"- group 0x{group:02x}: {members}")

    for heading, kinds in (("Combat timeline", COMBAT_KINDS), ("Timeline", None)):
        shown = [event for event in app.events if kinds is None or event[2] in kinds]
        lines += ["", f"#### {heading} (first {timeline} events, wire order)", "",
                  "| t | side | event | slot | what | record | body |",
                  "|---|---|---|---|---|---|---|"]
        for when, side, kind, slot, what, identifier, length, body in shown[:timeline]:
            lines.append(f"| {when:8.2f} | {side} | {kind} | {slot} | {what} | "
                         f"0x{identifier:04x}/{length}B | `{body.hex(' ')[:44]}` |")
    return lines


def diff(first: Capture, second: Capture) -> list[str]:
    lines = ["", "### Differences (working host -> this server)", ""]

    lines += ["#### Roster bytes, by slot", ""]
    left = {slot: (name, body) for slot, name, body in first.roster}
    right = {slot: (name, body) for slot, name, body in second.roster}
    for slot in sorted(set(left) | set(right)):
        if slot not in left or slot not in right:
            lines.append(f"- slot {slot}: present in {'working' if slot in left else 'this server'} only")
            continue
        (name_a, a), (name_b, b) = left[slot], right[slot]
        if a == b:
            lines.append(f"- slot {slot}: identical ({len(a)}B, '{name_a}')")
            continue
        changed = [f"+0x{offset:02x} {a[offset]:02x}->{b[offset]:02x}"
                   for offset in range(min(len(a), len(b))) if a[offset] != b[offset]]
        lines.append(f"- slot {slot}: {len(a)}B -> {len(b)}B, {', '.join(changed) or 'no shared byte differs'}")

    lines += ["", "#### Record inventory", "", "| record | working | this server | delta |",
              "|---|---|---|---|"]
    keys = set(first.inventory) | set(second.inventory)
    for key in sorted(keys, key=lambda key: (key[2], key[1], key[0])):
        a, b = first.inventory[key], second.inventory[key]
        if a == b:
            continue
        lines.append(f"| 0x{key[0]:04x}/{key[1]}B {key[2]} | {a} | {b} | {b - a:+d} |")

    lines += ["", "#### Records only one side sends", ""]
    only_first = sorted(set(first.inventory) - set(second.inventory),
                        key=lambda key: key[0])
    only_second = sorted(set(second.inventory) - set(first.inventory),
                         key=lambda key: key[0])
    for key in only_first:
        lines.append(f"- working only: 0x{key[0]:04x}/{key[1]}B {key[2]} "
                     f"x{first.inventory[key]} `{first.bodies[key][0].hex(' ')[:40]}`")
    for key in only_second:
        lines.append(f"- this server only: 0x{key[0]:04x}/{key[1]}B {key[2]} "
                     f"x{second.inventory[key]} `{second.bodies[key][0].hex(' ')[:40]}`")

    lines += ["", "#### Constant bytes that disagree", "",
              "| record | offset | working | this server |", "|---|---|---|---|"]
    for key in sorted(set(first.inventory) & set(second.inventory), key=lambda key: key[0]):
        if first.inventory[key] < 5 or second.inventory[key] < 5:
            continue
        width = min(len(first.offsets[key]), len(second.offsets[key]))
        for offset in range(width):
            a, b = first.offsets[key][offset], second.offsets[key][offset]
            if len(a) == 1 and len(b) == 1 and a != b:
                lines.append(f"| 0x{key[0]:04x}/{key[1]}B | +0x{offset:02x} | "
                             f"{next(iter(a)):02x} | {next(iter(b)):02x} |")

    lines += ["", "#### Event counts", "", "| event | working | this server |",
              "|---|---|---|"]
    left_counts = Counter(event[2] for event in first.events)
    right_counts = Counter(event[2] for event in second.events)
    for kind in sorted(set(left_counts) | set(right_counts)):
        lines.append(f"| {kind} | {left_counts[kind]} | {right_counts[kind]} |")

    lines += ["", "#### Spawn points", "", "| slot | working | this server |", "|---|---|---|"]
    for slot in sorted(set(first.spawn) | set(second.spawn)):
        a = first.spawn.get(slot)
        b = second.spawn.get(slot)
        lines.append(f"| {slot} | {f'{a[0]:.0f},{a[1]:.0f}' if a else '-'} | "
                     f"{f'{b[0]:.0f},{b[1]:.0f}' if b else '-'} |")

    lines += ["", "#### Item-box records", "", "| identifier | working | this server | bodies |",
              "|---|---|---|---|"]
    for identifiers, name in ((ITEM_BOX, "item box"), (ITEM_BOX_STATE, "item box state")):
        for identifier in sorted(identifiers):
            a = sum(count for key, count in first.inventory.items() if key[0] == identifier)
            b = sum(count for key, count in second.inventory.items() if key[0] == identifier)
            if a or b:
                sample = ""
                for key, bodies in second.bodies.items():
                    if key[0] == identifier and bodies:
                        sample = bodies[0].hex(" ")
                        break
                lines.append(f"| 0x{identifier:04x} {name} | {a} | {b} | `{sample[:40]}` |")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("captures", nargs=2)
    parser.add_argument("--timeline", type=int, default=80)
    parser.add_argument("--markdown", help="also write the report here")
    arguments = parser.parse_args()

    first, second = (Capture(path) for path in arguments.captures)
    lines = [f"# Capture comparison, {Path(arguments.captures[0]).name} against "
             f"{Path(arguments.captures[1]).name}", ""]
    lines += report(first, "Working host", arguments.timeline)
    lines += [""]
    lines += report(second, "This server", arguments.timeline)
    lines += diff(first, second)

    text = "\n".join(lines)
    print(text)
    if arguments.markdown:
        Path(arguments.markdown).write_text(text + "\n", encoding="utf-8")
        print(f"\nwritten to {arguments.markdown}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
