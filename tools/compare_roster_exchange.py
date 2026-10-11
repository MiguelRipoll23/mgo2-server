#!/usr/bin/env python3
"""Compare a captured roster exchange with our own server's, record for record.

Usage:
    python3 tools/compare_roster_exchange.py [--capture mgo2-game1.pcapng]
                                             [--log /tmp/mgo2-gameplay-server-3333.log]

Reads our roster out of the gameplay server's own log and the reference host's
out of a capture, both through `pcap_gameplay_log`, and prints the two side by
side: which records each sends, in what order, under which identifiers, with
which fourth bytes, and whether the peer answered them.

The comparison is only meaningful up to the point where the two servers differ
in *policy*, and this prints the policy differences rather than a verdict on
which is right. `docs/protocol/UDP_GAME_CAPTURE.md` is where a captured shape
and a reason for writing it belong; this is the tool that says which shape is
in front of you.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import Pcapng  # noqa: E402
from pcap_gameplay_log import Replay, collect_datagrams, find_joiner  # noqa: E402
from udp_frame import lzss_decompress, parse_messages  # noqa: E402
from udp_record_semantics import (  # noqa: E402
    ACKNOWLEDGEMENT_BIT,
    COMPRESSION_MARKER,
    ROSTER_TYPES,
    answer_body_length_of,
    is_record_acknowledgement,
    parse_profile,
)

# One entry of the `records=[...]` list an `OUT ... building frame` line carries.
RECORD = re.compile(r"0x(?P<type>[0-9a-f]{4})/(?P<length>\d+)B/ordinal=(?P<ordinal>\d+)")


def our_roster(log_path: str) -> list[tuple[int, int, int]]:
    """The roster frames our own server sent, as (counter, type, length, ordinal).

    Read from the log rather than recomputed, because the point is what this
    server actually put on the wire -- the same bytes a capture would show --
    and not what `RoomRosterService` would build today.
    """
    frames = []
    pattern = re.compile(
        r"\[udp:\d+\] OUT \S+ building frame counter=(\d+), compression=(\w+), "
        r"keyed=(\w+), records=\[(.*?)\]\s*$"
    )
    for line in Path(log_path).read_text(encoding="utf-8", errors="replace").splitlines():
        match = pattern.search(line)
        if not match:
            continue
        counter, compression, keyed, records = match.groups()
        if compression != "compressed":
            continue
        parsed = [
            (
                int(counter),
                int(record.group("type"), 16),
                int(record.group("length")),
                int(record.group("ordinal")),
            )
            for record in RECORD.finditer(records)
        ]
        if any(message_type in ROSTER_TYPES for _, message_type, _, _ in parsed):
            frames.append((int(counter), compression, keyed, parsed))
    return frames


def captured_roster(capture_path: str) -> tuple[list, str, str, Replay]:
    """The reference host's roster frames, as the capture holds them.

    The replay comes back too because the client's answers live in the same
    pass and re-reading the capture for them would decode every frame twice.
    """
    capture = Pcapng(capture_path)
    capture.read()
    datagrams = collect_datagrams(capture)
    joiner = find_joiner(datagrams)
    if joiner is None:
        raise SystemExit(f"{capture_path}: no peer-to-peer handshake")
    host = next(peer for _, source, peer, _ in datagrams if peer != joiner and source == joiner)
    replay = Replay(datagrams, joiner, host)

    frames = []
    for frame in replay.frames:
        if frame.source != host or not frame.verified:
            continue
        parsed = [
            (frame.counter, message_type, len(body), flags)
            for message_type, _, flags, body in frame.messages
        ]
        if any(message_type in ROSTER_TYPES for _, message_type, _, _ in parsed):
            frames.append((frame.counter, frame, parsed))
    return frames, joiner, host, replay


def answers_received(replay: Replay, responder: str) -> set:
    """The records `responder` answered, as (identifier, body length, ordinal)."""
    answers = set()
    for frame in replay.frames:
        if frame.source != responder or not frame.verified:
            continue
        for message_type, _, flags, body in frame.messages:
            if is_record_acknowledgement(message_type, body):
                answers.add((message_type & ~0x4000, len(body), flags))
    return answers


def answers_received_in_log(log_path: str) -> set:
    """The records the client answered in our own log, the same three fields.

    The log carries every inbound frame the server decoded as hex, which is
    the same plaintext a capture would hold, so the answers are read back out
    of it rather than from a second capture of our own server.

    A compressed frame's content region is still LZSS at this point -- the log
    line is written after the header is unscrambled but before the chain is
    removed -- so it is inflated here. Walking it raw reads the compressed
    bytes as records and invents answers to identifiers that were never sent.
    """
    answers = set()
    pattern = re.compile(
        r"IN frame counter=(?P<counter>\d+) key=[0-9a-f]+ messages=\d+ "
        r"compression=(?P<compression>\w+) from \S+ \(\d+ bytes\): (?P<hex>[0-9a-f ]+)$"
    )
    for line in Path(log_path).read_text(encoding="utf-8", errors="replace").splitlines():
        match = pattern.search(line)
        if not match:
            continue
        plain = bytes.fromhex(match.group("hex").replace(" ", ""))
        content = plain[2 : len(plain) - 0xA]
        if int(match.group("counter")) & COMPRESSION_MARKER:
            content = lzss_decompress(content)
        for message_type, _, flags, body in parse_messages(content):
            # A frame acknowledgement closes an outbound frame and is not an
            # answer to a record; `PeerAcknowledgementUtils.IsFrameAcknowledgement`.
            if is_record_acknowledgement(message_type, body):
                answers.add((message_type & ~ACKNOWLEDGEMENT_BIT, len(body), flags))
    return answers


def show_our_side(log_path: str) -> None:
    frames = our_roster(log_path)
    answered = answers_received_in_log(log_path)
    print(f"our server, from {log_path}")
    if not frames:
        print("  no compressed roster frame in the log")
        return
    print(f"  {len(frames)} roster frames; the client sent {len(answered)} answers in the log")
    for counter, compression, keyed, records in frames:
        print(f"\n  frame counter={counter} {compression} keyed={keyed}, {len(records)} records")
        for _, message_type, length, ordinal in records:
            key = (message_type, answer_body_length_of(message_type), ordinal)
            print(
                f"    0x{message_type:04x} {length:3}B ordinal={ordinal} "
                f"answered={'yes' if key in answered else 'NO'}"
            )


def show_captured_side(capture_path: str, opening: int) -> None:
    """Prints the captured host's roster frames, newest last, `opening` of them.

    A whole round holds 236 frames carrying a roster identifier, almost all of
    them gameplay records riding the same identifiers. Only the opening is
    shown by default because that is where the exchange happens -- the run, its
    repeat and the trailer, in the order a join sees them -- and the rest is
    the round going on. `--all` prints every one.
    """
    frames, joiner, host, replay = captured_roster(capture_path)
    answered = answers_received(replay, joiner)
    shown = frames if opening < 0 else frames[:opening]

    print(f"\ncaptured host, from {capture_path}")
    print(
        f"  {host} -> {joiner}, {len(frames)} frames carrying a roster identifier"
        + (f", showing the first {len(shown)}" if len(shown) < len(frames) else "")
    )
    for counter, frame, records in shown:
        names = [
            parse_profile(body)[0]
            for message_type, _, _, body in frame.messages
            if message_type in ROSTER_TYPES
        ]
        named = [name for name in names if name]
        print(
            f"\n  frame counter={counter} "
            f"{'compressed' if frame.compressed else 'uncompressed'}, {len(records)} records"
            + (f", names {named}" if named else "")
        )
        for _, message_type, length, ordinal in records:
            key = (message_type, answer_body_length_of(message_type), ordinal)
            print(
                f"    0x{message_type:04x} {length:3}B ordinal={ordinal} "
                f"answered={'yes' if key in answered else 'NO'}"
            )


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--capture", default="mgo2-game1.pcapng")
    parser.add_argument("--log", default="/tmp/mgo2-gameplay-server-3333.log")
    parser.add_argument(
        "--opening",
        type=int,
        default=3,
        help="captured roster frames to print, -1 for all of them (default 3)",
    )
    arguments = parser.parse_args()

    show_our_side(arguments.log)
    show_captured_side(arguments.capture, arguments.opening)


if __name__ == "__main__":
    main()
