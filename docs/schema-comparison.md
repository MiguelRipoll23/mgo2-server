# Database tables: this server vs `comradesean/mgo2server`

A table-by-table comparison of the PostgreSQL schema each server creates, read from this
repository's Entity Framework model and migrations and from the reference's Flyway migrations.

| | this repository | reference |
|---|---|---|
| Revision | `3cc05bb` (2026-09-18), migrations `20260916090528_InitialSchema` … `20260918115638_DropPresenceSince` | `comradesean/mgo2server` `2e3b4dc` (2026-08-04), Flyway `V1__base` … `V74__first_view_memory_polarity` |
| Schema authority | the EF model — migrations are generated from it by `dotnet ef` | hand-written SQL migrations, checksummed by Flyway |
| Source | `src/Shared/Persistence/{Entities,Configurations,Migrations}` | `src/main/resources/db/migration` |
| Tables | **32** | **34** |
| Columns | **390** | **404** |
| Guard | `PersistenceModelTests` — model vs snapshot, plus an explicit table list | Flyway checksums; there is no model to compare the SQL against |

Both schemas descend from the same original storage layout — this repository's
`Mgo2DatabaseContext` says the names are "pinned to the original schema", and the reference's
`V1`–`V3` were reconstructed from the same place — but they have since diverged in spelling and,
in four places, in shape. Twenty-eight of our 32 tables have a live counterpart;
`character_stats` maps to one the reference dropped in `V16`; three tables exist only here and
six only there, for 34 on the reference side against 32 here.

Nothing here says the two must match. The question this document answers is only: **is there
anything the reference stores that this server cannot serve, or the reverse?**

---

## 1. Naming and type conventions

Almost every difference in the per-table listing below is one of these, so they are stated once.

