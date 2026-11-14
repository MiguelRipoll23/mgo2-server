# Action capture index

Every capture in `.pcapngs/actions/` and the wire signature it was recorded to
identify. The pose and action tables are in
[ACTOR_POSE_AND_ACTION.md](ACTOR_POSE_AND_ACTION.md); this page is the index that
keeps a capture from being dismissed because its action does not move a pose.

The actor block is the six tick records per slot ten identifiers apart: state at
the base, vitals at `+1`, position at `+2`, weapon pair at `+4`, aim at `+6`, with
a hit at the state's own owner (`BotActorRecordUtils`). A capture records the
joiner and the host, so the side a record came from matters: the host's own
bot-shaped records are the ones a simulated actor can reproduce.

| Capture | Wire signature | What it identifies |
| ------- | -------------- | ------------------ |
| `sending-chat-macros` | `0x1a58`, four bytes `[kind, slot, macro, 0x02]`; client kind `0x01`, host kind `0x02`; macro `0x00` idle | **Chat macro.** The client sends `01 <slot> <macro> 02`; the host confirms the same slot and macro as `02 <slot> <macro> 02` to the sender and broadcasts it to the room. The third byte is the macro/phrase index (measured `0x05`, `0x10` and others), not a slot and not a kill. There is no actor pose or state action. The same identifier's **two-byte** body `00 <count>` is the room count; only the four-byte body is the chat macro. `ChatMacroHandler` performs the confirmation. |
| `hiding-and-moving-wearing-box` | weapon pair `09 14 00 00 00` boxed, `00 14 00 00 0F` after; pose `A1 A8` boxed crawl, `A1 28` crawl; item `176` (`C BOX A`) | **Cardboard box.** The box is the weapon pair's first byte, `0x09` (`WeaponPairUtils.CardboardBoxFlag`), not a separate item-box body or animation. The pose is the ordinary armed crawl, so a boxed actor crawls with `A1 A8`; it walks normally once the flag returns to `0x00`. |
| `switching-between-weapons` | pairs `00 34 00 00 03` grenade, `00 14 00 00 0F` P90, `00 07 00 00 08` Five-seveN, `00 18 00 00 0F` M4; pose `21 12` / `01 12` | **Weapon switch.** The pair's weapon and ammo change and the upper body plays animation `0x12`; there is no state-action byte for it. The actor is otherwise idle. |
| `magazine-to-teammate` | pair `00 45 00 00 <0..3>`; pose `41 30`, `A1 30`, `E1 30` | **Magazine handover.** The handover is animation `0x30` on the giver; the carried item is the magazine object's `0x45`, with its count in the ammo byte. No state action. |
| `forward-roll-and-also-forward-roll-to-crouching` | pose `81 81` for nine records, then `21 01`; crouched finish `A1 02` then `61 00` | **Forward roll.** Animation `0x00` on the prone head, then the standing/crouched head; no action byte and no damage record. |
| `backward-step-facing-forward` | pose `C1 01` one beat, then `C1 81` about 1.4 s | **Back-step.** A knocked-down head with animation `0x00`, not the `0x1A` stun; no damage record. |
| `left-and-right-side-flip` | same `C1 01` → `C1 81` | **Side flip.** Identical pose signature to the back-step, and likewise no damage record; the evasion is not a stun. |
| `different-explosives-mines-grenades-on-me` | action `0A` for five beats with a fixed aim point; `14` → `16` (also `94`/`96`, `D4`/`D6`) then blasts | **Device placement and grenade throws.** `0A` is a mine/C4 set down, not a throw; `14` is the readied grenade and `16` its release, with the `0x80` throw bit and sometimes the `0x40` acting bit above the action. |
| `hitting-with-weapon-short-and-long-attack` | pose `41 18`, `41 19`; action `48 FF 00` closes a run; damage subtype `0x91` | **Melee with a weapon.** A short and long swing are the same animation and action family; the recovery `0x48` is the beat after the swing. |
| `damage-and-kill0-with-knife` | three-byte states `57 FF 00` / `58 FF 00` / `5A FF 00`; pose `01 1E`; damage `0xC1` | **Knife stun and CQC hold family.** The three-byte `57`/`58`/`5A` bodies name no victim, so they are the actor's own CQC animation; the stun is the separate `0xC1` damage record. |
| `cqc` | states `57`/`59`; poses `41 37`, `01 35`, `01 36`, `01 3A`; damage `0x21`/`0x24` | **CQC hold, choke and throw.** `0x37` opens the grab, `0x35`/`0x36` are the choke and hold, `0x3A` the throw out of it; the `0x21`/`0x24` records are the impacts. |
| `stun-by-cqc-holding-strangling` | states `57`; poses `01 35`, `01 36`, `21 36`, `61 36`, `41 37` | **Strangle.** The choke `0x35` and hold `0x36` behind the grab `0x37`; the victim's head is the hold's, not a corpse. |
| `stun-by-hitting-and-kicking` | poses `21 1F`, `41 1F`; damage `0x21`/`0x24`; recovery `0x48` | **Punch and kick.** Animation `0x1F` on the attacker; the impact is the CQC damage record, and the body recovers on `0x48`. |
| `stun-by-knife` | poses `C1 1A`, `E1 1A`, `01 1E`; damage `0xC1`/`0xAD`/`0x2D` | **Knife stun.** The short stun animation `0x1A` and the lying `0x1E`; health stays intact. |
| `stun-by-throwing-to-ground` | poses `21 33` wind-up, `81 03`/`01 04`, `01 1B` for 13.8 s, `61 1B`; states `57`/`5A` | **CQC ground throw.** `0x33` is the thrower's wind-up, `0x03`/`0x04` the first landing, and `0x1B` the long lying form; the `0x48` recovery follows a firing engagement in the same capture. |
| `sleep-by-sgmines` | damage `0x40` (eleven or fifteen bytes); pose `01 1C`; health `250` | **Sleep device.** The only stun family that sleeps; about 26 s lying, no corpse and no health loss. |
| `shooting-and-reloading` | states `02`/`04`/`06`/`07`, `0B` reload, `08` recovery; pair ammo `0F → 00 → 0F` | **Fire and reload.** Fifteen rounds, a 3.46 s reload, and the `0x48` recovery behind an attack; the pair reports the magazine. |
| `switching-standing-crouching-prone` | heads `21`, `41`, `61`, `81`, `A1`, `C1`, `E1`; state `25` spawn | **Stance set and spawn.** The head is `stance << 5 | active`; `25 FF 00` is the round-opening/spawn form. Host bots in this capture stay standing or walking. |
| `switching-standing-crouching-prone-while-aiming-and-shooting` | pose animation `0x02` on standing/walking/prone/crawling heads; firing states | **Aim while changing stance.** The animation is separate from the head; the host bots here never crouch or go prone, so this is joiner evidence rather than a bot rule. |

## What the captures say about bot behaviour

- **Implemented for bots:** reload (`0x0B`), post-attack recovery (`0x48`), the
  short stun and sleep poses, the corpse down body, hit reaction, firing and
  spawn.
- **Player-side only in these captures:** chat macros, the cardboard box,
  weapon switching, magazine handover, rolls, back-steps, side flips and
  grenade/device use. Their records name the joiner's own action and no host
  bot repeats them, so they are identified here but not invented as bot
  behaviour.
- **Not a separate action:** the box flag, the weapon-switch animation, the
  handover animation and the evasion poses all live in the existing position
  or weapon-pair records. Adding an action enum for them would invent a field
  the capture does not carry.
