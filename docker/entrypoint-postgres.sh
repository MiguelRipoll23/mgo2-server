#!/bin/sh
set -e

# Run the migration bundle against the database before PostgreSQL starts
# accepting connections. The bundle reads the connection string from
# DATABASE_CONNECTION_STRING or DATABASE_URL, the same variables the servers
# use. Starting a temporary instance, migrating, and then handing off to the
# real entrypoint keeps the healthcheck (pg_isready plus the migration history
# table) reliable: by the time the database is accepting connections, the
# schema is already up to date.

# Two things about the temporary instance are load-bearing, and both are here
# because getting either wrong turns a migration into an outage.
#
# It listens on the loopback interface, on a port of its own. It must never
# answer on 5432. The servers keep pooled connections open, and the readiness
# probe is satisfied by a database that has already been migrated — its history
# table is there from the previous run — so an instance on 5432 becomes Ready
# for the two seconds the bundle takes and collects clients in that window.
#
# And it is stopped with SIGINT, which is a *fast* shutdown. SIGTERM is a smart
# shutdown: it waits for every client to disconnect first, and an idle pooled
# session never does. Observed on the cluster 2026-11-02: the postmaster sat in
# "the database system is shutting down" for as long as the pod lived, the
# `wait` below never returned, the real server was never started, and every
# service in the namespace crashed against a database that was running but
# could not talk to anyone. A fast shutdown terminates those backends and
# completes, which is also all a temporary instance needs.
/usr/local/bin/docker-entrypoint.sh postgres \
    -c listen_addresses=127.0.0.1 \
    -c port=5433 \
    -c unix_socket_directories=/tmp &

temporary=$!

# Wait for the temporary instance to accept connections, but not forever: an
# instance that never comes up has to fail the container rather than hold it
# Ready-less and silent.
ready=0
for _ in $(seq 1 120); do
    if pg_isready -h 127.0.0.1 -p 5433 -U postgres -d mgo2 >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 1
done

if [ "$ready" -ne 1 ]; then
    echo "The temporary instance did not start listening; refusing to serve without the schema." >&2
    exit 1
fi

# The host and port in the secret name the Service, and this instance is
# deliberately not reachable there, so they are replaced rather than reused.
# Everything else in the string — the database, the user, the password — is
# what the servers connect with, so the bundle migrates the database they use.
temporary_connection="$(
    printf '%s;' "${DATABASE_CONNECTION_STRING:-$DATABASE_URL}" \
        | sed -e 's/[Hh][Oo][Ss][Tt]=[^;]*;//g' -e 's/[Pp][Oo][Rr][Tt]=[^;]*;//g'
)Host=127.0.0.1;Port=5433"

# Apply the schema. The bundle carries the migrations without the SDK, the
# tools or the source tree, and it is the only thing in the deployment that
# is allowed to change the schema.
./efbundle --connection "$temporary_connection"

# Stop the temporary instance. The timer is the last resort: if the fast
# shutdown somehow does not finish, an immediate shutdown (SIGQUIT) still frees
# the data directory, and the real server recovers it on the way up. What must
# not happen is waiting for it indefinitely.
kill -INT "$temporary" 2>/dev/null || true

(
    sleep 60
    echo "The temporary instance is still up after a fast shutdown; shutting it down immediately." >&2
    kill -QUIT "$temporary" 2>/dev/null || true
) &
timer=$!

wait "$temporary" 2>/dev/null || true
kill "$timer" 2>/dev/null || true

# Start the real PostgreSQL instance. docker-entrypoint.sh handles data
# directory initialisation on first run and idempotently on subsequent ones,
# so calling it again is safe.
exec /usr/local/bin/docker-entrypoint.sh "$@"
