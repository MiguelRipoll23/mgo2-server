"""Players and kill feeds read straight off a capture.

Nothing here is assumed about who holds a slot: a player is the profile entry
that names it (the 57/63-byte short layout and the 68-byte host layout both carry
the slot at byte 5 and the name as the printable tail), and a kill is the
three-byte `09 <killer> <victim>` record the operations stream carries. The
membership record (`0a61`, class 0x00) is decoded the way its grammar states: a
group byte, its members, and 0xfe ends.

    python3 tools/kill_feed_players.py .pcapngs/myserver.pcapng [--feeds N] [--around]
"""
import re
import sys

sys.path.insert(0, 'tools')

from pcap_conversations import Pcapng
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner

LIST_END = 0xfe


def name_of(body):
    """The player's name: the last printable run in the record's tail."""
    runs = [m.group() for m in re.finditer(rb'[\x20-\x7e]{2,}', body[24:])]
    return runs[-1].decode('ascii').strip() if runs else ''


def membership(body):
    """Source-derived membership: group byte, members, 0xfe — repeated."""
    groups, current, members = [], None, []
    for byte in body[1:]:
        if byte == LIST_END:
            if current is not None:
                groups.append((current, members))
            current, members = None, []
            continue
        if current is None:
            current = byte
            continue
        members.append(byte)
    return groups


def main():
    path = sys.argv[1]
    limit = 20
    if '--feeds' in sys.argv:
        limit = int(sys.argv[sys.argv.index('--feeds') + 1])
    around = '--around' in sys.argv

    capture = Pcapng(path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    host = next((destination for _, source, destination, _ in datagrams if source == joiner), joiner)
    replay = Replay(datagrams, joiner, host)
    frames = [frame for frame in replay.frames if frame.verified]
    first = frames[0].ts

    players, feeds, members_last = {}, [], None
    for frame in frames:
        rel = frame.ts - first
        for kind, length, _flags, body in frame.messages:
            if body is None:
                continue
            if (kind & 0x7ff) == 0x254 and len(body) == 3 and body[0] == 0x09:
                feeds.append((rel, body[1], body[2], list(frame.messages)))
            if frame.source != host:
                continue
            if kind == 0x1001 and len(body) in (57, 63, 68) and body[0] == 0x07:
                players[body[5]] = (name_of(body), len(body))
            if (kind & 0x7ff) == 0x061 and body[0] == 0x00 and len(body) > 3:
                members_last = (rel, membership(body))

    print(f"{path}")
    print(f"  players read from the profile entries ({len(players)}):")
    for slot in sorted(players):
        name, length = players[slot]
        print(f"    slot {slot:2d}  {name or '(no name)':<16} ({length}-byte entry)")

    if members_last:
        rel, groups = members_last
        print(f"  membership (0a61 class 0x00) latest at t+{rel:.2f}:")
        for group, members_list in groups:
            print(f"    group 0x{group:02x}: {members_list}")

    print(f"  kill feeds, resolved against those entries ({len(feeds)}):")
    unresolved = credited_to_zero = 0
    for rel, killer, victim, messages in feeds[:limit]:
        kn = players.get(killer, ('no entry', 0))[0]
        vn = players.get(victim, ('no entry', 0))[0]
        mark = ''
        if killer not in players:
            unresolved += 1
            mark = '  <-- killer has no profile entry'
        elif killer == 0:
            credited_to_zero += 1
            mark = '  <-- killer is slot 0'
        print(f"    t+{rel:7.2f}  slot {killer:2d} '{kn}' killed slot {victim:2d} '{vn}'{mark}")
        if around:
            for kind, length, body in messages:
                if body is not None and length <= 32:
                    print(f"        {kind:04x} l={length:2d} {body.hex(' ')}")

    print(f"  feeds whose killer is slot 0: {credited_to_zero}; "
          f"whose killer has no profile entry: {unresolved}")


if __name__ == '__main__':
    main()
