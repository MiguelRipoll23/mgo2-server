#!/usr/bin/env python3
"""The record semantics the gameplay server applies, in Python.

The Python twin of the two C# files that decide what a record means:
`PlayerProfileRecordParseUtils` (is this body a player, and who) and
`PeerAcknowledgementUtils` (does this record owe an answer, and what shape is
it). Both are ports here rather than reimplementations, so a capture and the
live server agree on what a record says instead of merely looking alike.

Kept apart from `pcap_gameplay_log.py` because these rules are a reading of
the protocol rather than part of reading a capture: nothing here knows what a
pcapng is, and every tool that decodes a session imports them from here.
"""

from __future__ import annotations

# UdpCommandConstants.
HANDSHAKE = 0x1000
KEEP_ALIVE = 0x5000
PLAYER_PROFILE = 0x1001
JOIN_REQUEST = 0x9001
ROSTER_HEAD = 0x5001
TICK_RECORD_THRESHOLD = 0x1000
ACKNOWLEDGEMENT_BIT = 0x4000
PAYLOAD_CLASS_BIT = 0x8000
COMPRESSION_MARKER = 0x8000

# The two identifiers a player-profile record travels under: the roster type,
# and the tag a live joiner opens the exchange with -- which the host then sends
# its own entry under, so it is both what is asked and what is answered.
ROSTER_TYPES = (PLAYER_PROFILE, JOIN_REQUEST)

# PeerCommandHandlerRegistration.
HANDLERS = {
    HANDSHAKE: "AcceptHandshakeHandler",
    KEEP_ALIVE: "PeerKeepAliveHandler",
    PLAYER_PROFILE: "PlayerProfileHandler",
    JOIN_REQUEST: "PlayerProfileHandler",
}

# PlayerProfileRecordUtility: the fixed fields of the roster layout.
CHARACTER_ID_OFFSET = 0x08
ROSTER_INDEX_FIELD = 0x04
ROSTER_INDEX_BASE = 0xE2
NAME_MARKER_OFFSET = 0x43
NAME_OFFSET = 0x44

# The short record layout, twelve bytes below the long one. Which of the two a
# record uses is decided per record; see `_name_offset`.
SHORT_NAME_OFFSET = 0x38

# The shortest body either layout can carry a name in.
MINIMUM_BODY_SIZE = SHORT_NAME_OFFSET + 2

# PlayerProfileRecordUtility: the two bytes that open a roster record -- the
# version, and the record's OWN LENGTH less seven. The length byte moves with the
# character name (`fargraven2` makes a 79-byte entry read 0x48, `Libertine` a
# 78-byte one read 0x47), so it is not a record kind. A body that merely parses
# against the layout is not a record, because neither byte describes it: the
# host's own `0b` payload lines up with the layout and fails both.
RECORD_VERSION = 0x07
RECORD_LENGTH_OFFSET = 0x01
FIXED_PREFIX_LENGTH = 0x07


def _starts_a_name(byte: int) -> bool:
    """Letters, digits and the underscore: what a character name may open with."""
    return byte == 0x5F or (0x30 <= byte <= 0x39) or (0x41 <= byte <= 0x5A) or (0x61 <= byte <= 0x7A)


def parse_profile(body: bytes) -> tuple[str, str]:
    """The name a player-profile record carries and the layout it was read from.

    A port of the name recovery in `PlayerProfileRecordParseUtils.Parse`: a
    roster record carries its names at fixed offsets -- 0x44, or 0x38 in the
    short layout -- and anything else is read back from the end of the record the
    way a join request is.

    Which of the two answered is returned alongside, because the trailing read
    is the lenient one -- it walks back over any printable run -- and a record
    that is not a profile at all can still come back with a name in it. That is
    what the server does with it too, so a report shows the layout rather than
    hiding the record.
    """
    if len(body) < MINIMUM_BODY_SIZE:
        return "", ""

    if is_roster_entry(body):
        name_offset = _name_offset(body)
        name_end = body[name_offset:].find(b"\x00") if len(body) > name_offset else -1
        if name_end > 0:
            return (
                body[name_offset : name_offset + name_end].decode("iso-8859-1", "replace"),
                "roster",
            )

    clan_start = body.rfind(b"\x00") + 1
    name_end = clan_start - 1
    name_start = name_end
    while name_start > 0 and 0x20 <= body[name_start - 1] < 0x7F:
        name_start -= 1
    while name_start < name_end and not _starts_a_name(body[name_start]):
        name_start += 1
    if name_end <= name_start:
        return "", ""
    return body[name_start:name_end].decode("iso-8859-1", "replace"), "trailing"


