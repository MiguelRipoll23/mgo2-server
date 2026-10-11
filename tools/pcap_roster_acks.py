#!/usr/bin/env python3
"""Report which roster records a captured gameplay session acknowledged.

Usage:
    python3 tools/pcap_roster_acks.py CAPTURE.pcapng [--joiner IP:PORT] [--host IP:PORT]

The companion to `pcap_gameplay_log.py`, which replays a capture as the
gameplay server logs it. That answers "what did each side say"; this answers
"did the other side acknowledge it", which is the question the roster exists to
settle -- a joining client re-sends its profile until the host answers, so an
unanswered roster is a join that stalls rather than an error anyone is shown.

Everything about decoding a capture lives in `pcap_gameplay_log`; this file
only reads what it decoded and pairs the records. The pairing is the one
`PeerAcknowledgementUtils` documents: an answer travels under the same
identifier with bit 0x4000 set, carries the same fourth byte, and its body
length follows the 0x8000 class bit of the record it answers.

A body the host sent under `0x9001` or `0x1001` is not always a roster entry:
the `0b <slot>` settings payload shares those identifiers and used to read back
as an entry with a junk name in it. Those are decoded rather than listed as
roster records, by `udp_settings_semantics` -- see its report at the end.
"""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_record_semantics import (  # noqa: E402
    ACKNOWLEDGEMENT_BIT,
    ROSTER_TYPES,
    answer_body_length_of,
    character_id,
    is_record_acknowledgement,
    is_roster_entry,
    parse_profile,
    render_name,
    roster_index,
)
from udp_settings_semantics import SKILL_NAMES, parse_settings  # noqa: E402


def answers_by(replay: Replay, responder: str) -> set:
    """The records `responder` answered, as (identifier, body length, ordinal).

    Both the identifier and the body length are part of the pairing. An answer
    travels under the same identifier with bit 0x4000 set, so `0x9001` is
    answered by `0xd001` and `0x1001` by `0x5001`; and the body length follows
    the 0x8000 class bit of the record being answered, so the answer to a
    `0x9001` record carries one byte and the answer to a `0x1001` record none.
    Matching on the identifier alone pairs answers to records that were never
    asked about.

    Only frames that decoded count. A capture whose session key is unknown
    leaves the other side's frames unreadable, and counting their absence as
    "not answered" would report the capture's gap as a finding about the host.
    """
    answers = set()
    for frame in replay.frames:
        if frame.source != responder or not frame.verified:
            continue
        for message_type, _, flags, body in frame.messages:
            if is_record_acknowledgement(message_type, body):
                answers.add((message_type & ~ACKNOWLEDGEMENT_BIT, len(body), flags))
    return answers


def group_records(replay: Replay, endpoint: str, base: float) -> list[tuple]:
    """The roster records `endpoint` sent, grouped by what they say.

    Grouped rather than listed one per datagram, because a host repeats its
    whole roster every few seconds for the length of a round and the repetition
    is itself the finding: `first` and `last` are when the group was sent and
    `n` is how often.

    Only records that name a player are listed. A one-byte `0x9001` is the
    client asking rather than telling -- `PlayerProfileHandler` calls it a
    record that carries no profile -- and counting those would put hundreds of
    them beside the entries that matter. A name is not enough on its own: a
    body that is not a roster entry can still read back with one, so a record
    taken for the roster layout must also open the way an entry does.
    """
    groups: dict[tuple, list] = {}
    for frame in replay.frames:
        if frame.source != endpoint:
            continue
        for message_type, _, flags, body in frame.messages:
            if message_type not in ROSTER_TYPES:
                continue
            name, layout = parse_profile(body)
            if not name:
                continue
            if layout == "roster" and not is_roster_entry(body):
                continue
            key = (message_type, len(body), flags, layout, name)
            entry = groups.setdefault(key, [frame.ts - base, frame.ts - base, 0, body])
            entry[1] = frame.ts - base
            entry[2] += 1
    return [(key, *value) for key, value in groups.items()]


def report_side(replay: Replay, endpoint: str, other: str) -> None:
    """Prints one side's roster records and whether the other side answered them."""
    base = replay.frames[0].ts if replay.frames else 0.0
    print(f"\nroster records {endpoint} sent {other}")

    if replay.key is None:
        print(
            "  the session never keyed, so the answers that would pair with these records "
            "are among the frames this reader could not decode; whether they were "
            "answered is not knowable from this capture"
        )
        return

    answers = answers_by(replay, other)
    print(f"  {'first':>9} {'last':>9} {'n':>5} {'type':>6} {'len':>4} {'ord':>4} {'layout':>8} "
          f"{'char':>8} {'slot':>4} {'name':<18} answered")

    total = answered_count = 0
    for (message_type, length, flags, layout, name), first, last, count, body in group_records(
        replay, endpoint, base
    ):
        total += count
        acked = (message_type, answer_body_length_of(message_type), flags) in answers
        answered_count += count if acked else 0
        print(
            f"  {first:9.3f} {last:9.3f} {count:5} 0x{message_type:04x} {length:4} {flags:4} "
            f"{layout:>8} {character_id(body, layout):>8} {roster_index(body, layout):>4} "
            f"{render_name(name):<18} {'yes' if acked else 'NO'}"
        )
    print(f"  {total} sent, {answered_count} answered, {total - answered_count} not answered")


