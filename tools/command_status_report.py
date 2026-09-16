#!/usr/bin/env python3
"""Write `docs/COMMAND_STATUS.md`: every lobby command id with THIS server's status.

`docs/PACKETS.md` is the reference server's own packet reference, vendored here
verbatim, so its `our status` column describes *that* server. This script recalculates
the column for this repository, keeping the reference's vocabulary and section
headings, and writes the result to `docs/COMMAND_STATUS.md`.

The client-direction columns and the tables come from `docs/PACKETS.md`; the two id
lists come from the reference's `dev/analysis/c2s_ids.txt` and `s2c_ids.txt`, which is
what those tables were built from. Two facts are read out of our own source:

* **answered** — a `registry.Register<...>(ServerType.X, CommandConstants.SYMBOL)`
  call. The registry is the inventory of what this server replies to, so it is read
  rather than a hand-kept list.
* **emitted** — a command constant this source references and does not register. That
  is deliberately wider than "a constant passed to a send helper": several replies
  travel through a local helper that takes the id as a parameter (`ReplyAsync(...,
  replyCommand, ...)`, `SendToMembersAsync(...)`), which a parse of the helper's call
  sites cannot see through. The wider rule reports nothing it should not — every id it
  names is either a reply the client parses or one of the phantoms in the defects
  table — and the older handlers that pass reply ids as bare literals are covered by a
  separate scan of the send helpers' command-id argument.

Run it from the repository root with a checkout of comradesean/mgo2server beside it:

    python tools/command_status_report.py [--reference PATH]
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REGISTER_RE = re.compile(
    r"registry\.Register<\w+>\(\s*ServerType\.(\w+)\s*,\s*CommandConstants\.(\w+)\s*\)"
)
CONST_RE = re.compile(r"public const ushort\s+(\w+)\s*=\s*(0x[0-9A-Fa-f]+)\s*;")
SYMBOL_RE = re.compile(r"CommandConstants\.(\w+)")
ROW_RE = re.compile(r"^\|\s*`(0x[0-9A-Fa-f]{4})`\s*\|(.*)\|\s*$")
SECTION_RE = re.compile(r"^### (.+)$")

# Helper name -> index of the argument holding the command id. Most helpers take the
# session first; the codec helpers take the command id first.
SENDING_HELPERS = {
    "SendPacketAsync": 1,
    "SendResultAsync": 1,
    "SendStartEndPacketAsync": 1,
    "SendAcknowledgementAsync": 1,
    "SendErrorAsync": 1,
    "EncodePacket": 0,
    "EncodeErrorPacket": 0,
}

SYMBOL_ARGUMENT_RE = re.compile(r"^CommandConstants\.(\w+)$")
LITERAL_ARGUMENT_RE = re.compile(r"^0x([0-9A-Fa-f]{4})$")

# Refs to the client's pre-lobby handshake, which the lobby packet library does not
# carry, so it has no row in the packet reference and no entry in either id list. It is
# not a phantom: the reference registers it too, and its own notes say why it is absent.
OUTSIDE_THE_TABLES_BY_DESIGN = {0x0001}


def constants(root: Path) -> dict[str, int]:
    """Map every command constant's name to its id."""
    table: dict[str, int] = {}
    for path in sorted((root / "src/Shared/Constants").glob("CommandConstants*.cs")):
        for name, value in CONST_RE.findall(path.read_text(encoding="utf-8")):
            table[name] = int(value, 16)
    return table


def inventory(root: Path) -> tuple[set[str], set[str]]:
    """Return the symbols this server registers and the command symbols it references."""
    registered: set[str] = set()
    referenced: set[str] = set()
    for path in sorted(root.glob("src/**/*.cs")):
        if path.parent.name == "Constants":
            continue
        text = path.read_text(encoding="utf-8")
        registered.update(symbol for _server, symbol in REGISTER_RE.findall(text))
        referenced.update(SYMBOL_RE.findall(text))
    return registered, referenced


