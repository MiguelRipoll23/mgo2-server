# Actor pose and action

The two tick records that say what an actor looks like: the position record's
head and pose bytes, and the state record's action byte. Both tables are measured
over the twenty captures in `.pcapngs/actions/`, one capture per action, decoded
with `tools/pcap_action_map.py` (state, position, weapon and damage per slot,
with consecutive runs) and `tools/pcap_bot_combat.py`. The codecs are
`ActorPoseUtils`, `ActorAnimation` and `ActorActionUtils`; the bot writers are
`BotPositionBodiesUtils` and `BotStateBodiesUtils`.

## Position record, head byte

The position record is class `0x02`. A living body is sixteen bytes and starts
with the stance; a body on the floor is thirty-two and carries a four-byte down
header, a counter at `+4`, and its coordinates as `f32` instead.

| Head | Stance | Meaning |
| ---- | ------ | ------- |
| `01` | `Lying` | on the floor with no power of its own: a sleep, a stun's first beat |
| `20` | `Standing`, inactive | a spawn placement, before the body is in play |
| `21` | `Standing` | on its feet and still |
| `41` | `Walking` | on its feet and moving |
| `61` | `Crouching` | squatting |
| `81` | `Prone` | flat, moving or aiming; the forward roll |
| `A1` | `Crawling` | dragging itself along, or the cardboard box |
| `C1` | `KnockedDown` | the hit reaction, the short stun, a back-step or side flip |
| `E1` | `Recovering` | getting back up |

The head is `stance << 5` with bit zero set on a body in play. That is why a
spawn is `20`: it is `Standing` before the bit is set.

**The head does not say whether a blow landed.** `backward-step-facing-forward`
and `left-and-right-side-flip` write `C1 01` for one beat and then `C1 81` for
about 1.4 s on a body that no damage record names, which is an evasion; the hit
reaction writes the same `C1 81` for six or seven beats, about 200 ms. The
damage record is the record that decides, and the stance alone is not.

## Position record, pose byte

| Bit | Meaning |
| --- | ------- |
| `0x80` | armed |
| `0x40`–`0x02` | the upper-body animation, `ActorAnimation` |
| `0x01` | acting |

A body at ease writes `00`; an armed, acting body writes `81`. The animation is
its own field: `A1 A8` is the crawl animation `0x28` with the armed bit, not a
separate box id, which is why the box has no animation of its own.

Measured pairs: standing `21 00`, walking `41 81`, crouching `61 00`, prone
`81 02`, crawling `A1 28`, boxed crawling `A1 A8`, knocked down `C1 81`,
recovering `E1 00`, lying `01 1C`.

### Animation values

| Animation | Pose bytes | Drawn for | Capture |
| --------- | ---------- | --------- | ------- |
| `00` | `21 00`, `41 81`, `81 81`, `C1 81` | nothing; at ease, walking, the roll's own nine beats, a knockdown | every capture |
| `02` | `41 02`, `81 02`, `A1 02`, `21 02` | aiming while walking, prone or crawling | `shooting-and-reloading`, `switching-standing-crouching-prone-while-aiming-and-shooting` |
| `03` | `81 03` | the first landing beat of a ground throw | `stun-by-throwing-to-ground` t+54.62 |
| `04` | `01 04` | the lying beat behind it | same t+55.07; `cqc`, `stun-by-hitting-and-kicking` |
| `12` | `21 12`, `01 12` | changing weapon | `switching-between-weapons` t+40.42, `hiding-and-moving-wearing-box` |
| `18` | `41 18` | a melee blow with a weapon | `hitting-with-weapon-short-and-long-attack`, `stun-by-hitting-and-kicking` |
| `19` | `41 19` | its follow-through | `stun-by-hitting-and-kicking` |
| `1A` | `C1 1A`, `E1 1A`, `81 1A` | the short stun and the climb out of it | `stun-by-knife` (`E1 1A` 416 records), `cqc` |
| `1B` | `01 1B`, `61 1B`, `41 1B` | a CQC throw to the ground: `01 1B` lies 13.8 s before `61 1B` gets the body up | `stun-by-throwing-to-ground` t+60.07 to 73.92; `cqc` 1 277 lying |
| `1C` | `01 1C` | the sleep device's lying body | `sleep-by-sgmines` |
| `1E` | `01 1E` | a knife blow's lying animation | `stun-by-knife` |
| `1F` | `21 1F`, `41 1F` | a punch or kick | `stun-by-hitting-and-kicking` (239 standing, 306 walking) |
| `28` | `A1 28`, `81 28`, `A1 A8` | a crawl, and the armed crawl of the box | `hiding-and-moving-wearing-box` |
| `30` | `41 30`, `A1 30`, `E1 30` | handing a magazine to a teammate | `magazine-to-teammate` t+27.81, 35.81, 36.81 |
| `33` | `21 33` | the thrower's CQC ground-throw wind-up | `stun-by-throwing-to-ground` from t+54.02 and 58.67 |
| `35` | `01 35`, `61 35`, `E1 35` | the choke in a strangle | `stun-by-cqc-holding-strangling` |
| `36` | `01 36`, `21 36`, `61 36` | the hold behind it | same capture |
| `37` | `41 37` | the grab that opens a CQC hold | same capture, 107 records; `cqc` 159 |
| `3A` | `41 3A`, `01 3A`, `61 3A` | the throw out of a hold | `cqc` (83 walking, 70 lying, 36 crouching) |

