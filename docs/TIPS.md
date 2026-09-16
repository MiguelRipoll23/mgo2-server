# The welcome tip modals (help `2_6` and `2_13`)

Two modals with a TIPS panel appear back to back on the main menu right after the
character info (`0x4101` GetCharacterInfo) reply is parsed. The client fetches
`/jp/mgo2/help/2_6.txt` and `/jp/mgo2/help/2_13.txt` for them. The official
servers did not show them. **Both modals are armed by the feature byte at
payload offset `0x242` of that reply — clearing bits 2 and 3 turns them off,
and the server now sends `0x03` instead of `0x0f`.**

Everything below was read from this repository's `MGO2.ELF` (PPC64 big-endian,
Capstone via `tools/mgo2_disasm.py`; TOC base `0x1222a00` from the OPD at
`0x11e7358`).

## The request chain

- URL format string `%shelp/%d_%d.txt` at `0xfdd808`, next to the
  `online/helpdisp.sav` filename at `0xfdd7f0` in the save-descriptor table at
  `0x11cb420`. `helpdisp.sav` records which documents were already displayed.
- Help opener `0x981520(topic)`: validates the display state, marks it shown and
  drives the TIPS panel. Its callers pass a topic id; the fetcher builds the URL
  with the document id, so topic `6` fetches `2_6.txt` and topic `13` fetches
  `2_13.txt`.
- The parser lives in the same module; per `HELP.md` it understands only
  `<title>` and `<page-break>`, caps pages at 2048 bytes / 16 pages, and the
  whole document at 32768 bytes.

## The gates

The character info parser splits the feature byte's low nibble into four profile
flags (splitter `0xf06450`):

```
00f06450: cmpdi   r3, 0
00f06454: rlwinm  r11, r4, 0, 0x1c, 0x1c     ; isolate bit 3
00f0645c: clrlwi  r0,  r4, 0x1f              ; isolate bit 0
00f0645c: rlwinm  r9,  r4, 0, 0x1e, 0x1e     ; isolate bit 1
00f06464: rlwinm  r4,  r4, 0, 0x1d, 0x1d     ; isolate bit 2
00f06470: stb     r11, 0x4186(r3)            ; bit 3 → +0x4186
00f06474: stb     r0,  0x4184(r3)            ; bit 0 → +0x4184
00f06478: stb     r9,  0x4185(r3)            ; bit 1 → +0x4185
00f0647c: stb     r4,  0x4187(r3)            ; bit 2 → +0x4187
```

Mapping: bit 0 → `+0x4184`, bit 1 → `+0x4185`, bit 2 → `+0x4187`, bit 3 → `+0x4186`.

Two one-time gates in the main-menu update loop consume flags 2 and 3. Each runs
when the menu state machine reaches the idle state after the character info burst;
each passes its document id to the help opener:

**Gate A (`0x98e208`) — help document 13 (`2_13.txt`), armed by feature bit 2:**

```asm
0098e208: lbz     r0, 0x4187(r3)      ; feature flag 2
0098e20c: cmpwi   cr7, r0, 0
0098e210: beq     cr7, 0x98e250       ; clear → modal never opens
0098e214: bl      0x97c124            ; current profile
0098e21c: lbz     r0, 0x2c1(r3)       ; "already shown this session"
0098e224: cmpwi   cr7, r0, 0
0098e228: bne     cr7, 0x98e250       ; shown → skip
0098e22c: bl      0x482b0             ; sound
0098e23c: li      r0, 1
0098e240: stb     r0, 0x2c1(r3)       ; mark shown
0098e244: li      r3, 0xd             ; ← document 13
0098e248: bl      0x981520            ; help opener
```

**Gate B (`0x98e2b0`) — help document 6 (`2_6.txt`), armed by feature bit 3:**

```asm
0098e2b0: lbz     r0, 0x4186(r3)      ; feature flag 3
0098e2b4: cmpwi   cr7, r0, 0
0098e2b8: beq     cr7, 0x98e318       ; clear → modal never opens
...
0098e2f0: li      r3, 6               ; ← document 6
0098e2f8: bl      0x981520            ; help opener
```

Both gates re-check on every menu tick, so with the bits set the modals appear
one after the other until each one's `+0x2c0`/`+0x2c1` byte is set — the observed
"always, one after the other" behaviour. The official servers never set the bits,
so those modals were one-time *welcome* texts shown only to players whose client
had the corresponding flag from a campaign or event.

## The server side

`FeatureFlags.ExpansionByte` in
`src/GameLobbyServer/Commands/Game/Characters/CharacterInfoHandlers.cs` is the
byte written at payload offset `0x242`. It was `0x0f` (all four bits), which
armed both welcome tips alongside the intended expansion unlock. It is now
`0x03`: bits 0 and 1 still unlock every map, rule and character row (the
content mask at `0x22a` stays all-ones; see `expansion-entitlements.md`), and
bits 2 and 3 stay clear so neither modal ever opens. No other payload field
feeds the gates, so this one constant is the whole fix.

To show a tip deliberately (e.g. a welcome message), set bit 2 or bit 3 in
`ExpansionByte` and serve the matching document from the help endpoint; the
client shows it once per session, on the main menu, right after login.
