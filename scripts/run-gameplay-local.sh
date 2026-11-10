#!/usr/bin/env bash
#
# Runs the gameplay server on this machine against the gameplay cluster on the
# Raspberry Pi.
#
# The settings and database credentials are read from the cluster itself, so a run
# always matches the deployed one: the mgo2-appsettings ConfigMap and the
# DATABASE_CONNECTION_STRING in the mgo2-secrets secret. Three things change for a
# local run, and nothing else:
#
#   * the server listens on GAMEPLAY_SERVER_PORT;
#   * it advertises this machine's LAN address, because the lobby hands it to the
#     console;
#   * postgres is reached through an SSH tunnel on 127.0.0.1:DATABASE_TUNNEL_PORT in
#     front of the Pi's own port-forward, because the service is published inside
#     the Pi's cluster only. The tunnel is opened for the run and closed with it.
#
# Anything already set in the environment wins over the ConfigMap, so a setting can
# be tried for one run without editing it. The host character and its game are named
# after the local port unless the environment names them.
#
# Two settings are this run's own and never the cluster's. GAMEPLAY_LOG_LEVEL is
# the level the run logs at — Debug by default, Trace for every datagram in and out
# — and it is exported as LOG_LEVEL before the ConfigMap is merged, so the level a
# pod logs at stays on the pod. GAMEPLAY_SERVER_ROTATION is one round, TDM on Blood
# Bath, because a local run is played to watch a round and not the deployed
# rotation of nine.
#
# The output is written to a log file as it arrives, through tee, so the run can be
# read back after the terminal has scrolled or the process has stopped. The file is
# truncated at the start of each run; set GAMEPLAY_SERVER_LOG to write it elsewhere.
#
# Usage:
#   scripts/run-gameplay-local.sh
#   GAMEPLAY_SERVER_PORT=9999 scripts/run-gameplay-local.sh
#   PI_HOST=miguel@192.168.1.20 scripts/run-gameplay-local.sh
#   GAMEPLAY_LOG_LEVEL=Trace scripts/run-gameplay-local.sh
#   GAMEPLAY_SERVER_ROTATION='CAP@Coppertown, TDM@Blood Bath' scripts/run-gameplay-local.sh
#
# The rotation and the round the run is staged at are meant to be edited in
# scripts/gameplay-local-overrides.conf, which this run reads when it exists, so a
# round can be tried without editing this file. Set GAMEPLAY_LOCAL_OVERRIDES to
# read another file instead.

# Run under bash even when the file is started with sh. The run needs bash arrays,
# process substitution and pipefail, and a POSIX shell rejects the heredoc inside
# the process substitution as a syntax error before it reaches it.
if [ -z "${BASH_VERSION:-}" ]; then
    exec bash "$0" "$@"
fi

set -euo pipefail

project_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pi_host="${PI_HOST:-miguel@raspberrypi.local}"
pi_database_port="${PI_DATABASE_PORT:-5432}"
tunnel_port="${DATABASE_TUNNEL_PORT:-15432}"
gameplay_port="${GAMEPLAY_SERVER_PORT:-9999}"
log_file="${GAMEPLAY_SERVER_LOG:-${TMPDIR:-/tmp}/mgo2-gameplay-server-${gameplay_port}.log}"
override_file="${GAMEPLAY_LOCAL_OVERRIDES:-$project_directory/scripts/gameplay-local-overrides.conf}"
tunnel_pid=""

abort() {
    printf '%s\n' "$*" >&2
    exit 1
}

cleanup() {
    if [[ -n "$tunnel_pid" ]] && kill -0 "$tunnel_pid" 2>/dev/null; then
        kill "$tunnel_pid" 2>/dev/null || true
        wait "$tunnel_pid" 2>/dev/null || true
        printf 'Closed the tunnel to %s.\n' "$pi_host"
    fi
}
trap cleanup EXIT INT TERM

