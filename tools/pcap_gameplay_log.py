#!/usr/bin/env python3
"""Replay a capture's peer-to-peer session as the gameplay server logs it.

Usage:
    python3 tools/pcap_gameplay_log.py CAPTURE.pcapng [--roster] [--limit N]
                             [--joiner IP:PORT] [--host IP:PORT] [--port N]
                             [--skip SECONDS] [--until SECONDS]

The gameplay server writes two lines for every datagram it moves: the raw bytes
as they arrived or left, then the frame it made of them, its records and the
handler each record reached. `GameplayServerService`, `TrafficLogger` and
`PeerAcknowledgementUtils` are the three files whose wording this reproduces,
so a capture and a live run of `scripts/run-gameplay-local.sh` can be read
against each other line for line:

    python3 scripts/run-gameplay-local.sh            # writes the server log
    python3 tools/pcap_gameplay_log.py mgo2-game1.pcapng

The capture is read as the *host's* side, because that is the side this project
implements. Datagrams the dedicated host sent are the server's `OUT` lines and
datagrams the joining client sent are its `IN` lines; the joiner is the side
whose first datagram of the flow is a handshake, which is the only thing that
tells the two apart without being told.

Two things cannot be recovered from the wire and are not invented. The server
files a session under the endpoint a handshake arrived from and dials back to
the endpoint that handshake advertised; a capture shows the first and not the
second, so `dial-back` reads as the observed source. And the `answering N
reliable records` line is only written when the host's next outbound frame
really does carry those answers -- a host that should have answered and did not
gets a warning naming what went unanswered, which is the finding rather than a
line quietly missing.

`--roster` adds the acknowledgement report: every roster record each side sent,
which of them the other side answered, and the total either way.
"""

from __future__ import annotations

import argparse
import struct
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from pcap_conversations import UDP, Pcapng  # noqa: E402
from udp_frame import PRE_KEY, decode, handshake_of, lzss_decompress, parse_messages  # noqa: E402

# What a record means, ported from the C# that decides it. Re-exported so the
# sibling tools can read a capture through this module alone.
from udp_record_semantics import (  # noqa: E402
    ACKNOWLEDGEMENT_BIT,
    COMPRESSION_MARKER,
    HANDLERS,
    HANDSHAKE,
    JOIN_REQUEST,
    PAYLOAD_CLASS_BIT,
    PLAYER_PROFILE,
    ROSTER_HEAD,
    ROSTER_TYPES,
    TICK_RECORD_THRESHOLD,
    answer_body_length_of,
    answer_type_of,
    character_id,
    is_frame_acknowledgement,
    parse_profile,
    profile_name,
    render_name,
    roster_index,
    waits_for_acknowledgement,
)

# Ports that are not the peer-to-peer channel. STUN shares the client's p2p
# port and DNS shares nothing with it, so both are excluded by port.
EXCLUDED_PORTS = {3478, 53}


def format_hex(payload: bytes) -> str:
    """Space-separated hexadecimal, the way TrafficLogger.FormatHex writes it."""
    return " ".join(f"{byte:02x}" for byte in payload)


def format_hex_ascii(payload: bytes) -> str:
    """Hex with an ASCII column, sixteen bytes a line, as FormatHexAscii writes it."""
    lines = []
    for offset in range(0, len(payload), 16):
        chunk = payload[offset : offset + 16]
        ascii_text = "".join(chr(byte) if 0x20 <= byte < 0x7E else "." for byte in chunk)
        lines.append(f"{offset:04x}  {format_hex(chunk).ljust(47)}  {ascii_text}")
    return "\n".join(lines)


class Frame:
    """One datagram of the flow, decoded the way the gameplay server decodes it."""

    def __init__(self, ts, source, destination, wire, counter, plain, verified, keyed):
        self.ts = ts
        self.source = source
        self.destination = destination
        self.wire = wire
        self.counter = counter
        self.plain = plain
        self.verified = verified
        self.keyed = keyed
        self.messages = []
        if verified:
            content = plain[2 : len(plain) - 0xA]
            if counter & COMPRESSION_MARKER:
                content = lzss_decompress(content)
            self.messages = parse_messages(content)

    @property
    def compressed(self) -> bool:
        return bool(self.counter & COMPRESSION_MARKER)


def collect_datagrams(capture: Pcapng) -> list[tuple]:
    """Every UDP datagram of the capture that could be the peer-to-peer channel."""
    datagrams = []
    for ts, protocol, source, source_port, destination, destination_port, payload in (
        capture.decoded_packets()
    ):
        if protocol != UDP:
            continue
        if source_port in EXCLUDED_PORTS or destination_port in EXCLUDED_PORTS:
            continue
        datagrams.append(
            (ts, f"{source}:{source_port}", f"{destination}:{destination_port}", bytes(payload))
        )
    return datagrams


def find_joiner(datagrams: list[tuple]) -> str | None:
    """The side whose first datagram is a handshake, which is the joiner."""
    for _, source, _, wire in datagrams:
        if handshake_of(wire) is not None:
            return source
    return None


