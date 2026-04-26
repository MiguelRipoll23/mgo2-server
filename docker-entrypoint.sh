#!/bin/sh
set -e

SEED_MARKER="/app/data/.seeded"
PGDATA="/var/lib/postgresql/17/docker"
PG_LOG="/var/log/postgresql/entrypoint.log"
PG_BIN="/usr/lib/postgresql/17/bin"

# Ensure PostgreSQL directories exist with correct ownership
mkdir -p "$PGDATA" /var/log/postgresql
chown -R postgres:postgres /var/lib/postgresql /var/log/postgresql

# Initialize PostgreSQL data directory if needed
if [ ! -s "$PGDATA/PG_VERSION" ]; then
  echo "[entrypoint] Initializing PostgreSQL..."
  su -s /bin/sh postgres -c "$PG_BIN/initdb -D '$PGDATA' --auth-local=trust --auth-host=trust"
fi

# Start PostgreSQL
echo "[entrypoint] Starting PostgreSQL..."
su -s /bin/sh postgres -c "$PG_BIN/pg_ctl -D '$PGDATA' -l '$PG_LOG' start"

# Wait for PostgreSQL to be ready
echo "[entrypoint] Waiting for PostgreSQL..."
until su -s /bin/sh postgres -c "$PG_BIN/pg_isready -q"; do
  sleep 1
done

# Create database if it doesn't exist
su -s /bin/sh postgres -c "$PG_BIN/psql -tc \"SELECT 1 FROM pg_database WHERE datname='mgo2'\" | grep -q 1 || $PG_BIN/createdb mgo2"

# Default to local PostgreSQL if DATABASE_URL is not set
export DATABASE_URL="${DATABASE_URL:-postgresql://postgres@localhost/mgo2}"

echo "[entrypoint] Running database migrations..."
deno task migrate

if [ ! -f "$SEED_MARKER" ]; then
  echo "[entrypoint] Seeding database for the first time..."
  deno task setup
  mkdir -p /app/data
  touch "$SEED_MARKER"
  echo "[entrypoint] Seeding complete."
fi

exec deno run --node-modules-dir=auto --allow-env --allow-read --allow-net src/main.ts
