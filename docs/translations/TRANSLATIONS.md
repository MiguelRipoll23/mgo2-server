# MGO2 / MGS4 game translations — findings

## TL;DR

The game's English (and every other language's) text is **not** in the ELF and **not**
in the `.la2` / `.dar` / `.dlz` archives. It lives in the per-stage **GCX scenario
script**:

```
USRDIR/o/stage/<stage>/scenerio.gcx              (disc)
USRDIR/o/dl/p/stage/<stage>/scenerio.gcx         (downloaded patch, what the live server uses)
```

`o/stage/lobby/scenerio.gcx` holds the online lobby / menu text, which is where the
example string lives:

> `Select a skill.\nA description of each skill appears when you select it.`

It sits at **text ordinal 11946 (JP) / 11947 (EN)** of the lobby GCX. It is **not**
referenced by any named message definition — it is only reachable from the GCX script
section (see “Unknown / to research later”).

---

## 1. Files on disc are encrypted

Everything under `USRDIR` is **Ptsys**-encrypted. The key is
`MD5(<path from "stage/" onwards>)`, e.g. `MD5("stage/lobby")`. Downloaded
`o/dl/p/...` files reuse the key of the file they replace.

After decryption a `scenerio.gcx` is plain little-endian:

```
int32            timestamp
int32[]          packed script definitions (type<<24 | offset), until 0xFFFFFFFF
int32 x5         header: scriptSectionOffset, stringDefsOffset,
                          stringSectionOffset, scriptSectionOffset, cryptoSeed
int32[]          packed string definitions (type<<24 | offset)
byte[]           string section  (XOR'd with GcxCipher(cryptoSeed); seed = 0 here)
int32            script-section length + scripts
                 main script
```

## 2. Where the text actually is

* String definitions with **type `0x80`** are the text. They form a **flat, 0-based
  list** of NUL-terminated UTF-8 strings — `o/stage/lobby` has 22 913 of them. The
  0-based position in that list is the **string ordinal**.
* String definitions with **type `0x00`** are small scripts. They are the *message
  definitions* (see §3).
* `\xNN` sequences inside a string (e.g. `Match \xc0 mort`) are the **legacy /
  standard-definition font encoding**, not a separate language.
* `????` (literally `3F 3F 3F 3F 00`) is the game's own marker for a translation
  slot that shipped blank.

## 3. How the language is selected — the mechanism

The game does **not** pick a translation by “guessing” from the text. Every message is
declared by a type-`0x00` definition shaped like:

```
<namespace hash>  <message-name hash>  <ord0> <ord1> <ord2> <ord3> <ord4> <ord5>
```

The six numbers are **ordinals into the type-`0x80` string list**, one per language,
in a fixed order:

| slot | ord0 | ord1 | ord2 | ord3 | ord4 | ord5 |
|------|------|------|------|------|------|------|
| language | **JP** | **EN** | **FR** | **DE** | **IT** | **ES** |

The runtime resolves a message as `textList[ slot[systemLanguage] ]`, so **English is
always `ord1`**.

Verified examples (decrypted `o/stage/lobby`):

| name hash | name | ord0 JP | ord1 EN | ord2 FR | ord3 DE | ord4 IT | ord5 ES |
|---|---|---|---|---|---|---|---|
| `0x0089D8A4` | `Rule_RESCUE` | 12 レスキューミッション | 13 Rescue Mission | 14 Mission de sauvetage | 15 Rettungseinsatz | 16 Missione di salvataggio | 17 Misión de Rescate |
| `0x008386EB` | `Rule_TDM` | 5 チームデスマッチ | 6 Team Deathmatch | 7 Match à mort par équipe | 8 Team-Deathmatch | 9 Deathmatch a squadre | 10 Deathmatch de Equipo |
| `0x00244675` | `Rule_Eng_DM` | 2 | 2 Deathmatch | 4 | 2 | 2 | 2 |

### Message names are 24-bit hashes

Names are recovered with the same hash the tooling/dictionary uses:

```python
def hash(name):                       # 24-bit, rotate-left-5 then add
    h = 0
    for ch in name:
        h = ((h >> 19) | (h << 5)) & 0xFFFFFFFF
        h = (h + ord(ch)) & 0x00FFFFFF
    return h
```

`hash("Rule_DM") == 0x00F41A3D`, `hash("Skill_Eng_ASSLT_MAS") == 0x00B0BEF5`, etc.
The name table used here is `tools_Haven/dictionary.txt`. The game's own naming shows
an explicit English marker (`Rule_Eng_*`, `Skill_Eng_*`) alongside
`Rule_Short_*` (short/abbreviated form).

### How the text is stored in the string list

Messages are stored as **runs** in the same JP→EN→FR→DE→IT→ES order, and
**translations that are identical/absent are omitted** — so a run is not always six
wide:

```
2407  '頑張ってね'            JP   <- run of only 2 (EN omitted)
2408  'Bonne chance !'       FR
2395  '頑張んなさいよね'        JP   <- run of 6
2396  'Do a good job!'       EN
2397  'Fais du bon boulot !' FR
2398  'Mach deine Arbeit gut!' DE
2399  'Vedi di mettercela tutta!' IT
2400  '¡Haz un buen trabajo!' ES
```