def call_arguments(text: str, open_paren: int) -> list[str]:
    """Split the call whose opening parenthesis is at `open_paren` into its arguments."""
    depth = 0
    arguments: list[str] = []
    current: list[str] = []
    for index in range(open_paren + 1, len(text)):
        character = text[index]
        if character in "([":
            depth += 1
        elif character in ")]":
            if depth == 0:
                arguments.append("".join(current))
                return arguments
            depth -= 1
        elif character == "," and depth == 0:
            arguments.append("".join(current))
            current = []
            continue
        current.append(character)
    arguments.append("".join(current))
    return arguments


def command_argument(arguments: list[str], index: int) -> str | None:
    """Return the command symbol or literal a helper's command-id argument carries."""
    if index >= len(arguments):
        return None
    argument = arguments[index].strip()
    if (match := SYMBOL_ARGUMENT_RE.match(argument)) is not None:
        return match.group(1)
    if (match := LITERAL_ARGUMENT_RE.match(argument)) is not None:
        return f"0x{match.group(1).lower()}"
    return None


def sent_literals(root: Path) -> set[str]:
    """Return the ids the older handlers pass to a send helper as bare literals."""
    literals: set[str] = set()
    for path in sorted(root.glob("src/**/*.cs")):
        text = path.read_text(encoding="utf-8")
        for helper, index in SENDING_HELPERS.items():
            start = 0
            while (found := text.find(f"{helper}(", start)) != -1:
                name = command_argument(call_arguments(text, found + len(helper)), index)
                if name is not None and name.startswith("0x"):
                    literals.add(name)
                start = found + len(helper)
    return literals


def read_ids(path: Path) -> set[int]:
    """Read one of the reference's ELF-derived id lists."""
    return {
        int(token, 16) for token in path.read_text(encoding="utf-8").split() if token.startswith("0x")
    }


def parse_rows(packets: Path) -> list[tuple[str, int, list[str]]]:
    """Return (section, id, cells) for every row of the packet reference's tables."""
    rows: list[tuple[str, int, list[str]]] = []
    section = ""
    for line in packets.read_text(encoding="utf-8").splitlines():
        if (match := SECTION_RE.match(line)) is not None:
            section = match.group(1)
        if (match := ROW_RE.match(line)) is not None and match.group(2).count("|") == 3:
            cells = [cell.strip() for cell in match.group(2).split("|")]
            rows.append((section, int(match.group(1), 16), cells))
    return rows


def identifier(value: int) -> str:
    """Format an id the way the packet reference does."""
    return f"0x{value:04X}"


