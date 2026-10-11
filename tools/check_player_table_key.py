#!/usr/bin/env python3
"""Decide whether our session-slot allocation matches the captured hosts.

Usage:
    python3 tools/check_player_table_key.py

Our server hands each joining player the lowest free slot in the room's
24-entry player table, starting at 1, in the order players join
(`RoomRosterService.NextFreeSessionSlot`) -- the byte the replay console's
`rpdt.js` calls the record's `slot`. This reads the same field -- offset
0x05 of a player's roster entry -- out of the three reference captures and
prints the two side by side, so "does the current behaviour match" is answered
from the wire rather than from the doc comment.

Everything about decoding a capture lives in `pcap_gameplay_log` and
`udp_record_semantics`; this file only reads what they decoded and compares.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_record_semantics import (  # noqa: E402
    JOIN_REQUEST,
    PLAYER_PROFILE,
    ROSTER_TYPES,
    ROSTER_INDEX_BASE,
    ROSTER_INDEX_FIELD,
    parse_profile,
)

CAPTURES = ("mgo2-game1.pcapng", "mgo2-game2.pcapng", "mgo2-game3.pcapng")


def slot_of(body: bytes) -> int | None:
    """Roster slot a record reports, or None when the record is not that layout."""
    if len(body) <= ROSTER_INDEX_FIELD:
        return None
    field = body[ROSTER_INDEX_FIELD]
    return -1 if field == 0x00 else field - ROSTER_INDEX_BASE


def printable_name(body: bytes) -> str:
    """The character name a record carries, however that record lays it out.

    The roster layout puts it at a fixed offset; some of the captured rounds put
    it elsewhere. Both are runs of printable bytes that end at a NUL, so the
    fallback looks for the longest such run in the record's second half rather
    than at one offset -- this is a report about a field at 0x05, and refusing
    to read the name would hide records that carry the field it is about.
    """
    best = ""
    for start in range(len(body) // 2, len(body)):
        end = body.find(b"\x00", start)
        if end <= start:
            continue
        run = body[start:end]
        if all(0x20 <= byte < 0x7F for byte in run) and len(run) > len(best):
            best = run.decode("iso-8859-1")
    return best.strip()


def host_roster_entries(replay: Replay) -> list[dict]:
    """The first roster run the host sent, as records that name a player.

    A run is the host's answer to a join: the same roster in one or two frames
    (the run and its repeat). Only records whose name reads non-empty are kept,
    because a named record is the only kind that is a player rather than a
    structural marker, and only the first run is used so a repeated roster is
    not counted as new players.
    """
    entries: list[dict] = []
    seen: set[tuple] = set()

    for frame in replay.frames:
        if frame.source != replay.host or not frame.verified:
            continue
        for message_type, _, flags, body in frame.messages:
            if message_type not in ROSTER_TYPES:
                continue
            name, _ = parse_profile(body)
            if not name:
                name = printable_name(body)
            if not name:
                continue
            entry = {
                "counter": frame.counter,
                "type": message_type,
                "flags": flags,
                "length": len(body),
                "slot": slot_of(body) if len(body) > ROSTER_INDEX_FIELD else None,
                "raw_slot": body[ROSTER_INDEX_FIELD] if len(body) > ROSTER_INDEX_FIELD else None,
                "key": body[5] if len(body) > 5 else None,
                "name": name,
            }
            key = (entry["length"], entry["flags"], entry["name"])
            if key in seen:
                continue
            seen.add(key)
            entries.append(entry)
    return entries


def joiners(entries: list[dict]) -> list[dict]:
    """The named records that are joining players, host excluded, in send order.

    A record only counts when its slot reads as a real player slot: the host's
    own entry is slot -1, and a record that is not the roster layout at all
    reads a slot outside 0..23 from whatever byte it happens to hold there.
    """
    out = []
    for entry in entries:
        slot = entry["slot"]
        if slot is None or slot < 0 or slot > 23:
            continue
        out.append(entry)
    return out


def main() -> int:
    print("our rule: first joiner gets key 1, next 2, in join order (lowest free)")
    print("field compared: offset 0x05 of a player's roster entry (0x07 repeats it)")

    verdicts = []
    for name in CAPTURES:
        path = Path(name)
        print(f"\n================ {name} ================")
        if not path.exists():
            print("  missing")
            verdicts.append((name, "missing"))
            continue

        capture = Pcapng(str(path))
        capture.read()
        datagrams = collect_datagrams(capture)
        joiner_endpoint = find_joiner(datagrams)
        if joiner_endpoint is None:
            print("  no peer-to-peer handshake")
            verdicts.append((name, "no handshake"))
            continue
        host = next(peer for _, source, peer, _ in datagrams if peer != joiner_endpoint and source == joiner_endpoint)

        replay = Replay(datagrams, joiner_endpoint, host)
        entries = host_roster_entries(replay)
        players = joiners(entries)

        if not entries:
            print("  no named roster entry read from this capture")
            verdicts.append((name, "unread"))
            continue

        print(f"  joiner={joiner_endpoint}  host={host}")
        print(f"  {'ord':>4} {'type':>6} {'len':>4} {'slot':>4} {'key':>5} {'ours':>5}  name")
        matches = 0
        for index, entry in enumerate(players):
            ours = index + 1
            key = entry["key"]
            same = key == ours
            matches += 1 if same else 0
            print(
                f"  {entry['flags']:>4} 0x{entry['type']:04x} {entry['length']:>4} "
                f"{entry['slot']!s:>4} {key:#05x} {ours:#05x}  {entry['name']}"
                + ("" if same else "   <-- differs")
            )

        # Records whose slot byte does not decode as the roster layout are still
        # roster-ish and carry the compared field; they are shown, not compared.
        unknown = [
            entry
            for entry in entries
            if entry["slot"] is None or not 0 <= entry["slot"] <= 23
        ]
        if unknown:
            print("  records whose slot byte is not the roster layout (not compared):")
            for entry in unknown:
                print(
                    f"    ord={entry['flags']:>3} 0x{entry['type']:04x} len={entry['length']:>3} "
                    f"[0x04]={entry['raw_slot']!r} key={entry['key']:#05x}  {entry['name']}"
                )

        total = len(players)
        if not players:
            verdict = "no comparable joiner"
        elif matches == total:
            verdict = "MATCHES"
        else:
            verdict = "DIFFERS"
        print(f"  {matches}/{total} keys match our rule: {verdict}")
        verdicts.append((name, verdict))

    print("\n================ verdict ================")
    for name, verdict in verdicts:
        print(f"  {name:24} {verdict}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
