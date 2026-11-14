# The rules' instruction texts, from the PC client

**What this file is.** The sentences the game shows a player when a rule is being
explained, lifted verbatim from the PC client's own data — the answer to "where are the
rule instructions". They are **not** in any binary: an earlier pass searched
`MGO2.ELF`, `system.dll`, `savemgo.dll` and `mgo2.exe` and correctly found only the
HUD's short strings and the `STRING_naiyou_RULE` keys there. The text is in the lobby
stage script instead, which is data rather than code.

**Where they came from.** `https://cdn.mgo2.net/pc-client.zip`, 129 399 876 bytes,
downloaded 3 November 2026 and extracted to `.game/downloads/pc-client/` (that path is
gitignored — `.gitignore:19`). The text is not in any `.dll` or `.elf`: it is in
**`game/stage/lobby/scenerio.gcx`**, the lobby stage script, which carries the Create
Game menus, their tooltips and these rule panels together.

**Not only the rules.** The same file carries the Create Game menus — every setting the
room body encodes, from `Round Time` to `Briefing Time` to `Auto Assign Teams`. That
list, with offsets, is §8 of `docs/analysis/rotation-payload-corpus.md`, where it is
used to bound what the gameplay body's unnamed fields can be.

**How it was read.** The GCX stores its text as NUL-separated strings with the line
breaks written as literal `\n` escapes, so the panel is one string, not several. Cutting
the file on NUL and decoding each run as UTF-8 gives 14 142 runs of 25 characters or
more; the rule panels are the English runs at `0x059d22`–`0x061a4b`. Offsets below are
file offsets in `scenerio.gcx`, and every panel is followed immediately by its French,
German, Italian and Spanish translations.

## The rules, in the panel's own order

| rule | id | panel | one-line menu text |
| --- | ---: | --- | --- |
| TDM | 1 | `0x059d22` | `0x0c7b9d` |
| RES | 2 | `0x05a0f1`, with `0x05a650` | `0x0c7e8b` |
| CAP | 3 | `0x05a9b4` | `0x0c8125` |
| SNE | 4 | `0x05b8b9`, with `0x05c000` and `0x05b028` | `0x0c8444` |
| BASE | 5 | `0x05c4a4` | `0x0c85eb` |
| BOMB | 6 | `0x05c84c`, with `0x05ce23` and `0x05d502` | `0x0c92fa` |
| TSNE | 7 | `0x05de19` | `0x0c8813` |
| SDM | 12 | `0x05f648` | `0x0c8e44` |
| INTERVAL | 13 | `0x06046c` | `0x0c906c` |
| SCAP | 15 | `0x06189f` | `0x0c98b5` |
| DM | 0 | `0x05fdac` **[I]** | — |

The pairing is by wording for every row but DM, which shares the panel's "free-for-all"
phrasing with two other panels (`0x060f9d`, `0x05f648`) and is placed by elimination.
The one-line menu text for DM is not in the second block; the block's `0x0c7b9d` row is
TDM's, and the menu otherwise shows the rule name alone.

## The panels, verbatim

### TDM — `0x059d22`

> Red Team vs. Blue Team.
>
> Win by fighting alongside your teammates
> and reducing the other team's tickets to zero.

### RES — `0x05a0f1`

> Battle over the fate of GA-KO!
>
> The attacking team must bring GA-KO back to its goal to win.
> The defending team guards GA-KO.
>
> You can also win by defeating all members of opposing team.

and `0x05a650`:

> The attacking team can also win by
> having possession of GA-KO when the timer reaches 0.

### CAP — `0x05a9b4`

> Race to capture KEROTAN and GA-KO.
>
> Capture the target, then take it back to your team's
> goal and protect it until the timer reaches zero.
>
> If both goals have a KEROTAN or GA-KO, the timer is reset.

### SNE — `0x05b8b9`

> One player controls Snake, fully equipped with OctoCamo!
> If many players join, one player becomes Metal Gear Mk.II
> and can support Snake.
> Snake wins if he collects a set number of dog tags.
> (To obtain dog tags, knock out or hold up other players
> and then do a body search by pressing the △ button.)

the others' win condition at `0x05c000`:

> Other players win by killing Snake a set number
> of times.
>
> If the time expires first, victory goes to the team which
> killed more players from the opposing team.

and the optional variant at `0x05b028`, shown when the host lets a player be Snake:

> "Snake can join" is enabled!
> The player who is Snake must hold up or take down other
> characters and collect a certain number of dogtags from them to win.
> Other players can win within the normal rules
> or by defeating Snake.
> (If 11 or more characters are playing,
> one player becomes Metal Gear Mk.II and can support Snake.)

### BASE — `0x05c4a4`

> Battle to control bases located throughout the map.
>
> The first team to capture all bases, or the team
> with the most bases when time runs out, wins.

### BOMB — `0x05c84c`

> A battle to destroy or protect target areas on the map.
> The attacking team will claim victory if they can plant time bombs
> in the target areas and protect them until any one of them detonates.

the defenders' page at `0x05ce23`:

> The defending team can win by protecting all of the target areas
> on the map until the timer expires.
> The defending team can use Coolant to nullify planted time bombs.
> The attacking team can only have one bomb at a time.

and the second defenders' page at `0x05d502`:

> The defending team can use Coolant to nullify a bomb,
> or defeat the player with a bomb and then eliminate it by leaving
> it on the ground for a set period of time.
> When the bomb is eliminated, it will reappear at its original location.
> To win the defending team must either hold the base
> until time runs out, or eliminate all of the bombs.

### TSNE — `0x05de19`

