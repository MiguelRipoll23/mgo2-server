# The SaveMGO replay console's scripts

Verbatim copies of the JavaScript the SaveMGO replay console loads, kept here because
`rpdt.js` is the **authority** for two wire facts this repository reads but cannot derive
from its own captures: what a `0b` settings payload contains, and how a replay's record
framing is cut. Without a copy, `docs/protocol/UDP_SETTINGS_PAYLOAD.md` cites a URL that
can change under it.

| file | bytes | sha256 (first 16) | what it is |
| --- | --- | --- | --- |
| `rpdt.js` | 130233 | `c3526e2cbbc97101` | The replay `.dat` parser. `TYPE_SETTINGS = 0x0b`, `appearanceRecord`, `skillRecord`, the record framing and the field tables. **The load-bearing file.** |
| `app.js` | 730224 | `31c99843d31af641` | The playback engine, renderer and HUD. Consumes `rpdt.js`'s output; its `VISUALIZER_CONTRACT.md` is the data contract. |
| `three.min.js` | 669884 | `170c6789f43217c9` | Upstream Three.js (minified `build/three.min.js`, © 2010–2023, MIT). A rendering dependency, not site code. |
| `index.html` | 33095 | `888b7579f1c7b07a` | The page that references them, so the set is reconstructible. |

Fetched 2026-10-11 from `https://eovsstone.com/games/mgo2-replay-console-beta/`, from
`index.html`'s own `?v=` query strings:

```
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/rpdt.js?v=ins236" -o rpdt.js
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/app.js?v=ins253"  -o app.js
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/three.min.js"      -o three.min.js
```

`ins236` / `ins253` are the site's own asset build counters, not semantic versions. A
higher number means the asset was republished and **every offset and table below must be
re-checked against the new copy** before it is relied on again. `rpdt.js`'s header carries
its own revision, `revision 2026-08-21`, which is the line to quote when an offset is
challenged.

## What was taken from it

| finding | where it is written up |
| --- | --- |
| The `0b` settings payload: rotation triples, the 23-byte appearance tail, the skill alignment | `docs/protocol/UDP_SETTINGS_PAYLOAD.md` |
| Its port, byte for byte | `tools/udp_settings_semantics.py` |
| Its printout over a capture | `tools/pcap_roster_acks.py` ("settings payloads (0x0b)") |

Nothing in this repository **loads** these files. They are reference, the same way
`docs/analysis/static_fn/*.c` is: something to read when a byte offset is in question.

## A note on line length

`rpdt.js` is 2608 lines and `app.js` 13569. They are vendored third-party artifacts,
not source this repository compiles, so the 400-line rule does not apply to them — the same
exemption the migrations tool's generated files get. Do not reformat them: they must stay
byte-identical to what the site serves, or the sha256 above stops meaning anything.
