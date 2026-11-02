#!/bin/sh
# Fails until PostgreSQL is accepting connections and the schema migrations have
# been applied, so a service waiting on this healthcheck starts against a
# database whose schema is up to date. Entity Framework records every applied
# migration in the __EFMigrationsHistory table; its absence means the migration
# bundle has not run yet.
#
# Both checks name port 5432 and reach it over the unix socket, which is the one
# listener that exists only once the real server is up: the entrypoint's
# temporary instance is on 127.0.0.1:5433 with its socket somewhere else, so it
# can neither pass this check nor be mistaken for the database. The socket is
# used rather than a TCP address because only the socket trusts the local user
# without a password; the instance still has to be the right one, hence the
# port.
set -e

pg_isready -p 5432 -U postgres -d mgo2 >/dev/null

psql -p 5432 -U postgres -d mgo2 -tAc "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND lower(table_name) LIKE '%migrationshistory'" | grep -q 1
