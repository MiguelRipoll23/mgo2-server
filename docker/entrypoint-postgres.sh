#!/bin/sh
set -e

# Run the migration bundle against the database before PostgreSQL starts
# accepting connections. The bundle reads the connection string from
# DATABASE_CONNECTION_STRING or DATABASE_URL, the same variables the servers
# use. Starting a temporary instance, migrating, and then handing off to the
# real entrypoint keeps the healthcheck (pg_isready plus the migration history
# table) reliable: by the time the database is accepting connections, the
# schema is already up to date.

# Start a temporary PostgreSQL instance in the background.
/usr/local/bin/docker-entrypoint.sh postgres &

# Wait for the temporary instance to accept connections.
until pg_isready -U postgres -d mgo2 >/dev/null 2>&1; do
    sleep 1
done

# Apply the schema. The bundle carries the migrations without the SDK, the
# tools or the source tree, and it is the only thing in the deployment that
# is allowed to change the schema.
./efbundle --connection "${DATABASE_CONNECTION_STRING:-$DATABASE_URL}"

# Stop the temporary instance.
kill -TERM "$!"

# Wait for it to exit cleanly.
wait "$!" 2>/dev/null || true

# Start the real PostgreSQL instance. docker-entrypoint.sh handles data
# directory initialisation on first run and idempotently on subsequent ones,
# so calling it again is safe.
exec /usr/local/bin/docker-entrypoint.sh "$@"