| | this repository | reference |
|---|---|---|
| Primary keys | `integer` identity (`int` in C#) | `bigint` identity (`GENERATED ALWAYS AS IDENTITY`) |
| Character key | `character_id` | `chara_id` |
| Account key | `user_id` | `account_id` |
| Table plurals | `characters`, `clans`, `games`, `lobbies`, `sessions` | `chara`, `clan`, `game`, `lobby`, `account` |
| Column affixes | `beginner_only`, `expansion_only`, `subtype_id`, `type_id`, `ip_address` | `beginners_only`, `expansion_required`, `subtype`, `type`, `ip` |
| Multi-word keys | `skill_1` … `skill_4`, `level_1` … `level_4` | `skill1` … `skill4`, `level1` … `level4` |
| The word "index" | `idx` | `index` (quoted where it is reserved) |
| Timestamps | mixed: `timestamp without time zone` for plain fields, `timestamp with time zone` for the rest | `timestamptz` (or `timestamp with time zone`) throughout |
| Account timestamps | `last_login_time` / `previous_login_time` / `creation_time` as unix `integer` | `created_at`, `last_seen_at` as `timestamptz` |
| Session | a `sessions` row per account | a `session` column on `account`, `UNIQUE` |

The id type is the one with reach: ours is `integer` end to end, so a row count above 2^31 is
not reachable, and any component that expects the original `bigint` keys cannot read our tables
without a cast. The reference is `bigint` everywhere.

---

## 2. Table map

| this repository | reference | shape |
|---|---|---|
| `users` | `account` | differs — see §3 |
| `sessions` | — (`account.session` column) | one row per account, unique on the account |
| `characters` | `chara` | differs — see §3 |
| `characters_appearance` | `chara_appearance` | same columns, key named differently |
| `characters_chatmacros` | `chara_chat_macro` | same columns; different key and no range checks |
| `character_connections` | `chara_connection` | same columns; reference allows 64-char addresses |
| `characters_equipped_skills` | `chara_equipped_skills` | same columns, `skill_1`/`skill1` spelling |
| `characters_friends` | `chara_relation` | `type` here, `state` + `updated_at` there |
| `character_gameplay_options` | `chara_settings` | same 56 settings; reference also carries the client's defaults |
| `characters_sets_gear` | `chara_gear_set` | same columns, `idx`/`index` spelling |
| `characters_hostsettings` | `chara_host_settings` | **blob here, 44 typed columns there** — see §4.1 |
| `characters_instructors` | `chara_instructor` | one extra column here |
| `character_presence` | `chara_presence` | same shape; reference keeps `since` |
| `characters_sets_skills` | `chara_skill_set` | same columns, `idx`/`index` spelling |
| `character_stats` | `chara_stats` | **89 columns here; dropped there in `V16`** — see §4.2 |
| `characters_titles` | `chara_title` | `rank` here, `title_bit` + range check there |
| `clans` | `clan` | differs — see §3 |
| `clan_applications` | — (`clan_member.state`) | a separate table here |
| `clans_members` | `clan_member` | `rank` here, `state` + `joined_at` there |
| `games` | `game` | **16 columns here, 53 there** — see §4.1 |
| `game_master_mail` | `gm_mail` | reference adds `handled` and a partial index |
| `game_players` | `game_player` | same columns |
| `game_rounds` | `game_round` | same columns |
| `host_reviews` | `host_review` | same columns; reference adds the `rating` check |
| `instructor_reviews` | `instructor_review` | same columns; reference defaults `reviewed_at` |
| `lobbies` | `lobby` | differs — see §3 |
| `lobby_game_types` | — | a saved game-type list per lobby |
| `mail` | `mail` | `recipient_read` here, `is_read` there |
| `news` | `news` | `topic`/`message` here, `title`/`body` there |
| `round_reports` | `round_report` | **10 interpreted columns here, 30 raw counters there** — see §4.3 |
| `round_weapon_stats` | `round_weapon_tally` | `value_a/b/c` here, `kills`/`headshots`/`faints` there |
| `character_training_times` | `chara_training_time` | same columns |
| — | `skill`, `chara_skill` | the skill catalogue and per-character skill experience — see §4.4 |
| — | `gear_item`, `chara_gear` | gear catalogue, ownership and colour masks — see §4.4 |
| — | `gear_colour_highlight` | per-character colour highlights (was `reward_unlock`, renamed in `V68`) — see §4.4 |
| — | `inert_field_watch` | tripwire for fields believed unread (`V56`) |

Tables the reference created and then dropped, so neither schema has them: `chara_stats`
(`V15` → `V16`), `blob_audit` and `dev_packet_audit` (`V57`), `starter_gear` (`V70` → `V71`).

---

## 3. Shared tables whose columns differ

Spellings listed in §1 are omitted; what is left is a real difference.

**`users` vs `account`**

| only here | only there |
|---|---|
| `display_name` (the account name), `role`, `ban_reason` | `username`, `created_at` |
| `banned_until` as an unix `integer` | `banned_until` as `timestamptz` |
| — | `entitlements_byte3`, `entitlements_byte1` — the two `0x3049` trailer bytes, added in `V62`–`V65`, with the default `0` decided in `V64` |

Ours has no column for the entitlements bytes at all, so the day-one codec pack state the
reference can now express per account (`UPDATE account SET entitlements_byte3 = 1`) has nowhere
to live here. Ours also has no range check on `slots`; the reference added
`account_slots_range CHECK (slots BETWEEN 1 AND 4)` in `V35`.

**`characters` vs `chara`** — the same concept, different fields:

| only here | only there |
|---|---|
| `creation_time`, `last_login_time`, `previous_login_time` (unix `integer`) | `created_at`, `last_seen_at` (`timestamptz`) |
| `total_rewards` | `clan_left_at` |
| `rank` as `integer` | `rank` as `smallint` |

`experience` and `active` now agree on both sides: ours added `experience` per character in
`20260918101833_ExperiencePerCharacter` with the same `CHECK (experience BETWEEN 0 AND 65535)`
the reference wrote in `V59`, and `active` was added in `20260918110507_CharacterActiveFlag`
(converting an `integer` column with an explicit `USING` clause, since PostgreSQL will not cast
it) against the reference's `chara.active` from `V2`. Uniqueness of the name differs:
ours is `UNIQUE (name)`; the reference has that plus `chara_name_lower_idx`, a partial unique
index on `lower(name) WHERE active` (`V37`).

**`clans` vs `clan`**

| only here | only there |
|---|---|
| `open` (join policy) | — (`comment` here is `description` there) |
| `notice_time` as `bigint` | `notice_at` as `timestamptz` |
| `emblem_wip` | `emblem_at` as `timestamptz`, `emblem_mode` as `smallint` |
| `leader_id`, `notice_writer_id`, `emblem_editor_id` | `leader_chara_id`, `notice_writer_chara_id`, `emblem_editor_chara_id` |

Ours separates pending applications into `clan_applications` (unique on `(clan, character)`);
the reference keeps a `clan_member` row in `state` instead, so an application and a membership
are one row there and two tables here.

**`lobbies` vs `lobby`** — ours adds `created_at`, `updated_at`, `players_count`,
`replays_only` and a `UNIQUE (port)` index. The reference's `lobby` is the published endpoint
list only (`type`, `subtype`, `name`, `ip`, `port`, and the three flags) and it briefly grew
`hub_flags` / `player_count_override` in `V46`/`V47` before `V48` removed both.

**`characters_instructors` vs `chara_instructor`** — ours adds
`instructor_skill_awarded_at`. `graduated_at` is `timestamp without time zone` here and
`timestamptz` there.

**`characters_titles` vs `chara_title`** — ours keys on `(character_id, rank)` with an `id`
primary key and stores a `timestamp without time zone`; the reference keys on
`(chara_id, title_bit)` and checks `title_bit BETWEEN 0 AND 21`.

**`round_weapon_stats` vs `round_weapon_tally`** — ours stores three unnamed
`smallint` values per `(game, character, weapon)`; the reference names them `kills`,
`headshots` and `faints` and adds `reported_at`.

**`news`** — ours: `topic varchar(128)`, `message text`, `time integer`. Reference:
`title varchar(128)`, `body varchar(774)` (widened in `V40`), `time timestamptz DEFAULT now()`.

**`mail` / `gm_mail`** — ours stores `recipient_read` where the reference stores `is_read`
(`V23`). The reference's `gm_mail` adds `handled boolean` plus
`gm_mail_unhandled_idx … WHERE NOT handled` (`V61`); ours has no equivalent flag, so a
GM letter has no read/handled state.

---

## 4. The four shape disagreements

### 4.1 Host settings: a blob here, 44 typed columns there

`characters_hostsettings` holds `id`, `character_id`, `type` and a `settings` **text blob**,
encoded and decoded by `HostSettingsBlobCodec`. The reference's `chara_host_settings` holds the
same settings as 44 typed columns — `stance`, `max_players`, `friendly_fire`, `unique_red`,
`rotation_rules smallint[]`, `rotation_maps`, `rotation_flags`, `rule_timers integer[]`,
`weapon_restrictions bytea`, the `unread_*` fields — and spent `V52`–`V55` correcting
individual ones, including decoding its own earlier blobs with `jsonb` operators and then
dropping the blob column in `V55`, so no setting was lost.

The trade is visible in the queries. The reference can answer "which rooms disallow headshots"
and can comment the meaning of a single byte; here that byte is only addressable by decoding the
blob in code, and a wrong offset is a code change rather than a migration. The reference's `V73`
and `V74` are the argument for the other side, on the sibling table: five gameplay options were
on the wrong bits and one was inverted, and each correction was one migration against one named
column.

### 4.2 Lifetime statistics: an accumulator here, derived there

Ours stores `character_stats` — 89 columns, one row per character, of which eleven `stats_*`
`text` columns hold one JSON blob per game mode — and reads it in `RankingBoardService` and
`PersonalStatisticsPayloadBuilder`.

The reference built the same idea in `V15` (eight columns), then **dropped it in `V16`**, and
`V42` says why: "No accumulator tables. Every stats surface still derives from `round_report`
at query time… a `chara_stats` accumulator was already built and dropped as write-only." It
replaced the accumulator with three composite indexes on `round_report`.

So this is not a column difference but a different answer to the same question, and it is the
largest divergence in the two schemas.

### 4.3 `round_reports`: the interpretation is stored here, the bytes there

Ours keeps ten columns per report — `id`, `game_id`, `host_character_id`,
`target_character_id`, `seconds`, `experience`, `aborted`, `team_win`, `lobby_subtype`,
`created_at` — with the 0x4390 counters folded into `character_stats`.

The reference keeps the report as it arrived: 30 columns, including `flag_0x04`, `counter_0x0f`
through `counter_0x21`, `detail_present`, `detail_counters smallint[]` and `trailing_word`, and
indexes `(chara_id, reported_at)`, `(chara_id, rule, reported_at)` and `(rule, chara_id)` for
the ranking and matrix queries. Its raw counters mean a newly identified field is readable
without a replay of history; ours means the same number is only available in aggregate.

### 4.4 Gear and skills: catalogues here, tables there

Four reference tables have no counterpart here: `skill` (17 rows, names read off the disc in
`V60`), `chara_skill` (per-character skill experience), `gear_item` (descriptors and colour
masks) and `chara_gear` (ownership plus a `colours` mask per item). Ours carries the skill list
in `CharacterSkillCatalogue` and serves a fixed `GearCatalogue` payload, so nothing here records
which character owns which item or colour — the two gates the reference names in `V68` ("to actually grant a
colour, set the bit in `chara_gear.colours`; to grant an item, insert the `chara_gear` row").
`gear_colour_highlight` and `inert_field_watch` are the reference's two operational tables with
no equivalent in this schema.

---

## 5. Defaults, constraints and indexes

- **Defaults.** The reference states the client's own defaults in the schema — `normal_view_speed 5`,
  `bgm_volume 10`, `weapon_switch_mode 2`, `max_players 16`, `sneaking_snake_kills 3` — because
  they were read from the client's clamp run. Ours supplies the same values from code, so the
  `character_gameplay_options` columns have no `DEFAULT` and a row inserted by hand would come
  back wrong.
- **Range checks.** The reference is much the richer: `chara_experience_range`,
  `account_slots_range`, `chara_skill_experience_range`, `chara_gear_colours_range`,
  `host_review`'s `rating BETWEEN 1 AND 5`, `chara_title`'s `title_bit BETWEEN 0 AND 21`,
  `chara_chat_macro`'s type/index ranges. Here only `characters_experience_range` exists, which
  matches the reference's `V59` constraint exactly.
- **Uniqueness.** Both have unique character names, account names and one session per account.
  Both make a host review unique per `(game, voter)` — the reference names it
  `host_review_once_per_game UNIQUE (game_id, voter_chara_id)`, ours makes the same pair the
  primary key. The reference adds a partial unique index on `lower(name) WHERE active`; ours
  does not, and ours makes `lobbies.port` unique where the reference does not.
- **Foreign keys.** Both cascade character-owned rows on delete — appearance, skills, gear
  sets, loadout sets, chat macros, connections, settings, presence. Both leave a nameable
  cycle non-cascading: here `characters.user_id`, `clans_members` and the two identifiers a
  `clan` points back at; there `chara_account_id_fkey` cascades while `account.main_chara_id_fkey`
  is `SET NULL`. And both keep match history readable after a character is gone: ours gives the
  report's host and target `NoAction`, and the reference leaves `round_report.chara_id` and
  `round_weapon_tally.chara_id` with no cascade.
- **Current indexes only.** Enumerating the reference's indexes is exact (they are written out
  in the SQL); ours are partly conventions the provider adds for foreign keys, so an index
  count here would overstate the comparison. Not attempted.

---

## 6. Migration practice

Worth one note, since it shapes how the two schemas can be corrected.

The reference's migrations are hand-written and carry their reasoning in the file — `V59` is
twelve lines of comment to three of SQL, and explains why experience moved off the account
before dropping the pool. Ours are generated from the model, and `PersistenceModelTests` fails
if the model carries a change no migration captures, so a forgotten migration cannot deploy.
The cost of that guard is that a change to the *meaning* of a column has nowhere to be written
down: `V73`'s five wrong bits and `V68`'s renamed table have no equivalent artefact here.

The repository's own rule is already this split — see `AGENTS.md`, which keeps migrations
tool-generated but allows a migration to carry a data statement (a backfill the model cannot
express) or a conversion the provider cannot write, both visible in the file. The reference's
`V52`–`V55` blob decodes are exactly that first case, and ours already contains one instance
(`20260918110159_CharacterGameplayOptions`, which decoded the stored settings blob with `jsonb`).

---

## 7. Open questions

1. **Gear ownership.** Without `chara_gear`/`gear_item`, per-character gear and colour masks
   cannot be stored, so the reference's `V68`/`V70`/`V71` behaviour is not expressible here.
2. **Skill experience.** `chara_skill` holds per-character skill points; ours serves every skill
   at maximum, which is a policy in code rather than a row.
3. **Host settings.** 44 typed columns or one blob is a decision that has now been made
   differently on each side, and the reference's corrections argue for the columns.
4. **Lifetime statistics.** Whether `character_stats` stays is the same question the reference
   answered in `V16`/`V42`; it reads and writes here today, so dropping it is not free.
5. **Entitlements.** No column exists here for the `0x3049` trailer bytes the reference
   identified in `V64`/`V65`.
6. **GM mail `handled`.** A letter to the Game Master has no state here.
7. **Presence `since`.** Ours dropped it (`20260918115638_DropPresenceSince`) on the grounds that
   nothing on the wire carries it; the reference keeps it and resets it on a lobby hop. Both
   arguments are on record, and the two tables now differ by that one column.

---

## Method

The two sides were read offline, without a live database:

- **this repository** — the EF model snapshot
  (`Mgo2DatabaseContextModelSnapshot.cs`) for tables, columns, types, keys and the declared
  indexes; the entity and configuration files for defaults and delete behaviour.
- **reference** — the 74 Flyway files replayed in version order, with `CREATE TABLE`,
  `ALTER TABLE … ADD/DROP/RENAME COLUMN`, `ALTER COLUMN … TYPE`, `DROP TABLE`, `RENAME TO` and
  `ADD CONSTRAINT` applied in sequence, so the listing is the schema as of `V74` rather than the
  state any single file describes. Index and constraint lists come from the same replay.

Neither side was dumped from a running PostgreSQL, so anything the provider or Flyway adds
implicitly — EF's foreign-key indexes, Flyway's schema history table — is out of scope. The
column counts and the quotations in the sections above come from the sources named.
