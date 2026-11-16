#!/usr/bin/env python3
"""Replay a capture through the client's own record rules, as the binary spells them.

Usage:
    python3 tools/udp_ordinal_rules.py CAPTURE.pcapng [CAPTURE.pcapng ...] [--stream id12|idfull]

Why it exists: the host has to write each stream's fourth byte in the order the
client takes them, and the order is not a convention this repository invented —
it is four statements in `MGO2.ELF`, decompiled here and named at each rule:

- **R1, the take.** `read_queued_p2p_message` @0x26b5c8 compares a record's
  fourth byte with the stream entry's expected byte at `+0xe`; on a mismatch it
  jumps to its drop path, which clears the queue slot and returns nothing. The
  expected byte does not move. On a match it increments the expected byte. So a
  record that arrives ahead of the one the stream is waiting for is **discarded,
  and its value is lost** until the byte-wide counter comes round to it.
- **R2, the seed.** `_opd_FUN_0026b178` @0x26b178 initialises an entry's expected
  byte to `0`, so every stream's first accepted record carries `0x00`.
- **R3, the answer.** `decode_p2p_datagram` @0x2666c8's `type & 0x4000` branch is
  the ack reader: it walks that stream's own sent entries and marks the one whose
  fourth byte equals the answer's byte as acknowledged (bit `0x80` of its flags).
  An answer names a record the peer actually took, which is why a host can read
  its answers as the peer's view of the stream.
- **R4, the send counter.** `P2P_SendRecord` @0x26b778 increments the byte it
  stamps per record sent, and refuses to send when the step would land on `0xff`
  while the entry is more than `0x17` ahead of what was acknowledged — the same
  refusal the reference shows by never writing `0xff` in a running stream.

**What a capture cannot settle on its own is the stream's name.** The binary
applies R1 to a record whose flags byte has bits `0x05` set, and the flags are
not in the wire header — the header is the identifier, the body length and the
fourth byte the rule compares. So which records share one run is a question the
working captures have to answer, and `--stream` asks it both ways: `id12` merges
`0x1001`, `0x5001`, `0x9001` and `0xd001` into one run (the identifier's low
12 bits, which is what the ack reader's walk keys on), and `idfull` gives each
identifier its own counter. **Neither reading is settled**, so a refused count
does not choose between them.

**A refusal is normal in a working capture, and that is the finding.** The
reference's own streams do it: `.pcapngs/pcapng1`'s `0x1001` runs
`00 01 02 03 04 05 06 10 07 08 …`, writing `0x10` a value ahead of the one
before it, and its `0xd001` records run `01 02 03 05 …` with `04` missing. On a
reader that compares for equality each of those is a drop. The session works
because the value is written again later, which is why this prints **written
again later** beside the count: that number is what separates a stream that
recovers from one that does not, and it is the property a host has to reproduce.
A refused record the capture never writes again is the one that costs the wrap
(`docs/analysis/stream-sequencing.md`).
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from udp_frame import lzss_decompress  # noqa: E402
from udp_session_records import collect, key_for, parse  # noqa: E402


def records_of(path: str):
    """Every session record of every flow, as (time, direction, type, flags, body)."""
    datagrams, bases, joiners, origins = collect(path)
    out = []
    for ts, local, remote, payload in datagrams:
        pair = frozenset((local, remote))
        at = ts - origins.get(pair, ts)
        joiner = joiners.get(pair)
        if joiner is None:
            continue
        _counter, plain, verified, _ = _decode(payload, key_for(bases, local, remote))
        if not verified:
            continue
        content = plain[2 : len(plain) - 0xA]
        if _counter & 0x8000:
            content = lzss_decompress(content)
        direction = "client" if local == joiner else "host"
        for message_type, _length, flags, body in parse(content):
            if message_type < 0x1000:
                continue
            out.append((at, direction, message_type, flags, body))
    return out


def _decode(payload, key):
    from udp_frame import decode_frame

    return decode_frame(payload, key)


def replay(records, stream_mode: str):
    """Applies R1 and R2 to one side's records; returns (accepted, refusals, waits)."""
    expected: dict[int, int] = {}
    accepted = 0
    refusals = []
    for index, (at, direction, message_type, flags, _body) in enumerate(records):
        if direction != "host":
            continue
        stream = message_type & 0x0FFF if stream_mode == "id12" else message_type
        want = expected.get(stream, 0)
        if flags == want:
            expected[stream] = (want + 1) & 0xFF
            accepted += 1
            continue
        refusals.append((index, at, message_type, flags, want))
    return accepted, refusals, expected


def written_again(records, stream_mode: str):
    """The last position each stream wrote each byte at, for the recovery test."""
    last = {}
    for index, (_at, direction, message_type, flags, _body) in enumerate(records):
        if direction != "host":
            continue
        last[(stream_of(message_type, stream_mode), flags)] = index
    return last


def stream_of(message_type: int, stream_mode: str) -> int:
    """The run one host record belongs to, under the chosen reading."""
    return message_type & 0x0FFF if stream_mode == "id12" else message_type


def answers(records):
    """R3: the answers the client sent, as (stream, byte) pairs."""
    return [
        (message_type & 0x0FFF, flags)
        for _at, direction, message_type, flags, _body in records
        if direction == "client" and message_type & 0x4000
    ]


def report(path: str, stream_mode: str) -> None:
    """Prints one capture's replay under the chosen stream reading."""
    records = records_of(path)
    if not records:
        print(f"== {Path(path).name}: no decryptable peer session")
        return
    accepted, refusals, expected = replay(records, stream_mode)
    last_writes = written_again(records, stream_mode)
    recovered = sum(
        1
        for index, _at, message_type, flags, _want in refusals
        if last_writes.get((stream_of(message_type, stream_mode), flags), -1) > index
    )
    pairs = answers(records)
    streams = sorted({message_type & 0x0FFF for _a, d, message_type, _f, _b in records if d == "host"})
    print(
        f"== {Path(path).name}: {len(records)} record(s), host streams "
        f"{', '.join(f'0x{s:03x}' for s in streams[:4])}"
        f"{' …' if len(streams) > 4 else ''}"
    )
    print(
        f"   taken {accepted}, refused {len(refusals)}, written again later {recovered}, "
        f"client answers {len(pairs)}"
    )
    if refusals:
        print(
            "   (the gate that selects ordinal-checked records is not identified, so these are "
            "what this reading would drop; the reference drops records too, so the count that "
            "matters is 'written again later')"
        )
    for _index, at, message_type, flags, want in refusals[:4]:
        print(
            f"   refused at t+{at:8.2f}  0x{message_type:04x} byte 0x{flags:02x} "
            f"while the stream waited for 0x{want:02x}"
        )
    if refusals:
        first = refusals[0]
        span = refusals[-1][1] - first[1]
        print(
            f"   the stream is refused from t+{first[1]:.2f} to t+{refusals[-1][1]:.2f} "
            f"({span:.0f} s) unless the waited-on byte arrives"
        )
    waiting = {stream: value for stream, value in expected.items() if value}
    if waiting:
        print(
            "   streams end waiting on: "
            + ", ".join(f"0x{s:03x}->0x{v:02x}" for s, v in sorted(waiting.items()))
        )
    print()


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        return
    mode = sys.argv[sys.argv.index("--stream") + 1] if "--stream" in sys.argv else "id12"
    if mode not in ("id12", "idfull"):
        raise SystemExit("--stream takes id12 or idfull")
    for path in sys.argv[1:]:
        if path.startswith("--"):
            continue
        report(path, mode)


if __name__ == "__main__":
    main()