Values `06`, `15`, `16`, `22` and `2B` appear in the CQC and hit captures but no
single action owns them; they stay unnamed rather than guessed at, and decoding
keeps the raw byte.

## Weapon pair flags

The five-byte pair is `[flag, weapon, 0, 0, ammo]`. The first byte is normally
`00`; `hiding-and-moving-wearing-box` writes `09 14 00 00 00` while the box is
worn and `00 14 00 00 0F` after it is put away. The flag is
`WeaponPairUtils.CardboardBoxFlag`; the box's item id is `176` (`C BOX A`).
There is no box action and no box animation: the armed crawl `A1 A8` is the
ordinary crawl animation `0x28` with the armed bit, and putting the box away is
the flag returning to `00`.

The other captures that put their action in a record rather than a pose — chat
macros, weapon switches, magazine handover, rolls/flips and device use — are
indexed in [ACTION_CAPTURE_INDEX.md](ACTION_CAPTURE_INDEX.md).

The forward roll is `81 81` for nine records into `21 01` and then `41 81`
(`forward-roll-and-also-forward-roll-to-crouching` t+26.41 to 27.12). A roll
that finishes crouched writes `A1 02` then `61 00` (t+26.08).

### Death and knock-out down body

A body on the floor is thirty-two bytes (`BotActorRecordUtils.CorpsePositionBodyLength`).
The header is four bytes, the counter is at `+4`, the coordinates are `f32` at
`+8` (`z`), `+12` (height) and `+16` (`x`), the heading is at `+20`, and the
living body's four words close it.

Measured on `working-server-log.pcapng`, the only large capture here that writes
the down body:

| Header | Vitals | Meaning | Samples |
| ------ | ------ | ------- | ------- |
| `22 1A 0X 0B` | `0` | a death, with the inactive standing frame | slot 20 t+20.96, slot 10 t+58.07, slot 9 t+326.43 |
| `21 1A 0X 0B` | `0` | a death, with the active standing frame | slot 2 t+12.37, slot 15 t+49.96, slot 1 t+60.25 |
| `A1 18 0X 0B` | 81–196 | a living knock-out, crawling | slot 22 t+18.21, slot 16 t+31.98, slot 3 t+54.85 |

The second byte is the animation: `1A` is the stun (`ActorAnimation.Stun`) and
`18` the melee blow (`ActorAnimation.MeleeBlow`). The third byte varies `00`–`04`.
The vitals record reads `0` within 0.01–0.24 s of the first `22`/`21 1A` body, so
that is the vitals-zero death pose; `A1 18` is the shape the knife and CQC
knock-outs use while health is intact.

`BotPositionBodiesUtils.BuildDown` writes `22 1A 00 0B` for a bot whose health
has reached zero, and the counter at `+4` steps `1, 3, 5 …` two a down. A
player's death body is the client's own record: this host relays it unchanged
and does not decode it into `PlayerPosition`, which is the sixteen-byte living
body.

## State record, action byte

The state record is class `0x01`, three bytes while standing and nine while
acting. The first byte is `0x40 | action` on an acting body. The second is the
target slot or `0xFF`; the third is one on the firing beats; the last six are
the aim point at a tenth of a unit.

| Action | First byte | Meaning |
| ------ | ---------- | ------- |
| `Idle` | `00` | the three-byte `00 FF 00` |
| `Aim` | `02`, `42` | aiming: `02 FF 00`, then the nine-byte form with its aim point |
| `TriggerHeld` | `04`, `44` | the trigger held, on every automatic-fire beat |
| `Fire` | `06`, `46` | the round that opens a burst |
| `FireBeat` | `07`, `47` | the rounds behind it, alternating with `Fire` |
| `Recover` | `08`, `48` | the three-byte beat as the body leaves an attack |
| `PlaceDevice` | `0A`, `4A` | a mine or C4 set down: one aim point held |
| `Reload` | `0B`, `4B` | a reload's run of beats |
| `ThrowReady` | `14`, `94`, `D4` | a grenade readied, its aim point tracking |
| `ThrowRelease` | `16`, `96`, `D6` | the grenade leaving the hand, then the three-byte tail |
| `CqcHold` | `17`, `57` | a CQC grab holding an enemy |
| `CqcStrike` | `18`, `58` | a blow landed in the hold |
| `CqcFinish` | `19`, `59` | a finish in the hold |
| `CqcThrow` | `1A`, `5A` | a throw out of the hold |
| `Spawn` | `25` | the round-opening form, and a body coming back to its feet |

The throw family also sets `0x80`, which is above the action's six bits and not
the acting bit (`ActorActionUtils.ThrowBit`): `Of(0x94)` is `ThrowReady` just as
`Of(0x14)` is, and `Of(0xD6)` is `ThrowRelease`.

The capture evidence, in timeline order:

* `shooting-and-reloading.pcapng` writes `02 FF 00` at t+17.31, the nine-byte
  `02` at t+17.34, `04` at t+17.41, then alternating `06`/`07` while the
  weapon's magazine falls `0F → 0E → 0D → 0C → 0B → 0A`.
* `hitting-with-weapon-short-and-long-attack.pcapng` closes each melee run on a
  single `48 FF 00` before `00 FF 00`.
* `switching-standing-crouching-prone.pcapng` writes `25 FF 00` at the round's
  opening and again when a body comes back up.
* `damage-and-kill0-with-knife.pcapng` writes the `57`/`58`/`5A` hold family;
  `cqc.pcapng` writes `57`/`59`.
* `different-explosives-mines-grenades-on-me.pcapng` places a device with `0A`
  at t+202.12, one fixed aim point over five beats. Its three grenades are the
  `14`→`16` runs at t+180, 284 and 310; the first writes them `94`/`96` and the
  other two `D4`/`D6`. `Throw` was the old name for `0A`, which read a placement
  as a throw.

## Reload cycle

Measured on `shooting-and-reloading.pcapng`, slot 8, weapon `0x17`:

* The weapon pair is `[00, weapon, 00, 00, ammo]`; the magazine is `0F` (15
  rounds) and falls `0F → 0E → … → 00` as the trigger spends it. The bots report
  the same byte from `BotBodyState.MagazineRounds`.
* A spent magazine opens the nine-byte `0B FF 00 <aim>` run: t+19.88 to 23.34
  (3.47 s), t+25.48 to 28.94 (3.46 s) and t+41.81 to 45.27 (3.46 s). One run
  splits into a 0.46 s nine-byte opening and a 2.96 s three-byte `0B FF 00` tail,
  so both forms are the same reload.
* The pair refills to `0F` on the reload's last beat, never mid-run.
* `0x48 FF 00` follows one firing run for six three-byte records, 0.17 s
  (t+35.71 to 35.87), and is interleaved as single beats elsewhere; it is the
  recovery beat behind an attack, not a second reload. `stun-by-throwing-to-ground`
  slot 6 holds `44` on target 8 from t+45.07 to 46.17 and writes the same
  three-byte `48 FF 00` standing from t+46.22, before it walks off at 46.46.

`BotReloadService` runs the cycle: fifteen rounds, 3 460 ms of reload and 170 ms
of recovery; `BotStateBodiesUtils` writes the `0x4B` beats and the `0x48` beat,
and `BotActorRecordUtils.BuildWeaponPair` reports the magazine. The same 170 ms
window follows the firing beat an engagement ends on, which is the second place
the capture writes `0x48`; `BotBodyState.WasFiring` marks that beat.

## Stun family: sleep and stun

The non-lethal damage subtypes (`DamageKind`) and what the latest capture shows
each does to the body:

| Subtype | Kind | Behaviour |
| ------- | ---- | --------- |
| `0x40` | `ExplosiveStun` | a sleeping claymore, sleep C4 or S.G. mine: spends stamina, and an emptied bar **sleeps** the body for about 26 s (`sleep-by-sgmines.pcapng`: slot 1 down t+133.46, up t+160.66; slot 2 t+119.06 to t+146.26). Health stays 250 and the body writes the sixteen-byte lying pose `01 1C`, never the thirty-two-byte corpse. The blast arrives at eleven **or** fifteen bytes. |
| `0x41`, `0x42` | `Stun` | a tranquilliser hit: spends the stun bar; an emptied bar **sleeps** while stamina refills (`01 1C`), like the device but without its 26 s floor |
| `0xC1` | `Stun` | the non-lethal knife hit: an emptied bar shows the `C1 1A` stun animation while stamina refills, not a sleep |
| `0xAD`, `0x2D` | `Stun` | the stun stab: the same `C1 1A` stun pose, not a sleep |
| `0x21`, `0x24` | `Knockout` | a CQC reach and throw impact, at eleven or fifteen bytes: spends stamina; an emptied bar shows the `C1 1A` stun animation while stamina refills, not a sleep |

Both the `0x40` device and the tranquilliser call `Sleeping`; CQC and the knife
show the stun pose. The blow that empties the bar decides, and the last such blow
wins: a tranquilliser sleeps the body until stamina refills, a device sleeps it
for its measured 26 s, and CQC or a knife holds `C1 1A` for the recovery time. A fifteen-byte
`0x40` record is a real shape (three of them in `sleep-by-sgmines.pcapng`) and
was previously refused by the codec, so the sleep device's blast had no effect.

The CQC throw to the ground is not a damage subtype at all. `stun-by-throwing-to-ground`
shows two of them: the first at t+54.02 lands `81 03` → `01 04` and the body is
up at t+56.07; the second at t+58.67 runs `C1 1A` for 0.8 s and then `01 1B`
lying from t+60.07 to 73.92 — about 13.8 s — before `61 1B` and `61 00` bring it
up. The short stun and its climb out are the `C1 1A`/`E1 1A` poses; none of them
is the sleep's `01 1C`, and the sleep device is measured at about 26 s.