Some banks also store the six languages in a **rotated** order (e.g.
`[DE, IT, ES, JP, EN, FR]`), so position inside the file is *not* a reliable
language indicator by itself.

---

## 4. `translations.csv` — what was exported

Single file, UTF-8 with BOM, one row per message/language slot:

```
stage,key,name,namespace,text,confidence
lobby,F41A3D,,654515,Deathmatch,verified
lobby,244675,Rule_Eng_DM,654515,Deathmatch,verified-eng-name
lobby,8386EB,Rule_TDM,654515,Team Deathmatch,verified
```

| column | meaning |
|---|---|
| `stage` | stage directory the string came from, `(patch)` = `o/dl/p/stage/...` |
| `key` | 24-bit message-name hash (hex), or `ord:N` for layout-derived rows |
| `name` | readable name when the hash is in the dictionary (else empty) |
| `namespace` | the hash of the bank the definition belongs to (`654515`, `preset_radio`, `lobby`, …) |
| `text` | the **English** string (`ord1` of the definition) |
| `confidence` | see below |

### `confidence`

| value | meaning | trust |
|---|---|---|
| `verified` | definition where exactly one of the six strings is Japanese and it is in the JP slot ⇒ slot 1 is definitively English | high |
| `verified-eng-name` | definition with no Japanese, but the name contains `_Eng_` (the game's own English marker) | high |
| `layout` | no name available. The English string is the one immediately after a Japanese string, used only when the Japanese-delimited run is 6–9 strings and all following strings are non-Japanese | **medium — verify before use** |

`layout` rows are inherited from the store layout, not from a name. This convention
was measured as correct for ~91 % of the *name-verified* messages; it still contains
a few wrong entries (e.g. `lobby ord:2719 = "Quelqu'un, venez ici !"`,
`lobby ord:3999 = "¡Se ha acabado la partida!"`). Filter them out with
`confidence = "layout"` if you only want the proven rows.

---

## 5. Unknown / to research later

1. **The `$strres` path (biggest gap).** Named definitions only reference ordinals
   ≤ ~6 400 in the lobby GCX. Roughly two thirds of the strings — including the
   example “Select a skill…” — are referenced from the *script* section via opcode
   `0x0E` + `uint16` (`$strres:N`), not by a named definition. Needs a full GCX
   script disassembly to map script → message → language offset. Whether the runtime
   literally does `$strres:N + languageIndex` (which would require contiguous 6-wide
   runs) is **unconfirmed**; the omitted-translation runs argue there is something
   more.
2. **Non-language banks.** Several namespaces (`preset_radio`, `lobby`,
   `MGO_ERROR_RES_LOBBY`, `online_opt`, `clan`, …) carry the same
   `<name> <6 numbers>` shape but the six numbers are **not** one message in six
   languages. Example `preset_radio` / `p_flag` → `JP, IT, JP, EN, IT, EN`.
   What those banks mean, and how their text is selected, is unknown. They are
   deliberately excluded from the CSV (that is where non-English text would leak in).
3. **`Rule_Short_*`.** The short-name bank is clearly a separate name family; which
   in-game widget uses it is not established.
4. **Rotated runs.** Some message runs are stored starting at a language other than
   JP (`[DE, IT, ES, JP, EN, FR]`). Cause (which bank/loader) unknown.
5. **The dictionary is incomplete.** Only 354 of ~4 986 definitions resolve to a name;
   e.g. `Rule_DM` (hash `0x00F41A3D`) has no line in `dictionary.txt` even though its
   hash is used in the file. More names would make the export far easier to use.
6. **ELF side.** Where the game reads the system language and applies it to the string
   list was not traced in `MGO2.ELF` (`docs/MGO2.ELF`). Worth finding for the server
   work: it would confirm the language enum and the exact `slot → ordinal` rule.
7. **`????` placeholder.** Confirmed as a shipped-blank translation slot; whether the
   server can/should override it is related to the expansion-pack flag work, not to
   the string store.

## 6. Tooling

All scripts live in `C:\Users\migue\Downloads\scripts`:

| script | purpose |
|---|---|
| `mgo2_stage_decrypt.py` | Ptsys-decrypt one stage (disc and `o/dl/p/` patch paths) |
| `decrypt_stages.py` | bulk decrypt every stage with progress (`--only`, `--resume`) |
| `gcx_text_extract.py` | parse a decrypted GCX, dump the raw text list |
| `gcx_disasm.py` | GCX bytecode disassembler + 24-bit name-hash dictionary loader |
| `gcx_langmap.py` | decode `<ns> <name> <6 ordinals>` message definitions |
| `export_english.py` | produce `translations.csv` |

Reproduce the CSV:

```bash
python scripts/decrypt_stages.py                # -> scripts/decrypted (or Desktop/stages/decrypted)
python scripts/export_english.py "C:/Users/migue/Desktop/stages/decrypted" \
    "C:/Users/migue/Documents/GitHub/mgo2-server/docs/translations"
```
