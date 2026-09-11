#!/usr/bin/env python3
"""Parse MGO2 survival-match replay files (the game's RPDT format).

Replays come from a SaveMGO-compatible site via
`/api/v1/download-replay/<survivalmatch-id>`; the archive holds one file named
`replay_<game_id>_<replay_id>.dat`. Everything below was derived from direct
byte analysis (cross-checked against the site's match API and the world f32
coordinates inside the file).

WHAT THE FILE IS
----------------
Not a raw network capture: there are no p2p handshakes, no `module_magic`
(`b7 8a 25 4d`) and no IP/port pairs anywhere in the file. It is the host's
recorded **data-phase** stream — the same `{type u16, len u8, ctr u8, body}`
record shape the UDP channel uses (docs/udp-p2p-protocol.md §6.2) for the
opening header block only.

LAYOUT
------
    [0x00..0x08)  "RPDT" magic + version bytes
    [0x07]        map id (17 = Desert Duel, 4 = Gronznyj Grad, ...)
    [0x0c]        size of the first record (which starts at 0x50)
    [0x50]        first record: {ctr u16, type u16 (=0x1001), len u8, flags u8}
                  + body carrying the first character's id at body+8 (u16 LE)
    [0x8a]        12 player records (16-byte prefix, NUL-terminated name,
                  non-terminated clan name, 56-byte tail)
    [0x13ed..]    the recorded data-phase stream, organised in TICKS:

        tick header (30 bytes): `61 02 10 00 ff <12 per-player counters>
          fe fe 61 02 02 01 fe <tick u32> 00 00 00 <var> 02`

        then a sequence of RECORDS. Every record is **self-describing**:

            {id u16 LE, len u8, class u8, data[len-1]}

        `len` is data_len+1 and `class` is the attribute class. `id & 0xFF`
        identifies the player: for class C the id low byte is
        `0x75 + 10*slot + OFF[C]` with

            OFF[0x01] = 0   OFF[0x02] = 2   OFF[0x04] = 4
            OFF[0x05] = 6   OFF[0x06] = 4   OFF[0x03] = 1

        (the second round re-sends the same ids with bit 0x800 set; the low
        byte is unchanged, so slot decoding still works).

        Observed classes / data sizes:

            0x01  state          3 bytes (`00 ff 00` alive), 9 bytes (aim/fire)
            0x02  transform     16 / 17 / 18 / 32 / 38 / 44 bytes
            0x03  vitals         2 bytes  -> {HP, STAMINA}, each 0..250
            0x04  ?              5 bytes
            0x05  ?              6 bytes (3 x i16)
            0x06  ?              1 / 2 bytes
            0x08  rare event    13 bytes

    STAMINA
    -------
    class 0x03 is `{HP u8, STAMINA u8}` (0xFA = 250 = full). HP drops are hits;
    HP -> 0 is a kill. The second byte is STAMINA: it depletes and then
    regenerates over the match (observed climbing 0 -> 250 on players who used
    it), and some players cap at a lower value (a skill cost).

    POSITION / FACING
    -----------------
    class 0x02 carries the player transform. Its data size selects which
    fields are present (the tail block is always [yaw_a, z, y, x, yaw_b]);

        data 16/17/18 : yaw @12, z @6, y @8, x @10      (walking update)
        data 32       : f32 pos @8,  yaw_a @20, z @22, y @24, x @26, yaw_b @28
        data 38       :              yaw_a @26, z @28, y @30, x @32, yaw_b @34
        data 44       : f32 pos @8,  yaw_a @32, z @34, y @36, x @38, yaw_b @40
                        (plus a second f32 triple @20..31)

    The compressed position is 3 x i16 in the order (z, y, x) at scale x10.
    Verified: the i16 x/z reproduce the f32 world coordinates to <1 unit, and
    the yaw tracks the player's movement bearing. In the aim/shoot variants
    (data 32/38/44) a second angle appears that equals the first while walking
    and diverges while aiming, so it is reported as the camera/aim yaw.

    HITS / KILLS — WHAT IS AND ISN'T IN THE FILE
    --------------------------------------------
    The vitals record names the **victim** (whose HP dropped). There is NO
    attacker field anywhere in the stream (verified: no record co-varies with
    HP drops, and the class 0x02 "shot" variant's origin always equals the
    shooter's own position, not the victim's). The attacker can only be
    INFERRED: a player whose class 0x01 len-10 (aim/fire) record sits in the
    same tick is printed as a candidate attacker.

USAGE
-----
    python3 tools/mgo2_replay_parser.py <replay.dat>                  # everything
    python3 tools/mgo2_replay_parser.py <replay.dat> --show=pos       # position + facing
    python3 tools/mgo2_replay_parser.py <replay.dat> --show=hit,kill  # damage + deaths
    python3 tools/mgo2_replay_parser.py <replay.dat> --show=stam      # stamina gauge
    python3 tools/mgo2_replay_parser.py <replay.dat> --show=fire      # aim/fire updates
    python3 tools/mgo2_replay_parser.py <replay.dat> --events=50      # cap output

Event kinds: POS (position + yaw), HIT (victim HP drop), KILL (victim HP -> 0),
STAM (stamina change), FIRE (class 0x01 aim/fire update).

NOTE: hits/kills name the VICTIM only -- the wire format has no attacker field,
so `attacker(inferred)` lists players with an aim/fire record in the same tick.
"""

