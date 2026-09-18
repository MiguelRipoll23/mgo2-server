# AGENTS.md

* Each domain owns its own service(s).
* A domain must never interact directly with another domain.
* To use another domain’s functionality, call its service.
* Keep domain boundaries strict and avoid cross-domain dependencies.
* No source file may exceed 400 LOC.
* Organize code into focused services, interfaces, utils, types, etc. to keep files below the 400 LOC limit.
* Services and utilities must use the service or utils suffix respectively in both the file name and class name.
* Do not use abbreviations unless they are extremely common and well-established, such as id, utils, or info.

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
  change — the snapshot comes from it and `PersistenceModelTests` fails if the two part ways.
* A migration that has been committed and applied is never edited: an applied database takes
  the statement once, so an edit reaches only the databases that have not run it. Correct a
  migration by adding the next one, however small the correction.
* No server applies migrations either. The postgres container runs a migration bundle
  on startup, before it accepts connections; a local run applies them with
  `dotnet ef database update`.
* Run `dotnet tool restore` once per clone, then `dotnet ef migrations add <Name>` from the
  repository root.
* `PersistenceModelTests` fails when the model carries a change no migration captures, so a
  forgotten migration cannot reach a deployment.
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
  model by `PersistenceModelTests`, which is why the statement has to come from the tool. A
  hand-written migration here would be a schema with nothing comparing the two.
* Its convention from `V52` forward is worth copying: **no wire offsets in a migration.** A
  column comment says what the data *means*; the packet's layout lives in the payload builder
  and its test, or the parsing document. Offsets in both drift apart, and the schema is the wrong
  place to look for a wire layout anyway.