> A battle between two teams with different gear.
>
> The sneaking team (stealth-equipped) must eliminate all enemies,
> or bring KEROTAN or GA-KO to their goal to win.
> The defending team must defeat all enemies or keep KEROTAN
> and GA-KO safe to win.

### SDM — `0x05f648`

> A free-for-all match with no teams.
>
> All participants are equiped with stealth.
>
> The battle area decreases in size as time passes.
> Players who wander outside of the battle area will suffer damage.
> Once a player dies, they will not respawn.

### DM — `0x05fdac`

> The alarm will sound when enemies are nearby.
> The alarm is functioning when the alarm icon is on.
>
> Whenever someone dies, all surviving players will receive points.
> In addition, players will also receive points whenever
> they defeat an opponent.

### INTERVAL — `0x06046c`

> A rule that allows an interval of rest between matches.
> For health reasons, it's best to avoid long periods of
> continuous play.
> Any actions performed while using this rule
> will have no influence on Level, Score, Personal Stats, or
> Skill development.

### SCAP — `0x06189f`

> A Red Team Vs. Blue Team match.
>
> Each Team must bring its target to the checkpoints
> a certain number of times to win;
> the Blue Team's target is KEROTAN,
> the Red Team's target is GA-KO.

### Panels that belong to no rule in the rotation

| offset | text | what it is |
| --- | --- | --- |
| `0x05e438` | Cooperate with teammates to complete the mission. Follow orders and achieve the mission objectives. | COOP, rule 8 — hardcoded off in the client (`docs/GATES.md` §3) |
| `0x05e711` | Practice the controls using targets and dolls placed throughout the stage. | Training |
| `0x05e959` | Join a game in which you can receive instruction from the host player. | Combat Training |
| `0x060c36` | Headshots settings will be ignored. Under this rule, headshots will do the same amount of damage as any other attack. | the rule modifier that clears the Headshots-Only penalty |
| `0x060f9d` + `0x061379` | A free-for-all match with no Teams. The first Player able to keep the target in their possession until the counter reaches zero wins. / Points will not be awarded for defeating opponents… | a free-for-all target mode, paired with its own scoring note |

## The option notices — the Drebin and Headshots pages

The Create Game options have panels of their own, in the same block, and they are the
client's own statement of what `docs/GATES.md` §2 measures on the wire:

### DREBIN POINTS — `0x05ec3c`

> DREBIN POINTS are enabled!
>
> Based on your Drebin Points (DP), you can customize
> and use weapons not normally available.
>
> You will earn more DP depending on how well you do in battle.

### HEADSHOTS ONLY — `0x05f139`

> HEADSHOTS ONLY is enabled!
>
> Players who fail to down an enemy by a
> headshot will be penalized.

Both are shown when the round's third triple byte carries `2` or `4`
(`docs/analysis/rotation-payload-corpus.md` §2), which is the radio those two facts
describe from opposite ends: the client's text here, the reader at `0x6A9948` there.

## The result banners sit in the same table

The stage resource table that carries these panels also carries the text a round
ends on. `game/stage/<stage>/scenerio/resources.gcl` declares it once, at ids
`3432`–`3477`, in the same five languages and the same
English-then-French-then-German-then-Italian-then-Spanish order:
`MISSION COMPLETE!` (`3432`), `RED TEAM WON!` (`3437`), `BLUE TEAM WON!`
(`3442`), `DRAW!` (`3447`), `RED GOAL` / `BLUE GOAL` (`3452`/`3457`),
`SNAKE WON!` (`3462`), `SNAKE LOST!` (`3467`), `ROUND SET` (`3472`) and
`GAME SET` (`3477`). Every one of the twenty-one gameplay stages carries the
block at those ids; the lobby, boot and init stages do not, because no round is
played in them. The wire coding those banners are drawn for is `state[2]` and
`state[3]` of the operations state block, read in
[`gameplay-data-functions.md`](gameplay-data-functions.md) §5b.

## What this settles, and what it does not

* **It settles the wording.** The sentences are now read from the client's own data
  rather than recalled, and every one of them is at a file offset in a file this
  project can fetch again.
* **It settles what the rules are called in the UI.** Snake's panel is Sneaking
  Mission with OctoCamo and dog tags; the team version is "two teams with different
  gear"; BOMB has three panels, two of them written for the defending team.
* **It does not name any wire field.** Nothing here touches the room-state payload.
  The one place the two meet is the round's third triple byte, and both directions of
  that match are recorded above.
* **The other four languages are at adjacent offsets** — French, German, Italian and
  Spanish follow each English panel in that order — so a server that wanted a
  localized briefing has the text in the same file.

## Re-deriving it

```
mkdir -p .game/downloads && cd .game/downloads
curl -L -o pc-client.zip https://cdn.mgo2.net/pc-client.zip
unzip -q pc-client.zip -d pc-client
cd pc-client && python3 - <<'EOF'
raw = open('game/stage/lobby/scenerio.gcx', 'rb').read()
offset = 0
for chunk in raw.split(b'\0'):
    if len(chunk) >= 25:
        try:
            text = chunk.decode('utf-8')
        except UnicodeDecodeError:
            text = None
        letters = [c for c in text if c.isalpha()] if text else []
        if letters and sum(c.isascii() for c in letters) > 0.95 * len(letters):
            if 0x059c00 <= offset < 0x061b00:
                print(hex(offset), text.replace('\\n', '\n'))
    offset += len(chunk) + 1
EOF
```

That is the whole method: split `scenerio.gcx` on `0x00`, decode each run as UTF-8,
keep runs of 25 characters or more whose letters are ASCII, and print those in the panel
range. The one-line menu texts are the same loop over `0x0c7b00`–`0x0c9b00`; the
non-English panels are the same loop with the ASCII test removed.