import struct
import sys

RECORD_COUNT = 12
RECORD_TAIL = 56
PLAYER_SECTION = 0x8A
FIRST_RECORD = 0x50
TICK_START = 0x13ED
TICK_HEADER = 30

# class -> offset added to 0x75+10*slot to form the record id low byte
CLASS_OFF = {0x01: 0, 0x02: 2, 0x03: 1, 0x04: 4, 0x05: 6, 0x06: 4}

# class 0x02 data size -> offset of the f32 world position, when present
F32_POS = {32: 8, 44: 8}

# class 0x02 data size -> (yaw_a, z, y, x, yaw_b) i16 offsets
# Verified across the file: x*10 / z*10 reproduce the f32 world coordinates,
# yaw_a == yaw_b while walking and diverge while aiming.
TAIL = {
    16: (4, 6, 8, 10, 12),
    17: (4, 6, 8, 10, 12),
    18: (4, 6, 8, 10, 12),
    32: (20, 22, 24, 26, 28),
    38: (26, 28, 30, 32, 34),
    44: (32, 34, 36, 38, 40),
}


def u16(data, off):
    return struct.unpack_from("<H", data, off)[0]


def u32(data, off):
    return struct.unpack_from("<I", data, off)[0]


def i16(data, off):
    v = struct.unpack_from("<H", data, off)[0]
    return v - 0x10000 if v >= 0x8000 else v


def f32(data, off):
    return struct.unpack_from("<f", data, off)[0]


def cstr(data, off):
    end = data.find(b"\x00", off)
    return data[off:end if end >= 0 else len(data)].decode("latin-1")


def slot_of(ident, cls):
    off = CLASS_OFF.get(cls)
    if off is None:
        return None
    lb = ident & 0xFF
    d = lb - 0x75 - off
    if d < 0 or d % 10 or d // 10 >= RECORD_COUNT:
        return None
    return d // 10


def angle_deg(raw):
    """16-bit signed circle angle -> degrees (65536 = 360)."""
    return raw * 360.0 / 65536.0


# ---------------------------------------------------------------- header ----

def parse_header(data):
    lines = [f"magic            : {data[:4].decode(errors='replace')}"]
    lines.append(f"version bytes    : {data[4:7].hex(' ')}")
    lines.append(f"map id           : {data[7]}  (offset 0x07)")
    lines.append(f"first rec size   : {u32(data, 0x0c)} (offset 0x0c)")
    size0 = u32(data, 0x0c)
    body0 = data[FIRST_RECORD + 6:FIRST_RECORD + size0]
    lines.append(f"record0 @0x50    : ctr={u16(data, FIRST_RECORD)} "
                 f"type=0x{u16(data, FIRST_RECORD + 2):04x} flags={data[FIRST_RECORD + 5]}")
    if len(body0) >= 10:
        lines.append(f"  first char id  : {u16(body0, 0x8)}")
    return lines


def parse_players(data):
    lines = ["  players:"]
    pos = PLAYER_SECTION
    players = []
    for i in range(RECORD_COUNT):
        prefix = data[pos:pos + 0x10]
        name = cstr(data, pos + 0x10)
        co = pos + 0x10 + len(name.encode("latin-1")) + 1
        tail_start = data.find(b"\x01\x10", co, co + 32)
        clan = ""
        if tail_start > co:
            clan = data[co:tail_start].decode("latin-1")
        else:
            tail_start = co
        players.append((name or f"slot{i}", clan))
        lines.append(f"    [{i:2d}] {name or '-':<14} clan={clan or '-':<18} "
                     f"flag={prefix[0x0f]:#04x}")
        pos = tail_start + RECORD_TAIL
    lines.append(f"  records end     : 0x{pos:x}")
    return players, lines


# ------------------------------------------------------------------ ticks ---

def tick_starts(data):
    starts = []
    n = len(data)
    p = TICK_START
    while p < n - 4:
        if data[p:p + 2] == b"\x61\x02" and data[p + 3] == 0x00:
            starts.append(p)
        p += 1
    return starts


def parse_ticks(data):
    """Yield (tick_index, file_offset, records).

    Each record is (offset, ident, cls, data_bytes). Records with an invalid
    length are skipped one byte at a time to re-sync.
    """
    starts = tick_starts(data)
    for ti in range(len(starts) - 1):
        t0, t1 = starts[ti], starts[ti + 1]
        p = t0 + TICK_HEADER
        recs = []
        while p < t1 - 3:
            ln = data[p + 2]
            cls = data[p + 3]
            if ln < 1 or p + 3 + ln > t1:
                p += 1
                continue
            recs.append((p, u16(data, p), cls, data[p + 4:p + 3 + ln]))
            p += 3 + ln
        yield ti, t0, recs