port_listening() {
    (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null
}

wait_for_port() {
    for _ in 1 2 3 4 5 6 7 8 9 10; do
        port_listening "$1" && return 0
        sleep 1
    done
    return 1
}

if ! [[ "$gameplay_port" =~ ^[0-9]+$ ]] || (( gameplay_port < 1 || gameplay_port > 65535 )); then
    abort "Invalid GAMEPLAY_SERVER_PORT: $gameplay_port"
fi

if ! [[ "$tunnel_port" =~ ^[0-9]+$ ]] || (( tunnel_port < 1 || tunnel_port > 65535 )); then
    abort "Invalid DATABASE_TUNNEL_PORT: $tunnel_port"
fi

command -v dotnet >/dev/null 2>&1 || abort "The .NET SDK is required to run the gameplay server."
command -v python3 >/dev/null 2>&1 || abort "python3 is required to merge the ConfigMap settings into the run."
command -v ssh >/dev/null 2>&1 || abort "ssh is required to reach the database on $pi_host."

if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iUDP:"$gameplay_port" >/dev/null 2>&1 &&
        abort "UDP port $gameplay_port is already in use; stop that server or pick another port."
elif command -v ss >/dev/null 2>&1; then
    ss --udp --listening --numeric --no-header | grep -qE "[:.]${gameplay_port}[[:space:]]" &&
        abort "UDP port $gameplay_port is already in use; stop that server or pick another port."
fi

advertised_address="${ADVERTISED_ADDRESS:-}"
if [[ -z "$advertised_address" ]]; then
    if command -v ipconfig >/dev/null 2>&1; then
        for interface in en0 en1 en2; do
            advertised_address="$(ipconfig getifaddr "$interface" 2>/dev/null || true)"
            [[ -n "$advertised_address" ]] && break
        done
    else
        advertised_address="$(hostname -I 2>/dev/null | awk '{print $1}')"
    fi
fi
[[ -n "$advertised_address" ]] ||
    abort "Cannot determine this machine's LAN address; set ADVERTISED_ADDRESS."

remote_kubectl() {
    ssh -o BatchMode=yes -o ConnectTimeout=10 "$pi_host" "sudo -n kubectl $1"
}

settings_json="$(remote_kubectl "get configmap mgo2-appsettings --namespace mgo2 \
    --output 'jsonpath={.data.appsettings\.json}'")" ||
    abort "Cannot read mgo2-appsettings from the cluster on $pi_host."
database_connection_string="$(remote_kubectl "get secret mgo2-secrets --namespace mgo2 \
    --output 'jsonpath={.data.DATABASE_CONNECTION_STRING}'" | base64 --decode)" ||
    abort "Cannot read mgo2-secrets from the cluster on $pi_host."

[[ -n "$settings_json" ]] || abort "The mgo2-appsettings ConfigMap has no appsettings.json."
[[ -n "$database_connection_string" ]] ||
    abort "DATABASE_CONNECTION_STRING is missing from mgo2-secrets."

# The row a browsing client reads names the round this host is on, so the local
# run has to be staged where the deployed one is. Its place in the rotation is
# the deployment's own: the ConfigMap's appsettings.json carries no GAMEPLAY_*
# key, and `mgo2-gameplay-1` states GAMEPLAY_SERVER_START_ROUND=5 in its
# container environment. That is the fifth entry of the rotation both runs share
# — CAP on Blood Bath — so it is read from the deployment rather than assumed,
# and a manifest that stages the room elsewhere stages this run with it.
gameplay_deployment="${PI_GAMEPLAY_DEPLOYMENT:-mgo2-gameplay-1}"
deployed_environment="$(remote_kubectl "get deployment $gameplay_deployment \
    --namespace mgo2 --output json")" || deployed_environment=""
deployed_start_round="$(
    printf '%s' "$deployed_environment" | python3 -c '
import json
import sys

try:
    deployment = json.load(sys.stdin)
    container = deployment["spec"]["template"]["spec"]["containers"][0]
except Exception:
    print("")
    raise SystemExit(0)

for entry in container.get("env") or []:
    if entry.get("name") == "GAMEPLAY_SERVER_START_ROUND":
        print(entry.get("value", ""))
        break
else:
    print("")
'
)"
unset deployed_environment

# The ConfigMap's appsettings.json is the other place the cluster can state it,
# and a value written there is the one the deployment is configured with, so it
# is preferred over the deployment's own when both exist.
configured_start_round="$(
    printf '%s' "$settings_json" | python3 -c '
import json
import sys

try:
    settings = json.load(sys.stdin)
except Exception:
    print("")
    raise SystemExit(0)

value = settings.get("GAMEPLAY_SERVER_START_ROUND")
print("" if value is None else value)
'
)"
[[ -n "$configured_start_round" || -n "$deployed_start_round" ]] ||
    printf '\nNo GAMEPLAY_SERVER_START_ROUND in the ConfigMap or on %s, so the room starts at round 1.\n' \
        "$gameplay_deployment" >&2

if port_listening "$tunnel_port"; then
    printf 'Reusing the postgres tunnel already listening on 127.0.0.1:%s.\n' "$tunnel_port"
else
    # The forward is probed by its listener and not by its process: a
    # `pgrep -f` on the command line matches the remote shell that carries the
    # pattern, so it answered "already forwarding" for a forward that a pod
    # restart had already taken down, and the tunnel then opened onto a dead
    # port.
    if ssh -o BatchMode=yes -o ConnectTimeout=5 "$pi_host" \
        "ss -ltn 2>/dev/null | grep -qE '[:.]${pi_database_port}[[:space:]]'"; then
        printf 'The Pi already forwards postgres to its own port %s.\n' "$pi_database_port"
    else
        printf 'Starting the postgres port-forward on %s.\n' "$pi_host"
        ssh -o BatchMode=yes -o ConnectTimeout=10 "$pi_host" \
            "sudo -n nohup kubectl port-forward -n mgo2 svc/mgo2-postgres \
            $pi_database_port:$pi_database_port >/tmp/mgo2-port-forward.log 2>&1 &" ||
            abort "Cannot start the postgres port-forward on $pi_host."
        sleep 3
    fi

    printf 'Opening a tunnel from 127.0.0.1:%s to %s port %s.\n' \
        "$tunnel_port" "$pi_host" "$pi_database_port"
    ssh -N -o BatchMode=yes -o ExitOnForwardFailure=yes \
        -L "$tunnel_port:127.0.0.1:$pi_database_port" "$pi_host" &
    tunnel_pid=$!
    wait_for_port "$tunnel_port" ||
        abort "The tunnel on 127.0.0.1:$tunnel_port did not come up."
