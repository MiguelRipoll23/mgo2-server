# AGENTS.md

* Each domain owns its own service(s).
* A domain must never interact directly with another domain.
* To use another domain’s functionality, call its service.
* Keep domain boundaries strict and avoid cross-domain dependencies.
* No source file may exceed 400 LOC.
* Organize code into focused services, interfaces, utils, types, etc. to keep files below the 400 LOC limit.
* Services and utilities must use the service or utils suffix respectively in both the file name and class name.
* Do not use abbreviations unless they are extremely common and well-established, such as id, utils, or info.

## Tests

* **Test the protocol crypto, encoding and decoding. Nothing else.**
* The codecs are the whole of the suite: the peer-to-peer frame cipher and its tail digest,
  the TCP packet codec, the record framing, and the handshake body — each tested for the
  round trip and for the malformed input it has to refuse.
* Do not add a test for a payload, a handler, a builder's output or a property. Those change
  as the protocol is read further, and each one's byte offsets live in `docs/protocol/`,
  which is where a reader should look.
* Do not test text utilities, chat payloads, or persistence mappings.
* Prefer one test per behaviour over several that re-derive the same fact in other words.
* When a change alters the codecs, update those tests; do not add new ones alongside them.

## Schema

* The schema is owned by the Entity Framework migrations in `src/Shared/Persistence/Migrations`.
* Never create, alter or repair a table at runtime. Change the model and add a migration
  instead, and commit the generated files in the same change.
* **Migrations are never written by hand.** Every migration file comes from
  `dotnet ef migrations add <Name>`, run against the model; do not create one, do not
  scaffold one by copying a neighbour, and do not hand a schema operation into an existing
  one — the two exceptions in the next bullet are the only ones. The model is the only thing
  a schema change is expressed in.
* A generated migration may be extended with two things, and nothing else. A **data
  statement** — a backfill the model cannot express — in a marked block containing no DDL:
  `ExperiencePerCharacter` and `CharacterGameplayOptions` are the shape of it, the second
  decoding the stored settings blob with `jsonb` operators, so no setting is lost. And a
  **conversion the provider cannot express**, which is a schema statement with no other
  route: `CharacterActiveFlag` is the case, because PostgreSQL will not cast an integer
  column to boolean without a `USING` clause that Entity Framework has no way to write.
  Both are visible in the file rather than hidden at runtime, and the model still owns the
  change — the snapshot comes from it, and reviewing the model against the snapshot is a
  check on the change rather than an automated one.
* A migration that has been committed and applied is never edited: an applied database takes
  the statement once, so an edit reaches only the databases that have not run it. Correct a
  migration by adding the next one, however small the correction.
* No server applies migrations either. The postgres container runs a migration bundle
  on startup, before it accepts connections; a local run applies them with
  `dotnet ef database update`.
* Run `dotnet tool restore` once per clone, then `dotnet ef migrations add <Name>` from the
  repository root.
* A forgotten migration must not reach a deployment: check the model against the migrations
  before shipping one. This is not covered by an automated test — the suite is the codecs
  alone — so it is a review step, and it belongs in the description of the change.
* A database that predates the migrations holds the schema with no migration history, so it
  has to be recreated rather than migrated.
* Files the migrations tool generates — the migrations and the model snapshot — are exempt
  from the 400 LOC limit, and the snapshot is never edited by hand.

### How the reference does it

`comradesean/mgo2server` migrates with Flyway and **hand-writes** its `V<n>__<name>.sql`, each
carrying the prose that explains the change, because it has no model to generate from: the SQL
*is* its schema. Neither habit transfers here, and its reasons are worth keeping in view:

* Its migrations are immutable once applied — Flyway checksums the whole file, comments
  included, and a mismatch crash-loops every container — so a migration with a wrong comment
  stays wrong and the fix is always the next migration. We have the same constraint with none of
  the enforcement: an edited migration only lands on databases that have not run it yet.
* Its schema is only ever as trustworthy as the last person's SQL; ours is checked against the
  model when the migration is written, which is why the statement has to come from the tool. A
  hand-written migration here would be a schema with nothing comparing the two.
* Its convention from `V52` forward is worth copying: **no wire offsets in a migration.** A
  column comment says what the data *means*; the packet's layout lives in the payload builder
  or the parsing document. Offsets in both drift apart, and the schema is the wrong
  place to look for a wire layout anyway.