def _name_offset(body: bytes) -> int:
    """Which of the two name offsets a record uses.

    A port of `PlayerProfileRecordParseUtils.NameOffsetOf`, and of the replay
    console's `rosterNameOff`: the record ends with the nick, a NUL and the clan,
    so the nick is the printable run before the last NUL, and the short layout is
    the one present when that run begins exactly SHORT_NAME_OFFSET bytes in. The
    captures need both -- game1's roster is the long layout, game2's and game3's
    the short -- and the header's format version does not distinguish them.
    """
    end = len(body) - 1
    while end > 0 and body[end] != 0x00:
        end -= 1
    start = end
    while start > 0 and body[start - 1] >= 0x20:
        start -= 1
    return SHORT_NAME_OFFSET if (start == SHORT_NAME_OFFSET and end > start) else NAME_OFFSET


def is_roster_entry(body: bytes) -> bool:
    """Whether a body opens the way a roster record does.

    The version and the record's own length byte are its first two bytes, and
    they are what tells a record apart from a body that merely lines up with the
    layout -- the `0b` settings payload does, and reads back as a record with a
    name of two control bytes in it. A report that trusted the layout alone
    would list that payload as a player.
    """
    return (
        len(body) > RECORD_LENGTH_OFFSET
        and body[0] == RECORD_VERSION
        and body[RECORD_LENGTH_OFFSET] == len(body) - FIXED_PREFIX_LENGTH
    )


def profile_name(body: bytes) -> str:
    """The character name a record carries, or empty when it is none."""
    return parse_profile(body)[0]


def character_id(body: bytes, layout: str) -> str:
    """Character id a roster entry carries, or `-` when it has none to carry.

    Only the roster layout has the field. On a join request the same offset
    lands inside its own header, so the number would belong to nothing -- which
    is why `PlayerProfileRecordParseUtils` reads it from that layout alone.
    """
    if layout != "roster" or len(body) < CHARACTER_ID_OFFSET + 4:
        return "-"
    return str(int.from_bytes(body[CHARACTER_ID_OFFSET : CHARACTER_ID_OFFSET + 4], "little"))


def roster_index(body: bytes, layout: str) -> str:
    """Roster slot a record reports, or `-` when the record does not carry one.

    The field is the long layout's. A short record's host writes a different base
    there -- game2's writes 0x01 and 0x02 where game1's writes 0xe2 plus the
    index -- so no index is claimed from one rather than a number that belongs to
    no scale.
    """
    if layout != "roster" or len(body) <= ROSTER_INDEX_FIELD:
        return "-"
    if _name_offset(body) == SHORT_NAME_OFFSET:
        return "-"
    field = body[ROSTER_INDEX_FIELD]
    return str(-1 if field == 0x00 else field - ROSTER_INDEX_BASE)


def render_name(name: str) -> str:
    """A name as a report prints it, with the unprintable bytes escaped.

    A record that is not a profile can still read back with a name in it, and
    those bytes are the finding -- a report that printed them as nothing would
    hide the record that produced them.
    """
    return "".join(
        character if 0x20 <= ord(character) < 0x7F else f"\\x{ord(character):02x}"
        for character in name
    )


def is_frame_acknowledgement(message_type: int, body: bytes) -> bool:
    """A one-byte zero acknowledgement entry, which closes an outbound frame.

    Not an answer to a record: it names a frame sequence and is never answered
    in turn, which is why the server excludes it from both directions.
    """
    return (message_type & 0xF000) == HANDSHAKE and len(body) == 1 and body[0] == 0


def is_record_acknowledgement(message_type: int, body: bytes) -> bool:
    """An answer to one of our records: the evidence a roster was accepted.

    The keep-alive is excluded by identifier, not by class. It is id 0 of the
    `0x5x..` class, so it and the answer to a `0x1001` record -- `0x5001` -- are
    the same four bits apart, and masking the answer bit off `0x5000` leaves
    the handshake, which is not a numbered record. Reading the keep-alive as an
    answer would make every join look acknowledged the moment the client says
    hello.
    """
    return (
        message_type >= TICK_RECORD_THRESHOLD
        and bool(message_type & ACKNOWLEDGEMENT_BIT)
        and message_type != KEEP_ALIVE
        and not is_frame_acknowledgement(message_type, body)
    )


def waits_for_acknowledgement(message_type: int, body: bytes) -> bool:
    """Whether a record is one this host owes a record-level answer."""
    if message_type < TICK_RECORD_THRESHOLD or message_type & ACKNOWLEDGEMENT_BIT:
        return False
    return not is_frame_acknowledgement(message_type, body)


def answer_type_of(message_type: int) -> int:
    """The identifier the answer to a record travels under."""
    return message_type | ACKNOWLEDGEMENT_BIT


def answer_body_length_of(message_type: int) -> int:
    """An answer carries one body byte to a 0x8000-class record and none otherwise.

    This is part of what makes an answer recognisable rather than part of its
    content: a `0xd001` answering a `0x9001` record is one byte long, and the
    same identifier answering a `0x1001` record is empty. Pairing an answer to
    a record needs both the identifier and this length.
    """
    return 1 if message_type & PAYLOAD_CLASS_BIT else 0