fi

export ADVERTISED_ADDRESS="$advertised_address"
export DEDICATED_ADVERTISED_ADDRESS="${DEDICATED_ADVERTISED_ADDRESS:-$advertised_address}"
export GAMEPLAY_SERVER_PORT="$gameplay_port"

# The log level is this run's own and not the cluster's. LOG_LEVEL is exported
# before the ConfigMap is merged, and the merge only fills names the environment
# has not set, so the level a pod logs at never reaches a local run. Debug is what
# a run is read at: what each service made of a payload, and the decisions between
# them. Trace adds the wire — every datagram in and out, with its hex.
export LOG_LEVEL="${GAMEPLAY_LOG_LEVEL:-Debug}"

export GAMEPLAY_SERVER_CHARACTER_NAME="${GAMEPLAY_SERVER_CHARACTER_NAME:-server-$gameplay_port}"
export GAMEPLAY_SERVER_GAME_NAME="${GAMEPLAY_SERVER_GAME_NAME:-server-$gameplay_port}"

# The local room is the bot room: bots on both sides, filling the slots the humans
# leave and giving them up when one joins. The count is this run's own, and the
# stage directory is the client's install, so navigation reads the stage the round
# is played on out of its own GEOM. A run that points the directory elsewhere gets
# navigation that says it has no data, rather than a route nothing measured.
export BOT_TEAM="${BOT_TEAM:-BOTH}"
export BOT_POPULATION_MODE="${BOT_POPULATION_MODE:-FILL}"
export BOT_COUNT="${BOT_COUNT:-8}"
export BOT_POPULATION_INTERVAL_SECONDS="${BOT_POPULATION_INTERVAL_SECONDS:-5}"

# The round the room is staged with is read from the overrides file when there is
# one, so a round is tried by editing that file and never this one. What the file
# leaves out falls back to the room's own values: the rotation is then one round,
# TDM on Blood Bath, because a local run is played to watch a round and not the
# deployed nine, and the start round is the deployment's own place in the
# rotation, read from the cluster above — the ConfigMap's value first, then the
# deployment's, which is 5 (CAP on Blood Bath) on mgo2-gameplay-1. Entry zero would
# brief on a different round than the room this run is compared with, and the row
# it publishes would name that wrong round to every browsing client.
if [[ -f "$override_file" ]]; then
    # shellcheck source=/dev/null
    . "$override_file"
    printf 'Read the round overrides from %s.\n' "$override_file"
fi

export GAMEPLAY_SERVER_START_ROUND="${GAMEPLAY_SERVER_START_ROUND:-${configured_start_round:-${deployed_start_round:-1}}}"
export GAMEPLAY_SERVER_ROTATION="${GAMEPLAY_SERVER_ROTATION:-TDM@Blood Bath}"

export DATABASE_CONNECTION_STRING="$database_connection_string"
unset database_connection_string

printf 'Starting the gameplay server on UDP %s, advertising %s, staged at round %s, playing %s, logging at %s.\n' \
    "$gameplay_port" "$advertised_address" "$GAMEPLAY_SERVER_START_ROUND" \
    "$GAMEPLAY_SERVER_ROTATION" "$LOG_LEVEL"
printf 'Writing output to %s; press Control-C to stop the server.\n' "$log_file"

# The ConfigMap's settings become the environment, except where the environment
# already named one, and the connection string is pointed at the local tunnel
# rather than at the in-cluster service name. Each assignment is read back as a
# NUL-terminated pair so a value's spaces and quotes survive intact.
while IFS= read -r -d '' assignment; do
    export "$assignment"
done < <(python3 - "$settings_json" "$tunnel_port" <<'PY'
import json
import os
import sys

settings = json.loads(sys.argv[1])
forward_port = sys.argv[2]
environment = os.environ.copy()

for name, value in settings.items():
    if value is not None:
        if isinstance(value, bool):
            value = str(value).lower()
        environment.setdefault(name, str(value))

connection = environment.get("DATABASE_CONNECTION_STRING", "")
if connection:
    pairs = []
    for pair in connection.split(";"):
        key, separator, _ = pair.partition("=")
        if separator and key.strip().lower() == "host":
            pair = "Host=127.0.0.1"
        elif separator and key.strip().lower() == "port":
            pair = "Port=" + forward_port
        if pair:
            pairs.append(pair)
    environment["DATABASE_CONNECTION_STRING"] = ";".join(pairs)

for name in sorted(environment):
    sys.stdout.write("%s=%s\0" % (name, environment[name]))
PY
)

cd "$project_directory"

set +e
dotnet run --project src/GameplayServer/Mgo2Server.GameplayServer.csproj --no-launch-profile 2>&1 |
    tee "$log_file"
server_status=${PIPESTATUS[0]}
set -e
exit "$server_status"