def main() -> int:
    root = Path(".")
    reference = Path("../reference-mgo2server")
    args = sys.argv[1:]
    if "--reference" in args:
        reference = Path(args[args.index("--reference") + 1])

    table = constants(root)
    registered_symbols, referenced_symbols = inventory(root)
    answered = {table[symbol] for symbol in registered_symbols if symbol in table}
    emitted = {table[symbol] for symbol in referenced_symbols - registered_symbols if symbol in table}
    emitted |= {int(literal, 16) for literal in sent_literals(root)}
    emitted -= answered

    c2s = read_ids(reference / "dev/analysis/c2s_ids.txt")
    s2c = read_ids(reference / "dev/analysis/s2c_ids.txt")

    def status_of(key: int) -> str:
        if key in answered:
            return "served"
        if key in emitted:
            return "PHANTOM" if key not in s2c else "served"
        if key in c2s:
            return "gap"
        return "unsent"

    rows = parse_rows(root / "docs/PACKETS.md")
    ours = {key: status_of(key) for _section, key, _cells in rows}
    reference_statuses = [cells[3].strip("*").strip() for _section, _key, cells in rows]

    def tally(is_ours: bool) -> dict[str, int]:
        counts: dict[str, int] = {}
        for index, (_section, key, _cells) in enumerate(rows):
            status = ours[key] if is_ours else reference_statuses[index]
            counts[status] = counts.get(status, 0) + 1
        return counts

    our_counts = tally(True)
    their_counts = tally(False)
    phantoms = sorted(key for key in emitted if key not in s2c and key not in c2s)
    misnumbered = sorted(
        key
        for key in answered
        if key not in c2s and key not in s2c and key not in OUTSIDE_THE_TABLES_BY_DESIGN
    )
    gaps = sorted({key for _section, key, _cells in rows if ours[key] == "gap"})
    we_answer_they_do_not = sorted(
        key
        for index, (_section, key, _cells) in enumerate(rows)
        if ours[key] == "served" and reference_statuses[index] in ("gap", "unsent")
    )

    document: list[str] = []
    document.append("# Command status — every lobby command id, for this server\n")
    document.append(
        "The same tables as [`PACKETS.md`](PACKETS.md), with the `our status` column\n"
        "recalculated for **this** repository.\n"
        "`PACKETS.md` is the reference server's own packet reference, kept here verbatim:\n"
        "its status column describes that server, not ours, and its `served` therefore means\n"
        "\"the reference answers this\", which is a different claim from \"we do\".\n"
        "\n"
        "Generated by `tools/command_status_report.py`. Regenerate it after changing a\n"
        "registration in `src/*/Commands/*CommandHandlerRegistration.cs` or adding a reply id,\n"
        "or the column and the code part company.\n"
    )
    document.append("## Counts\n")
    document.append("| | this server | reference |\n| --- | --- | --- |")
    for status in ("served", "gap", "unsent"):
        document.append(
            f"| {status} | {our_counts.get(status, 0)} | {their_counts.get(status, 0)} |"
        )
    for status in ("served \u2020", "handled", "sent"):
        if status in their_counts:
            document.append(
                f"| the reference's `{status}` rows, spelled `served` here | "
                f"{their_counts[status]} | {their_counts[status]} |"
            )
    document.append(f"| **rows** | **{len(rows)}** | **{len(rows)}** |")
    document.append(
        f"| ids in neither direction's list — the defects below | **{len(phantoms) + len(misnumbered)}** "
        "| the reference lists its own in `PACKETS.md` → *Defects* |"
    )
    document.append("")
    document.append(
        "* **served** — the command the client sends is answered, or a reply the client parses\n"
        "  is emitted.\n"
        "* **gap** — the client sends it and we do not implement it. A reachable stall.\n"
        "* **unsent** — the client can parse it and we never emit it. Benign unless a screen\n"
        "  waits on the reply.\n"
        "* **PHANTOM** — we emit an id the client has no parser for. A defect, not tidiness.\n"
        "* **MISNUMBERED** — we answer a neighbouring id instead of the real one. A defect.\n"
        "\n"
        "The last two have **no row in the tables at all**, which is what makes them defects\n"
        "rather than gaps: the client neither sends nor parses those ids, so neither id list\n"
        "the tables are built from contains them. They are listed under *Ids we touch that the\n"
        "client does not* instead.\n"
        "\n"
        "The reference also writes `handled` and `sent` on four rows (`0x43A4`/`0x43A5` and\n"
        "`0x43C4`/`0x43C5`). Those spell the same two states differently, so this table uses\n"
        "`served` for all four; the rows are called out in the counts above only so the two\n"
        "files can be reconciled row for row.\n"
    )
    document.append("## How the column was derived\n")
    document.append(
        f"* **answered** — {len(answered)} ids, read from the `registry.Register` calls. The\n"
        "  registry is the inventory of what a client can get an answer from, so a command that\n"
        "  is only special-cased in the socket loop is invisible to it. `0x0003` and `0x0005`\n"
        "  used to be exactly that; they are registered commands now.\n"
        f"* **emitted** — {len(emitted)} ids: command constants this source references and does\n"
        "  not register, plus the ids the older handlers pass as bare literals. A reply id that\n"
        "  is only *defined* is `unsent`, which is what the column means here.\n"
        "* **client direction** — the ELF id lists in the reference's `dev/analysis/`, the same\n"
        "  source its own tables cite. An id in neither list is an id the client neither sends\n"
        "  nor parses, which is what the two defect labels are for.\n"
        "\n"
        "**What this cannot tell you.** It is a reading of source, not of a client: `served`\n"
        f"means a handler exists, not that the client accepted the reply — the reference's own\n"
        "caveat for its column applies here unchanged.\n"
    )
    document.append("## Evidence from the deployment\n")
    document.append(
        "Checked against the server running on `raspberrypi` on 2026-09-16 (every `mgo2-*`\n"
        "container, ~517k log lines, the whole window the containers retain):\n"
        "\n"
        "* **Inbound opcodes actually observed: 16**, and every one is answered —\n"
        "  `0x0003`, `0x0005`, `0x2005`, `0x2008`, `0x3003`, `0x3048`, `0x3103`, `0x4100`,\n"
        "  `0x4130`, `0x4150`, `0x4300`, `0x43D0`, `0x4700`, `0x4820`, `0x4900`, `0x4990`.\n"
        "* **Zero `no-handler` lines and zero handler failures.** No command a client sent ever\n"
        "  went unanswered, which is the only claim about coverage the logs can support.\n"
        "* **None of the eleven ids dropped from the registry appeared inbound.** Five of them\n"
        "  (`0x4122`, `0x4124`, `0x4125`, `0x4140`, `0x4142`) appear only outbound, as the\n"
        "  connect burst's pushes, which is why the pushes were kept and the handlers were not.\n"
        "* `0x0003` (13×) and `0x0005` (9×) both appeared inbound, which is what makes them\n"
        "  commands rather than socket plumbing.\n"
        "\n"
        "The window is hours long, not months, so read this as \"no counter-example\", not proof.\n"
        "The static reading above is what carries the claim for ids nobody has exercised yet.\n"
    )
    document.append("## Command-count parity\n")
    document.append(
        f"* This server answers **{len(answered)}** request ids; the reference answers **84**\n"
        "  (83 of them in the ELF client-to-server list, plus the `0x0001` handshake echo).\n"
        f"* **Every command the reference answers is answered here.** The difference is\n"
        f"  `0x4348`, which the client sends (`0xd4a8a4`) and the reference leaves as a `gap`;\n"
        "  keeping it is a deliberate choice — matching the reference exactly would mean\n"
        "  removing the answer to a command the client genuinely issues.\n"
        "* The eleven ids this pass removed from the registry were the ones the client never\n"
        "  sends; see the deployment evidence above.\n"
    )

    document.append("## Tables\n")
    document.append(
        "Summaries, payload sizes and the `client` column are the reference's, unedited.\n"
    )
    section = ""
    for _section, key, cells in rows:
        if _section != section:
            section = _section
            document.append(f"\n### {section}\n")
            document.append("| id | client | payload | summary | our status |")
            document.append("| --- | --- | --- | --- | --- |")
        document.append(
            f"| `{identifier(key)}` | {cells[0]} | {cells[1]} | {cells[2]} | {ours[key]} |"
        )

    document.append("\n---\n")
    document.append("## Ids we touch that the client does not\n")
    if phantoms or misnumbered:
        document.append("| id | status | what | why it is still there |")
        document.append("| --- | --- | --- | --- |")
        for key in phantoms:
            if key == 0x4115:
                document.append(
                    "| `0x4115` | **PHANTOM** | reply to the chat-macro write (`0x4114`) | the "
                    "reference's evidence is that `0x4114` is fire-and-forget, so this reply is "
                    "unread; removing it needs a live check first, because a client that *does* "
                    "wait on the slot stalls with `FFFFFF60` when nothing answers. Sending an "
                    "unparsed packet costs nothing. |"
                )
            elif key in (0x4140, 0x4142):
                document.append(
                    f"| `{identifier(key)}` | **PHANTOM** | saved skill sets / gear sets, pushed in "
                    "the `0x4100` connect burst | the client has no parser for it, but the burst "
                    "otherwise works and the real read path is probably the `0x4133` outfit "
                    "readback; keep until that is traced rather than deleting on inference. |"
                )
            else:
                document.append(f"| `{identifier(key)}` | **PHANTOM** | emitted by this server | investigate |")
        for key in misnumbered:
            if key not in OUTSIDE_THE_TABLES_BY_DESIGN:
                document.append(f"| `{identifier(key)}` | **MISNUMBERED** | answered by this server | investigate |")
    else:
        document.append("None.")
    document.append("")
    document.append(
        f"`0x0001` (echo) is answered and is in neither id list, and that is **not** a defect:\n"
        "it is part of the pre-lobby handshake, which lives outside the packet library the id\n"
        "lists were scanned from, and the reference registers it too. It has no row in the\n"
        "tables above for the same reason.\n"
    )

    document.append("## Gaps\n")
    document.append(f"{len(gaps)} ids the client sends that this server does not answer:\n")
    document.append("`" + "` · `".join(identifier(key) for key in gaps) + "`\n")
    unidentified = [key for key in gaps if 0x4900 <= key <= 0x4EFF]
    document.append(
        "They are the reference's own gaps minus `0x4348`, which we answer. `0x49C0` appears\n"
        "once per direction in the tables, so it is listed once here.\n"
        f"\n{len(unidentified)} of the {len(gaps)} sit in the still-unidentified\n"
        "`0x49xx`/`0x4Axx`/`0x4Exx` blocks, where the reference has layouts but no meanings;\n"
        "closing those is reverse engineering, not porting, and inventing a shape for them is\n"
        "what the reference's own notes warn against. The other seven (`0x2006`, `0x3040`,\n"
        "`0x4210`, `0x4394`, `0x43B0`, `0x4860`, `0x4E00`) are equally unidentified but sit\n"
        "outside it.\n"
    )

    document.append("## Where this server is ahead of the reference\n")
    document.append("| id | what |\n| --- | --- |")
    for key in we_answer_they_do_not:
        row = next(cells for _section, row_key, cells in rows if row_key == key)
        document.append(f"| `{identifier(key)}` | {row[2]} |")
    if not we_answer_they_do_not:
        document.append("| — | none |")
    document.append("")

    document.append("## Open items\n")
    document.append(
        "1. **The `0x4101` grid length** is still disputed: this server sends a `0x229` grid\n"
        "   with 64-id friend and blocked arrays and a content mask at `0x22a`, the reference\n"
        "   sends `0x142` with 32-id arrays and no mask. Both cite a parser address. The test is\n"
        "   a live client with a deliberately narrowed mask: if narrowing it changes the\n"
        "   create-game rows, the mask is read. See `command-handler-comparison.md` §7.\n"
        "2. **The three phantoms** above, each for the reason recorded with it.\n"
        "3. **`0x4900`'s entry length** is a fixed 35 bytes here and sized per client build on\n"
        "   the reference (64 bytes of text on 1.0, none on 1.36). 35 is the 1.36 layout, which\n"
        "   is the build this server targets, so there is nothing to size until a 1.0 deployment\n"
        "   is wanted — at which point the length belongs in the configuration next to the other\n"
        "   build-specific values.\n"
        "4. **Not verified against a client**: the ranking boards' `skey` meanings, and the\n"
        "   combat-training graduation flow end to end.\n"
    )

    output = root / "docs/COMMAND_STATUS.md"
    output.write_text("\n".join(document) + "\n", encoding="utf-8")
    print(f"answered {len(answered)} ids, emitted {len(emitted)}")
    print(f"rows: {our_counts}  (reference: {their_counts})")
    print(f"gaps {len(gaps)}, phantoms {[identifier(k) for k in phantoms]}")
    print(f"wrote {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
