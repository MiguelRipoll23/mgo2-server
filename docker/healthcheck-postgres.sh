#!/bin/sh
# Fails until PostgreSQL is accepting connections and the schema migrations have
# been applied, so a service waiting on this healthcheck starts against a
# database whose schema is up to date. Entity Framework records every applied
# migration in the __EFMigrationsHistory table; its absence means the migration
# bundle has not run yet, which is the only time the temporary instance the
# entrypoint starts is the one answering pg_isready.
set -e

pg_isready -U postgres -d mgo2 >/dev/null

psql -U postgres -d mgo2 -tAc "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND lower(table_name) LIKE '%migrationshistory'" | grep -q 1