#!/usr/bin/env python3
"""Compare the commands THIS server handles with the ones the reference handles.

Inputs:
  * `--reference PATH` — a checkout of comradesean/mgo2server.
  * `--local PATH` — this repository (default: the parent of tools/).

Both inventories resolve the per-project constant tables, then the comparison is
reported against the ELF-derived client-to-server and server-to-client id lists in
the reference's `dev/analysis/`, which is what the PACKETS.md tables are built from.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

CONST_RE = re.compile(r"public const ushort\s+(\w+)\s*=\s*(0x[0-9A-Fa-f]+|\d+)\s*;")
REGISTER_RE = re.compile(
    r"registry\.Register<(\w+)>\(\s*ServerType\.(\w+)\s*,\s*CommandConstants\.(\w+)\s*\)"
)
JAVA_CONST_RE = re.compile(r"\b([A-Z][A-Z0-9_]*)\s*=\s*(0x[0-9A-Fa-f]+)\b")
JAVA_PUT_RE = re.compile(r"handlers\.put\(\s*([A-Za-z0-9_]+(?:\[\d+\])?|0x[0-9A-Fa-f]+)\s*,")
JAVA_ARRAY_HEAD_RE = re.compile(r"([A-Z][A-Z0-9_]*)\s*=\s*\{")
JAVA_TUPLE_RE = re.compile(r"\{([^{}]*)\}")
JAVA_LOOP_RE = re.compile(r"for \(var (\w+) : ([A-Z][A-Z0-9_]*)\)")
JAVA_LITERAL_RE = re.compile(r"0x[0-9A-Fa-f]+")


def brace_body(text: str, open_index: int) -> str:
    """Return the text between the brace at `open_index` and its matching brace."""
    depth = 0
    for index in range(open_index, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return text[open_index + 1 : index]
    return ""


def array_heads(text: str) -> dict[str, list[int]]:
    """Map each tuple-array name to the first id of every tuple it holds."""
    arrays: dict[str, list[int]] = {}
    for match in JAVA_ARRAY_HEAD_RE.finditer(text):
        body = brace_body(text, match.end() - 1)
        heads = [int(tuple_text.split(",")[0].strip(), 0) for tuple_text in JAVA_TUPLE_RE.findall(body)]
        if heads:
            arrays[match.group(1)] = heads
    return arrays


def local_opcodes(root: Path) -> dict[int, str]:
    constants: dict[str, int] = {}
    for path in sorted((root / "src/Shared/Constants").glob("CommandConstants*.cs")):
        for name, value in CONST_RE.findall(path.read_text(encoding="utf-8")):
            constants[name] = int(value, 0)
    handled: dict[int, str] = {}
    for path in sorted(root.glob("src/*/Commands/*Registration.cs")):
        for handler, _server, symbol in REGISTER_RE.findall(path.read_text(encoding="utf-8")):
            handled[constants[symbol]] = handler
    return handled


def java_opcodes(root: Path) -> dict[int, str]:
    handled: dict[int, str] = {}
    controller = root / "src/main/java/mgo2server/game/controller"
    for path in sorted(controller.glob("*.java")):
        text = path.read_text(encoding="utf-8")
        constants = {name: int(value, 16) for name, value in JAVA_CONST_RE.findall(text)}
        arrays = array_heads(text)
        loop_vars = {var: name for var, name in JAVA_LOOP_RE.findall(text)}
        for symbol in JAVA_PUT_RE.findall(text):
            if symbol in constants:
                handled[constants[symbol]] = path.stem
            elif symbol.startswith("0x"):
                handled[int(symbol, 16)] = path.stem
            else:
                # A grouped loop (`handlers.put(pair[0], ...)`) registers the head of each tuple.
                variable = symbol.split("[")[0]
                for head in arrays.get(loop_vars.get(variable, ""), []):
                    handled[head] = path.stem
    return handled


def read_ids(path: Path) -> set[int]:
    ids: set[int] = set()
    for token in path.read_text(encoding="utf-8").split():
        if token.startswith("0x"):
            ids.add(int(token, 16))
    return ids


def main() -> int:
    local_root = Path(".")
    reference_root = Path("../reference-mgo2server")
    args = sys.argv[1:]
    if "--local" in args:
        local_root = Path(args[args.index("--local") + 1])
    if "--reference" in args:
        reference_root = Path(args[args.index("--reference") + 1])

    ours = local_opcodes(local_root)
    theirs = java_opcodes(reference_root)
    c2s = read_ids(reference_root / "dev/analysis/c2s_ids.txt")
    s2c = read_ids(reference_root / "dev/analysis/s2c_ids.txt")

    def describe(key: int) -> str:
        if key in c2s and key in s2c:
            return "sends+parses"
        if key in c2s:
            return "sends"
        if key in s2c:
            return "parses"
        return "NOT IN ID SPACE"

    print("## Reference handles, this server does not")
    missing = sorted(set(theirs) - set(ours))
    for key in missing:
        print(f"  0x{key:04X} {describe(key)} {theirs[key]}")
    print(f"  COUNT {len(missing)}")

    print("\n## This server handles, the reference does not")
    extra = sorted(set(ours) - set(theirs))
    for key in extra:
        print(f"  0x{key:04X} {describe(key)} {ours[key]}")
    print(f"  COUNT {len(extra)}")

    def counts(handled: dict[int, str]) -> tuple[int, int, int]:
        in_c2s = {k for k in handled if k in c2s}
        return len(handled), len(in_c2s), len(in_c2s & s2c)

    print("\n## Counts (registered / in c2s / in c2s+s2c)")
    print(f"  reference {counts(theirs)}")
    print(f"  this one  {counts(ours)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