# ------------------------------------------------------------------ output --

def decode_transform(cls_data):
    """Return a human string for a class 0x02 record, or None."""
    size = len(cls_data)
    offsets = TAIL.get(size)
    if offsets is None:
        return None
    oa, oz, oy, ox, ob = offsets
    z, y, x = i16(cls_data, oz), i16(cls_data, oy), i16(cls_data, ox)
    parts = []
    fo = F32_POS.get(size)
    if fo is not None:
        parts.append(f"pos=({f32(cls_data, fo):.0f},{f32(cls_data, fo + 4):.0f},"
                     f"{f32(cls_data, fo + 8):.0f}) f32")
    parts.append(f"pos=({x * 10},{y * 10},{z * 10}) i16")
    body = angle_deg(i16(cls_data, ob))
    if size >= 32:
        # the aim/shoot variants carry a second angle that only diverges while aiming
        cam = angle_deg(i16(cls_data, oa))
        parts.append(f"body_yaw={body:.1f} cam_yaw={cam:.1f}"
                     + (" (aiming)" if abs(body - cam) > 0.5 else ""))
    else:
        parts.append(f"yaw={body:.1f}")
    return " ".join(parts)


def parse(path, show=("all",), max_events=0):
    data = open(path, "rb").read()
    if data[:4] != b"RPDT":
        raise ValueError(f"{path}: not an RPDT replay (magic {data[:4]!r})")

    def want(kind):
        return "all" in show or kind in show

    out = [f"=== {path} ({len(data):,} bytes) ==="]
    out += parse_header(data)
    players, plines = parse_players(data)
    out += plines
    out.append("")
    out.append("== recorded stream (in file order) ==")

    hp = [None] * RECORD_COUNT
    stamina = [None] * RECORD_COUNT
    events = []
    counts = {"POS": 0, "HIT": 0, "KILL": 0, "STAM": 0, "FIRE": 0}
    total = 0

    for _, _, recs in parse_ticks(data):
        # first pass: which players have an aim/fire record in this tick
        fires = [s for _, ident, cls, d in recs
                 for s in [slot_of(ident, cls)]
                 if s is not None and cls == 0x01 and len(d) == 9]

        for off, ident, cls, d in recs:
            total += 1
            slot = slot_of(ident, cls)
            name = players[slot][0] if slot is not None else "?"
            who = f"slot {slot:2d} [{name}]" if slot is not None else "?"

            if cls == 0x02:
                text = decode_transform(d)
                if text:
                    counts["POS"] += 1
                    if want("pos"):
                        events.append((off, f"POS   {who}", text))
            elif cls == 0x03 and slot is not None and len(d) == 2:
                h, s = d[0], d[1]
                old = hp[slot]
                old_s = stamina[slot]
                hp[slot] = h
                stamina[slot] = s
                if old is not None and h < old:
                    dmg = old - h
                    atk = " ".join(f"slot {a} [{players[a][0]}]" for a in sorted(set(fires)) if a != slot)
                    atk = atk or "unknown"
                    if h == 0:
                        counts["KILL"] += 1
                        if want("kill"):
                            events.append((off, f"KILL  victim {who}",
                                           f"HP {old}->0 (took {dmg})  attacker(inferred)={atk}"))
                    else:
                        counts["HIT"] += 1
                        if want("hit"):
                            events.append((off, f"HIT   victim {who}",
                                           f"HP {old}->{h} (-{dmg})  stamina={s}  "
                                           f"attacker(inferred)={atk}"))
                if old_s is not None and s != old_s:
                    counts["STAM"] += 1
                    if want("stam"):
                        events.append((off, f"STAM  {who}", f"stamina {old_s} -> {s} (hp={h})"))
            elif cls == 0x01 and len(d) == 9:
                counts["FIRE"] += 1
                if want("fire"):
                    a, b, c = i16(d, 3), i16(d, 5), i16(d, 7)
                    events.append((off, f"FIRE  {who}", f"state={d[0]:#04x} "
                                   f"vec=({a},{b},{c})"))

    if max_events:
        events = events[:max_events]
    for off, kind, text in events:
        out.append(f"  0x{off:06x}  {kind:28s} {text}")
    out.append("")
    out.append(f"  {total:,} records parsed; totals: "
               f"{counts['KILL']} kills, {counts['HIT']} hits, "
               f"{counts['POS']} position updates, {counts['FIRE']} fire/aim updates, "
               f"{counts['STAM']} stamina changes")
    out.append(f"  {len(events):,} events shown")
    return "\n".join(out)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    show = ("all",)
    max_events = 0
    for a in sys.argv[1:]:
        if a.startswith("--show="):
            show = tuple(x.strip() for x in a.split("=", 1)[1].split(","))
        elif a.startswith("--events="):
            max_events = int(a.split("=", 1)[1])
    for p in args:
        print(parse(p, show=show, max_events=max_events))
        print()
