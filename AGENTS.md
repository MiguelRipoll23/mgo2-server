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
* No server applies migrations either. The deployment runs the one-shot `mgo2-migrate` job,
  which carries a migration bundle, before any server starts; a local run applies them with
  `dotnet ef database update`.
* Run `dotnet tool restore` once per clone, then `dotnet ef migrations add <Name>` from the
  repository root.
* `PersistenceModelTests` fails when the model carries a change no migration captures, so a
  forgotten migration cannot reach a deployment.
* A database that predates the migrations holds the schema with no migration history, so it
  has to be recreated rather than migrated.
* Files the migrations tool generates — the migrations and the model snapshot — are exempt
  from the 400 LOC limit, and the snapshot is never edited by hand.
