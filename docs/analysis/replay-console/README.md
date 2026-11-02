# The SaveMGO replay console's scripts

Verbatim copies of the JavaScript the SaveMGO replay console loads, kept here because
`rpdt.js` is the **authority** for two wire facts this repository reads but cannot derive
from its own captures: what a `0b` settings payload contains, and how a replay's record
framing is cut. Without a copy, `docs/protocol/UDP_SETTINGS_PAYLOAD.md` cites a URL that
can change under it.

| file | bytes | sha256 (first 16) | version the page asks for | what it is |
| --- | ---: | --- | --- | --- |
| `rpdt.js` | 131750 | `b26e5961cac3f72f` | `rpdt.js?v=len59` | The replay `.dat` parser. `TYPE_SETTINGS = 0x0b`, `appearanceRecord`, `skillRecord`, the record framing and the field tables. **The load-bearing file.** |
| `app.js` | 838174 | `1aaa4f892fec5c95` | `app.js?v=ins298` | The playback engine, renderer and HUD. Consumes `rpdt.js`'s output; its `VISUALIZER_CONTRACT.md` is the data contract. |
| `three.min.js` | 669884 | `170c6789f43217c9` | `three.min.js` | Upstream Three.js (minified `build/three.min.js`, © 2010–2023, MIT). A rendering dependency, not site code. |
| `index.html` | 33298 | `af9c9651d758e952` | — | The page that references them (`rpdt.js?v=len59`, `app.js?v=ins298`, `data/index.json?v=len59`), so the set is reconstructible. |

Fetched **2026-11-02** from `https://eovsstone.com/games/mgo2-replay-console-beta/`, from
`index.html`'s own `?v=` query strings:

```
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/rpdt.js?v=len59"  -o rpdt.js
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/app.js?v=ins298"  -o app.js
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/three.min.js"     -o three.min.js
curl -sSL --compressed "https://eovsstone.com/games/mgo2-replay-console-beta/"                 -o index.html
```

The previous copy was fetched 2026-10-11 from `rpdt.js?v=ins236` / `app.js?v=ins253`.

## What the 2026-11-02 update changed

**`rpdt.js` is byte-identical to the 2026-10-11 copy** — same hash, same 131750 bytes. The
replay grammar (`TYPE_SETTINGS`, the record framing, the world-object streams, the item
pool) has not moved, so nothing in `docs/protocol/RPDT_REPLAY_RECORDS.md` needs revisiting
on its account.

**`app.js` is a new build** (+94 381 bytes, 107 diff hunks). Three of its changes bear on
this repository:

* **Placed devices, measured over 395 matches (123 traps in 25 of them).** The device
  stream's `0x65` carries the placer and a device number — object id is the placer's lobby
  slot `<< 3` (279 of 282 placements name the player in the placing action), and
  `ITEM[2]` is the device's number in the round. The `0xa5` that ends it carries the reason
  in `ITEM[4]`: **1 = went off**, **3 = destroyed** (in 26 of 42 a player is firing with his
  aim point on it), **0 = taken away with no effect** (picked up by a crawler, or cleared
  8.0–9.2 s after the owner's death). 21 traps were never removed. This is the coding the
  `WorldObjectEvent` / `WorldObjectRemovalReason` enums now state.
* **The game's hit-kind table slips nine rows from about id 99.** `113` is the **drum can**
  hit (17 of 18 victims were rolling in the can) and `107` the shield's second blow; `111`
  and `110` are labelled as C4 triggers and are probably ROLLING and C4 TRIGGER by the same
  slip. Measured over 370 matches. Not used by this server — it reads no hit records — but
  it is why the console's damage labels differ from the shipped table.
* **Lighting, sky and reload playback** (stage `.lt3` sun/ambient, sky domes, reload clips
  fitted to the action's length). Rendering-side only; nothing on the wire.

`index.html` gained a **Copy link** button and moved its `?v=` strings to `len59`/`ins298`;
`three.min.js` is unchanged in content (the previous copy differed only by line endings).

## Reading it

`rpdt.js` is minified only in places; its comments carry the measurements the wire facts
come from, so a citation can point at the comment rather than at the bytes. `app.js` is
where the *interpretations* live (which item id is the drum can, what a removal reason
means), and its comments say which of them are measured and how many matches that took.