class Replay:
    """The capture's session, keyed and decoded as the gameplay server does."""

    def __init__(self, datagrams, joiner, host):
        self.joiner = joiner
        self.host = host
        self.bases: dict[str, int] = {}
        self.peer_ids: dict[str, int] = {}
        self.key: int | None = None
        self.frames: list[Frame] = []

        for ts, source, destination, wire in datagrams:
            if {source, destination} != {joiner, host}:
                continue

            handshake = handshake_of(wire)
            if handshake is not None and source not in self.bases:
                self.peer_ids[source], self.bases[source] = handshake
            if len(self.bases) == 2:
                self.key = self.bases[joiner] ^ self.bases[host]

            # The digest key classifies the frame before the chain is removed:
            # a pre-keyed frame verifies with the bare constant, a keyed frame
            # with the session key combined with it.
            counter, plain, verified = decode(wire, PRE_KEY)
            keyed = False
            if not verified and self.key is not None:
                counter, plain, verified = decode(wire, self.key)
                keyed = verified
            self.frames.append(Frame(ts, source, destination, wire, counter, plain, verified, keyed))

    def answer_types_due(self, frame: Frame) -> list[int]:
        """The record types this host owes an answer to for one keyed frame.

        A player-profile record is the exception: its roster response opens with
        the same 0x5001 that would otherwise be its answer, so it is not
        answered separately (`GameplayServerService.DispatchKeyedAsync`).
        """
        return [
            message_type
            for message_type, _, _, body in frame.messages
            if waits_for_acknowledgement(message_type, body)
            and not (
                (message_type & ~PAYLOAD_CLASS_BIT) == PLAYER_PROFILE and profile_name(body)
            )
        ]

    def next_frame_from(self, index: int, endpoint: str) -> Frame | None:
        """The next frame `endpoint` sent after the one at `index`."""
        for frame in self.frames[index + 1 :]:
            if frame.source == endpoint:
                return frame
        return None


def timestamp(ts: float) -> str:
    """The log's `HH:MM:SS`, from the capture's own clock."""
    return time.strftime("%H:%M:%S", time.gmtime(ts))


class Writer:
    """Writes under the `[HH:MM:SS LVL]` prefix the gameplay server logs with."""

    def __init__(self, port: int):
        self.port = port

    def __call__(self, ts: float, text: str, level: str = "DBG") -> None:
        print(f"[{timestamp(ts)} {level}] {text}")

    def warning(self, ts: float, text: str) -> None:
        self(ts, text, "WRN")


def log_handler(out: Writer, ts: float, remote: str, message_type: int, flags: int, body: bytes) -> None:
    """The invoking/completed pair for one record that reaches a handler."""
    handler = HANDLERS.get(message_type)
    if handler is None:
        if message_type >= TICK_RECORD_THRESHOLD:
            out.warning(
                ts,
                f"[udp:{out.port}] IN {remote} message type {message_type:04x}: "
                f"{len(body)} bytes dropped, no handler",
            )
        return
    out(
        ts,
        f"[udp:{out.port}] IN {remote} invoking {handler} for 0x{message_type:04x} "
        f"({len(body)} body bytes, ordinal={flags})",
    )
    if handler == "PlayerProfileHandler" and not profile_name(body):
        out(
            ts,
            f"UDP {out.port}: {message_type:04x} of {len(body)} bytes from {remote} "
            "carries no profile; not answered",
        )
    out(ts, f"[udp:{out.port}] IN {remote} completed {handler} for 0x{message_type:04x}")


def log_answers(out: Writer, replay: Replay, frame: Frame, index: int, remote: str) -> None:
    """The `answering N reliable records` line, or the gap where it was not sent."""
    due = replay.answer_types_due(frame)
    if not due:
        return

    sent = replay.next_frame_from(index, replay.host)
    sent_types = {message_type for message_type, _, _, _ in sent.messages} if sent else set()
    answered = [message_type for message_type in due if answer_type_of(message_type) in sent_types]
    named = ", ".join(f"0x{message_type:04x}" for message_type in due)
    if len(answered) == len(due):
        out(
            frame.ts,
            f"[udp:{out.port}] OUT {remote} answering {len(due)} reliable records: "
            + ", ".join(f"0x{answer_type_of(message_type):04x}" for message_type in due),
        )
        return

    got = ", ".join(f"0x{answer_type_of(message_type):04x}" for message_type in answered) or "none"
    out.warning(
        frame.ts,
        f"[udp:{out.port}] OUT {remote} {len(due)} records await an answer ({named}) "
        f"and the host answered {got}",
    )


