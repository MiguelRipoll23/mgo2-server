# The string tables

Two generated files, both produced by `tools/extract_strings.py` from the binaries in
`.game/` and the downloaded PC client. Neither is hand-edited: re-run the tool rather
than patching a line.

| file | lines | size | what it is |
| --- | ---: | ---: | --- |
| `strings.txt` | 142 114 | 8.2 MB | every printable run of six characters or more in each binary |
| `codepaths.txt` | 472 | 38 KB | only the runs that are source paths — the ones ending in `.cc` |

## Format

Both files are tab-separated, four columns, one string per line:

```
0x1107BA28	0x0107BA28	.game/pc/system.dll	SIGNAL_NET_PLAYER_PARTS_FACECAMO :: player chara not snake !!!
```

| column | meaning |
| --- | --- |
| 1 | **virtual address** — the address a disassembly shows, `0x…` eight digits |
| 2 | file offset of the same bytes, for `xxd`/`dd` against the file itself |
| 3 | the binary, as the path given to the tool, so the two `savemgo.dll` builds cannot be confused |
| 4 | the string, with any tab in it written as `\t` |

`unmapped` in column 1 means the bytes lie outside every segment the file advertises —
forty-one lines in this run, all in file headers or inter-section padding, where no
address exists.

## What was read

| binary | strings | address range |
| --- | ---: | --- |
| `.game/ps3/MGO2.ELF` | 29 854 | `0x00010FF7`–`0x0129C177` |
| `.game/pc/system.dll` | 86 286 | `0x10001625`–`0x318DE059` |
| `.game/pc/savemgo.dll` | 12 076 | `0x1000AF48`–`0x10806DE3` |
| `.game/pc/mgo2.exe` | 1 067 | `0x004015F1`–`0x00470787` |
| `.game/downloads/pc-client/savemgo.dll` | 11 763 | `0x1000A129`–`0x107877DD` |
| `.game/downloads/pc-client/mgo2.exe` | 1 068 | `0x004015F1`–`0x00470787` |

The two PC clients are different builds: the one in `.game/pc/` carries 12 076 strings
and the one in the download 11 763, and their images differ in size by 89 088 bytes.
Both are listed so a reading can say which it came from.

## How the addresses are derived

Not assumed. For the PS3 client the tool walks the **ELF program header table** — the
file is `ELF64`, big-endian `PowerPC64`, not the 32-bit ELF its `.ELF` name suggests —
and maps each string's file offset through the `PT_LOAD` segment that contains it. For
the PE images it walks the **section table**, adds the section's `VirtualAddress` to the
image base (`0x10000000` for the two DLLs, `0x400000` for `mgo2.exe`) and adds the
offset inside the section. A string in no segment is written as `unmapped` rather than
given a plausible-looking number.

**The ELF mapping was checked against `readelf -l`.** It reports two real `PT_LOAD`
segments — file `0x0` → VA `0x10000` (size `0x117E348`) and file `0x1180000` → VA
`0x1190000` (`0x11CA00`) — and the tool derives exactly those, so a string in the first
segment reads as its file offset plus `0x10000`.

**The addresses agree with what `docs/ADDRESSES.md` already records.** That file puts
the labelled lobby menu table — `obj_4_select_others`, `icon_drebin_point`,
`icon_HSonly` — at `0xFE7084`, and the three label strings it points at appear here at
`0x00FE0D88`, `0x00FE10C0` and `0x00FE10D8`: the same `0xFE` region, with the table a
pointer list and the labels the bytes it points at.

## Re-deriving

```
python3 tools/extract_strings.py --min 6 \
    --strings docs/strings/strings.txt --codepaths docs/strings/codepaths.txt \
    .game/ps3/MGO2.ELF .game/pc/savemgo.dll .game/pc/system.dll .game/pc/mgo2.exe \
    .game/downloads/pc-client/mgo2.exe .game/downloads/pc-client/savemgo.dll
```

`--min` is the only knob: four gives roughly three times the lines and a great deal
more code noise, six is what these tables were built at.

## What they are for

The addresses are what make a string actionable: `docs/ADDRESSES.md` and
`docs/OBSERVED.md` name code addresses, and a string's address is how the two are
tied together — the rule-name symbols, the `Rule_Eng_*` family, the menu keys and the
`STRING_*` tables all appear here with the address of their own bytes. The `.cc` list
is the one that matters most for orientation: it is the compiler's own record of which
source file each part of the client came from, so a region of code can be named
(`.\online\ruleman\sr_dm.cc`) without reading a single instruction.
