#!/usr/bin/env bash
#
# Runs the gameplay server directly on the host with the live k3s settings.
# The settings ConfigMap and database secret are read from k3s, then only the
# dedicated address and UDP port are overridden for this local validation run.
# dotnet watch keeps the process attached and applies supported code changes.
#
# The output is written to a log file as it arrives, through tee, so the run can
# be read back after the terminal has scrolled or the process has stopped. The
# file is truncated at the start of each run; set GAMEPLAY_SERVER_LOG to write
# it somewhere else.
#
# Usage:
#   scripts/run-gameplay-local.sh
#   GAMEPLAY_SERVER_PORT=3334 scripts/run-gameplay-local.sh
#   GAMEPLAY_SERVER_LOG=/tmp/gameplay.log scripts/run-gameplay-local.sh

set -euo pipefail

project_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
gameplay_port="${GAMEPLAY_SERVER_PORT:-3333}"
dedicated_address="${DEDICATED_ADVERTISED_ADDRESS:-192.168.1.15}"
log_file="${GAMEPLAY_SERVER_LOG:-${TMPDIR:-/tmp}/mgo2-gameplay-server-${gameplay_port}.log}"

if ! [[ "$gameplay_port" =~ ^[0-9]+$ ]] || (( gameplay_port < 1 || gameplay_port > 65535 )); then
    printf 'Invalid GAMEPLAY_SERVER_PORT: %s\n' "$gameplay_port" >&2
    exit 2
fi

if ! command -v dotnet >/dev/null 2>&1; then
    printf 'The .NET SDK is required to run the gameplay server.\n' >&2
    exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
    printf 'python3 is required to load the k3s appsettings.\n' >&2
    exit 1
fi

kubectl_command=(kubectl)
if ! kubectl get configmap mgo2-appsettings --namespace mgo2 >/dev/null 2>&1; then
    if sudo -n kubectl get configmap mgo2-appsettings --namespace mgo2 >/dev/null 2>&1; then
        kubectl_command=(sudo -n kubectl)
    else
        printf 'Cannot read mgo2-appsettings from k3s; check kubectl access.\n' >&2
        exit 1
    fi
fi

appsettings_json="$("${kubectl_command[@]}" get configmap mgo2-appsettings \
    --namespace mgo2 \
    --output 'jsonpath={.data.appsettings\.json}')"
if [[ -z "$appsettings_json" ]]; then
    printf 'The mgo2-appsettings ConfigMap has no appsettings.json.\n' >&2
    exit 1
fi

if [[ -z "${DATABASE_CONNECTION_STRING:-}" ]]; then
    database_connection_string="$("${kubectl_command[@]}" get secret mgo2-secrets \
        --namespace mgo2 \
        --output 'jsonpath={.data.DATABASE_CONNECTION_STRING}' | base64 --decode)"
    if [[ -z "$database_connection_string" ]]; then
        printf 'DATABASE_CONNECTION_STRING is missing from mgo2-secrets.\n' >&2
        exit 1
    fi
    export DATABASE_CONNECTION_STRING="$database_connection_string"
    unset database_connection_string
fi

if command -v ss >/dev/null 2>&1 &&
    ss --udp --listening --numeric --no-header | grep -qE "[:.]${gameplay_port}[[:space:]]"; then
    printf 'UDP port %s is already in use.\n' "$gameplay_port" >&2
    exit 1
fi

export DOTNET_USE_POLLING_FILE_WATCHER=true
export GAMEPLAY_SERVER_PORT="$gameplay_port"
export DEDICATED_ADVERTISED_ADDRESS="$dedicated_address"

printf 'Starting local gameplay server on UDP %s; dedicated address %s.\n' \
    "$gameplay_port" "$dedicated_address"
printf 'Using live k3s appsettings and database credentials; dotnet watch is enabled.\n'
printf 'Writing output to %s.\n' "$log_file"

cd "$project_directory"

# Redirect the shell's output through tee before it is replaced by the server,
# so every line reaches the terminal and the log file as it is written rather
# than at exit.
exec > >(tee "$log_file") 2>&1

exec python3 - "$appsettings_json" <<'PY'
import json
import os
import sys

settings = json.loads(sys.argv[1])
environment = os.environ.copy()

for name, value in settings.items():
    if value is not None:
        if isinstance(value, bool):
            value = str(value).lower()
        environment.setdefault(name, str(value))

environment.setdefault("GAMEPLAY_SERVER_LOBBY_NAME", "Free Battle")
environment.setdefault("GAMEPLAY_SERVER_ACCOUNT_NAME", "server")

os.execvpe(
    "dotnet",
    [
        "dotnet",
        "watch",
        "run",
        "--project",
        "src/GameplayServer/Mgo2Server.GameplayServer.csproj",
        "--no-launch-profile",
    ],
    environment,
)
PY