def log_inbound(out: Writer, replay: Replay, frame: Frame, index: int) -> None:
    """The server's `IN` lines for one datagram the joiner sent."""
    remote = frame.source
    out(frame.ts, f"[udp:{out.port}] IN {remote} {len(frame.wire)} bytes")
    print(format_hex_ascii(frame.wire))

    if not frame.verified:
        out.warning(
            frame.ts,
            f"[udp:{out.port}] IN {remote} undecodable ({len(frame.wire)} bytes): "
            "no session is keyed on that endpoint and it does not verify pre-keyed either",
        )
        return

    compression = "compressed" if frame.compressed else "uncompressed"
    if frame.keyed:
        out(
            frame.ts,
            f"[udp:{out.port}] IN {remote} passed the session tail-digest check at counter {frame.counter}",
        )
        out(
            frame.ts,
            f"[udp:{out.port}] IN frame counter={frame.counter} key={replay.key:08x} "
            f"messages={len(frame.messages)} compression={compression} from {remote} "
            f"({len(frame.plain)} bytes): {format_hex(frame.plain)}",
        )
        log_answers(out, replay, frame, index, remote)
        for message_type, _, flags, body in frame.messages:
            log_handler(out, frame.ts, remote, message_type, flags, body)
        return

    is_handshake = len(frame.wire) == 44 and any(
        message_type == HANDSHAKE for message_type, _, _, _ in frame.messages
    )
    out(
        frame.ts,
        f"[udp:{out.port}] IN {remote} passed the pre-key tail-digest check at counter {frame.counter}",
    )
    out(
        frame.ts,
        f"[udp:{out.port}] IN frame counter={frame.counter}"
        f"{' handshake' if is_handshake else ' pre-keyed'} compression={compression} from {remote} "
        f"({len(frame.plain)} bytes): {format_hex(frame.plain)}",
    )
    for message_type, _, _, body in frame.messages:
        out(
            frame.ts,
            f"[udp:{out.port}] IN {remote} dispatching pre-keyed message 0x{message_type:04x} "
            f"({len(body)} body bytes)",
        )
        log_handler(out, frame.ts, remote, message_type, 0, body)


def log_outbound(out: Writer, frame: Frame, peer: str) -> None:
    """The server's `OUT` lines for one datagram the host sent."""
    if not frame.verified:
        out.warning(frame.ts, f"[udp:{out.port}] OUT {peer} {len(frame.wire)} bytes could not be decoded")
        return

    records = ", ".join(
        f"0x{message_type:04x}/{len(body)}B/ordinal={flags}"
        for message_type, _, flags, body in frame.messages
    )
    compression = "compressed" if frame.compressed else "uncompressed"
    out(
        frame.ts,
        f"[udp:{out.port}] OUT {peer} building frame counter={frame.counter}, "
        f"compression={compression}, keyed={str(frame.keyed).lower()}, records=[{records}]",
    )
    out(
        frame.ts,
        f"[udp:{out.port}] OUT frame counter={frame.counter} messages={len(frame.messages)}"
        f"{'' if frame.keyed else ' pre-keyed'} compression={compression} to {peer} "
        f"({len(frame.wire)} bytes): {format_hex(frame.wire)}",
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("capture")
    parser.add_argument("--joiner", help="the joining client, default: whoever sent the first handshake")
    parser.add_argument("--host", help="the dedicated host, default: the other side of that flow")
    parser.add_argument("--port", type=int, help="port for the [udp:N] prefix, default: the host's")
    parser.add_argument("--limit", type=int, default=0, help="stop after N frames, 0 for all of them")
    parser.add_argument("--skip", type=float, default=0.0, help="start this many seconds in")
    parser.add_argument("--until", type=float, help="stop this many seconds in")
    arguments = parser.parse_args()

    capture = Pcapng(arguments.capture)
    capture.read()
    datagrams = collect_datagrams(capture)

    joiner = arguments.joiner or find_joiner(datagrams)
    if joiner is None:
        raise SystemExit("no peer-to-peer handshake in this capture")
    host = arguments.host or next(
        peer for _, source, peer, _ in datagrams if peer != joiner and source == joiner
    )

    replay = Replay(datagrams, joiner, host)
    base = replay.frames[0].ts if replay.frames else 0.0

    print(f"{arguments.capture}: {len(datagrams)} UDP datagrams, {len(replay.frames)} in the gameplay flow")
    print(f"joiner {joiner}  host {host}")

    writer = Writer(arguments.port or int(host.rsplit(":", 1)[1]))
    shown = 0
    for index, frame in enumerate(replay.frames):
        relative = frame.ts - base
        if relative < arguments.skip or (arguments.until is not None and relative > arguments.until):
            continue
        if frame.source == joiner:
            writer(
                frame.ts,
                f"[udp:{writer.port}] IN {frame.source} looking up session: "
                f"peer=0x{replay.peer_ids.get(joiner, 0):08x}, dial-back={frame.source} (1 sessions)",
            )
            log_inbound(writer, replay, frame, index)
        else:
            log_outbound(writer, frame, joiner)
        shown += 1
        if arguments.limit and shown >= arguments.limit:
            break


if __name__ == "__main__":
    main()