def roster_report(replay: Replay) -> None:
    """Prints the roster records each side sent, host first."""
    report_side(replay, replay.host, replay.joiner)
    report_side(replay, replay.joiner, replay.host)


def gender_name(value: int) -> str:
    """The appearance gender byte as the replay decoder names it: 0 male, 1 female."""
    return {0: "male", 1: "female"}.get(value, f"gender {value}")


def settings_report(replay: Replay) -> None:
    """Prints the `0b` settings payloads the host sent, decoded.

    The companion to the roster tables above: the host's own payload and each
    player's loadout travel under the same identifiers a roster entry does, and
    are decoded here instead of being read as entries with a name of control
    bytes. Every field comes from `udp_settings_semantics`; this function only
groups what it decoded and prints it.
    """
    base = replay.frames[0].ts if replay.frames else 0.0
    groups: dict[tuple, list] = {}
    for frame in replay.frames:
        if frame.source != replay.host or not frame.verified:
            continue
        for _, _, _, body in frame.messages:
            decoded = parse_settings(body)
            if decoded is None:
                continue
            if decoded["kind"] == "rotation":
                key = ("rotation", len(body), tuple(decoded["rotation"]))
            else:
                appearance = decoded["appearance"]
                key = (
                    "loadout",
                    len(body),
                    decoded["slot"],
                    decoded["team"],
                    (appearance["gender"], appearance["head"], appearance["chest"],
                     appearance["hand"], appearance["waist"], appearance["foot"],
                     appearance["acc1"], appearance["acc2"]),
                    None if decoded["skills"] is None
                    else tuple((skill[0], skill[2]) for skill in decoded["skills"]),
                )
            entry = groups.setdefault(key, [frame.ts - base, frame.ts - base, 0])
            entry[1] = frame.ts - base
            entry[2] += 1

    print(f"\nsettings payloads (0x0b) {replay.host} sent {replay.joiner}")
    if not groups:
        print("  (none decoded)")
        return

    for key, (first, last, count) in sorted(groups.items(), key=lambda kv: kv[0]):
        if key[0] == "rotation":
            _, length, rotation = key
            maps = ", ".join(f"{map_id}/{mode}" for map_id, mode, _ in rotation)
            print(f"  {first:9.3f} {last:9.3f} {count:5}  rotation {length:4} B  "
                  f"map/mode: {maps}")
            continue

        _, length, slot, team, appearance, skills = key
        gender, *gear = appearance
        skill_text = (
            "unreadable" if skills is None
            else ", ".join(f"{SKILL_NAMES[skill_id - 1]}/{level}" for skill_id, level in skills)
            or "none"
        )
        print(f"  {first:9.3f} {last:9.3f} {count:5}  loadout  {length:4} B  "
              f"slot {slot}, team {team}, {gender_name(gender)}, "
              f"gear [{' '.join(str(byte) for byte in gear)}], skills {skill_text}")


def load(capture_path: str, joiner: str | None, host: str | None) -> Replay:
    """Reads the capture and returns its gameplay session."""
    capture = Pcapng(capture_path)
    capture.read()
    datagrams = collect_datagrams(capture)

    joiner = joiner or find_joiner(datagrams)
    if joiner is None:
        raise SystemExit(f"{capture_path}: no peer-to-peer handshake")
    if not host:
        host = next(peer for _, source, peer, _ in datagrams if peer != joiner and source == joiner)

    replay = Replay(datagrams, joiner, host)
    for endpoint in (joiner, host):
        role = "joiner" if endpoint == joiner else "host"
        if endpoint in replay.bases:
            print(
                f"  {role:7} {endpoint:22} peer_id={replay.peer_ids[endpoint]:<8} "
                f"counter_base=0x{replay.bases[endpoint]:08x}"
            )
        else:
            print(f"  {role:7} {endpoint:22} no handshake in the capture")
    if replay.key is None:
        print("  session key unknown: only one counter base is in the capture, keyed frames stay unread")
    else:
        print(f"  session key = 0x{replay.key:08x}")
    return replay


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("capture")
    parser.add_argument("--joiner", help="the joining client, default: whoever sent the first handshake")
    parser.add_argument("--host", help="the dedicated host, default: the other side of that flow")
    arguments = parser.parse_args()

    print(f"{arguments.capture}: roster acknowledgement report")
    replay = load(arguments.capture, arguments.joiner, arguments.host)
    roster_report(replay)
    settings_report(replay)


if __name__ == "__main__":
    main()
