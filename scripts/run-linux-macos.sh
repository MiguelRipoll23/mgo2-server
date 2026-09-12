#!/usr/bin/env bash
#
# Runs the whole deployment with the .NET SDK instead of containers.
#
# Runs the gate, the account server, one gameplay lobby per process (Free
# Battle, Replays, Survival, Basic Training, Combat Training, Survival Hosts,
# Automatching, Registration, Tournament), a dedicated host, the HTTP API and
# the name server: the same servers compose.yaml starts, minus PostgreSQL.
#
# PostgreSQL is not started by this script. The servers connect to the remote
# database of DATABASE_CONNECTION_STRING, which must be set in .env or in the
# environment. The compose placeholder (Host=postgres) is ignored. Either the
# keyword form Npgsql reads or the postgresql:// URL form Neon and other
# providers hand out is accepted; the URL is translated before it reaches the
# servers.
#
# The schema of an empty database is created by the servers themselves on their
# first start, so nothing has to be applied by hand.
#
# The .NET runtime is what the servers are exec'd through, so the SDK is
# installed into the user profile on the first run when it is missing. The
# binaries themselves are built by scripts/build-linux-macos.sh, which this
# script refuses to start without.
#
# The servers run in the foreground and Ctrl+C stops all of them. In Debug
# builds their output goes to .logs/<server>.log; Release builds write to
# the console only.
#
# Every setting is read from .env, which is created from .env.example on
# the first run and never overwritten. The environment wins over .env.
#
# Usage:
#   scripts/run-linux-macos.sh
#   MGO2_CONFIGURATION=Release scripts/run-linux-macos.sh

set -euo pipefail

project_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_channel="10.0"
configuration="${MGO2_CONFIGURATION:-Debug}"

# Database connection string. Required; set DATABASE_CONNECTION_STRING in the
# environment or in .env. The compose placeholder (Host=postgres) is ignored.

# Settings this script started, so a failure anywhere still stops them.
server_pids=()
server_labels=()
stopping=0

# Copies .env into the process environment, so the servers see the same settings
# they would get from compose. A setting that is already in the environment is
# left alone, which is what lets the shell override .env.
load_env_file() {
    local path="$1"
    [ -f "$path" ] || return 0

    local line name
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line#"${line%%[![:space:]]*}"}"
        line="${line%"${line##*[![:space:]]}"}"
        case "$line" in
            '' | '#'*) continue ;;
        esac

        name="${line%%=*}"
        [ "$name" != "$line" ] || continue
        case "$name" in
            *[!A-Za-z0-9_]*) continue ;;
        esac

        if [ -z "${!name+x}" ]; then
            export "$name=${line#*=}"
        fi
    done < "$path"
}

# Turns %XX escapes and '+' back into the bytes they stand for.
url_decode() {
    local value="${1//+/ }"
    printf '%b' "${value//%/\\x}"
}

# Rewrites a postgresql:// URL as the keyword=value string Npgsql reads,
# percent-decoding the credentials and the database name and carrying the
# parameters it knows over. A string that is already keyword=value is returned
# untouched.
convert_to_npgsql_connection_string() {
    local connection_string="$1"
    if [[ ! "$connection_string" =~ ^[[:space:]]*postgres(ql)?:// ]]; then
        printf '%s' "$connection_string"
        return 0
    fi

    local remainder="${connection_string#*://}"

    local query=""
    if [[ "$remainder" == *"?"* ]]; then
        query="${remainder#*\?}"
        remainder="${remainder%%\?*}"
    fi

    local path=""
    if [[ "$remainder" == *"/"* ]]; then
        path="${remainder#*/}"
        remainder="${remainder%%/*}"
    fi

    local userinfo="" hostport="$remainder"
    if [[ "$remainder" == *"@"* ]]; then
        userinfo="${remainder%@*}"
        hostport="${remainder##*@}"
    fi

    local host="${hostport%%:*}" port=""
    if [[ "$hostport" == *":"* ]]; then
        port="${hostport##*:}"
    fi

    local settings=("Host=$host")
    [ -n "$port" ] && settings+=("Port=$port")
    [ -n "$path" ] && settings+=("Database=$(url_decode "$path")")

    if [ -n "$userinfo" ]; then
        settings+=("Username=$(url_decode "${userinfo%%:*}")")
        if [[ "$userinfo" == *":"* ]]; then
            settings+=("Password=$(url_decode "${userinfo#*:}")")
        fi
    fi

    local parameter name value
    if [ -n "$query" ]; then
        local IFS='&'
        for parameter in $query; do
            [ -n "$parameter" ] || continue

            name="${parameter%%=*}"
            value=""
            if [[ "$parameter" == *"="* ]]; then
                value="${parameter#*=}"
            fi

            case "${name,,}" in
                sslmode) settings+=("SSL Mode=$(url_decode "$value")") ;;
                channel_binding) settings+=("Channel Binding=$(url_decode "$value")") ;;
                application_name) settings+=("Application Name=$(url_decode "$value")") ;;
                connect_timeout) settings+=("Timeout=$(url_decode "$value")") ;;
                *) echo "warning: '${name,,}' of the connection string has no Npgsql equivalent and is ignored" >&2 ;;
            esac
        done
    fi

    local result="" item
    for item in "${settings[@]}"; do
        if [ -z "$result" ]; then
            result="$item"
        else
            result="$result;$item"
        fi
    done
    printf '%s' "$result"
}

# Reports the connection string of the remote database: the one in the
# environment or the one in .env, ignoring the compose placeholder.
resolve_database_connection_string() {
    local connection_string="${DATABASE_CONNECTION_STRING:-}"

    if [ -z "$connection_string" ] ||
        [[ "$connection_string" =~ (^|;)[[:space:]]*Host=postgres[[:space:]]*(;|$) ]]; then
        echo 'error: DATABASE_CONNECTION_STRING is not set or is the compose placeholder. Set it in .env or in the environment' >&2
        exit 1
    fi

    connection_string="$(convert_to_npgsql_connection_string "$connection_string")"

    printf 'Using the remote database %s\n' \
        "$(printf '%s' "$connection_string" | sed -E 's/(Password=)[^;]*/\1***/I')" >&2
    printf '%s' "$connection_string"
}

# Reports whether a dotnet command able to build net10.0 is on the PATH.
test_dotnet_usable() {
    command -v dotnet >/dev/null 2>&1 || return 1

    local version
    version="$(dotnet --version 2>/dev/null | head -n1)"
    [[ "$version" =~ ^(1[0-9]|[2-9][0-9])\. ]]
}

# Puts a locally installed SDK on the PATH, whether it was installed by this
# script or by the user.
add_dotnet_to_path() {
    local install_directory="$1"
    [ -x "$install_directory/dotnet" ] || return 0

    export DOTNET_ROOT="$install_directory"
    export PATH="$install_directory:$PATH"
}

# Installs the .NET SDK the projects target when the machine has none.
install_dotnet() {
    local install_directory="$1"
    local installer_directory
    installer_directory="$(mktemp -d)"
    local installer_path="$installer_directory/dotnet-install.sh"

    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer_path"

    echo "The .NET SDK was not found; installing .NET $dotnet_channel into $install_directory"
    bash "$installer_path" --channel "$dotnet_channel" --install-dir "$install_directory"

    rm -rf "$installer_directory"
}

# Installs the .NET SDK when the machine has none, and reports the version that
# will build the projects.
initialize_dotnet() {
    local install_directory="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"

    add_dotnet_to_path "$install_directory"
    if test_dotnet_usable; then
        return 0
    fi

    install_dotnet "$install_directory"
    add_dotnet_to_path "$install_directory"

    if ! test_dotnet_usable; then
        echo 'the .NET SDK was installed but is not usable; open a new shell and run again' >&2
        exit 1
    fi

    echo "Installed $(dotnet --version)"
}

# Starts one built server. The settings are name=value pairs applied to this
# one process only. In Debug builds stdout and stderr are merged into a single
# log file under .logs/; Release builds write to the console.
start_server() {
    local name="$1" project="$2" label="$3"
    shift 3

    local dll="${project_directory}/src/${project}/bin/${configuration}/net10.0/Mgo2Server.${project}.dll"
    if [ ! -f "$dll" ]; then
        echo "$dll was not built; run scripts/build-linux-macos.sh first" >&2
        exit 1
    fi

    local log_file="/dev/null"
    if [ -n "$log_directory" ]; then
        log_file="${log_directory}/${name}.log"
    fi

    (
        while [ "$#" -gt 0 ]; do
            export "$1"
            shift
        done
        # The working directory is the project root because the HTTP API reads
        # its policy document from ./static. In Debug builds stdout and stderr
        # are merged into a single log file; Release builds write to the console.
        exec dotnet "$dll"
    ) >"${log_file}" 2>&1 &

    server_pids+=("$!")
    server_labels+=("$label")
}

# Stops every server this script started.
stop_servers() {
    if [ "$stopping" -eq 1 ] || [ "${#server_pids[@]}" -eq 0 ]; then
        return 0
    fi

    stopping=1
    echo ''
    echo 'Stopping every server'

    local pid
    for pid in "${server_pids[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    for pid in "${server_pids[@]}"; do
        wait "$pid" 2>/dev/null || true
    done
}

trap stop_servers EXIT INT TERM

cd "$project_directory"

if [ ! -f .env ]; then
    cp .env.example .env
    echo 'Created .env from .env.example. Review it before exposing the deployment.'
fi

load_env_file "${project_directory}/.env"
DATABASE_CONNECTION_STRING="$(resolve_database_connection_string)"
export DATABASE_CONNECTION_STRING

log_directory=""
if [ "$configuration" = "Debug" ]; then
    log_directory="${project_directory}/.logs"
    mkdir -p "$log_directory"
fi

initialize_dotnet

http_port="${HTTP_PORT:-80}"
dns_port="${DNS_PORT:-53}"
launcher_server="${LAUNCHER_SERVER:-http://mgo2pc.com}"

echo ''
echo 'Starting every server'

start_server gate-lobby-5731 GateLobbyServer 'gate (5731/tcp)'
start_server account-lobby-5732 AccountLobbyServer 'account (5732/tcp)'

# One process per gameplay lobby, with the identity and the attributes the
# compose file gives each container: name|subtype|port.
lobbies=(
    'Free Battle|FREE BATTLE|5733'
    'Replays|TRAINING|5734'
    'Survival|SURVIVAL|5735'
    'Basic Training|TRAINING|5737'
    'Combat Training|TRAINING|5738'
    'Survival Hosts|UNKNOWN|5739'
    'Automatching|AUTOMATCHING|5740'
    'Registration|TOURNAMENT REGISTRATION|5741'
    'Tournament|TOURNAMENT|5742'
)

for lobby in "${lobbies[@]}"; do
    IFS='|' read -r lobby_name lobby_subtype lobby_port <<< "$lobby"
    start_server "game-lobby-${lobby_port}" GameLobbyServer "${lobby_name} (${lobby_port}/tcp)" \
        "LOBBY_NAME=${lobby_name}" \
        "LOBBY_SUBTYPE=${lobby_subtype}" \
        "LOBBY_PORT=${lobby_port}" \
        'LOBBY_BEGINNER_ONLY=false' \
        'LOBBY_EXPANSION_ONLY=false' \
        'LOBBY_NO_HEADSHOT=false' \
        'LOBBY_REPLAYS_ONLY=false'
done

start_server gameplay-5730 GameplayServer 'dedicated host (5730/udp)' \
    'DEDICATED_HOST_PORT=5730' \
    'DEDICATED_HOST_LOBBY_NAME=Free Battle' \
    'P2P_HOST=127.0.0.1'

start_server http Http "HTTP API (${http_port}/tcp)" \
    "HTTP_PORT=${http_port}" \
    "LAUNCHER_SERVER=${launcher_server}"

start_server dns Dns "name server (${dns_port}/udp)" \
    "DNS_PORT=${dns_port}"

# Give the processes a moment to fail fast before the state is reported.
sleep 3

expected="${#server_pids[@]}"
running=0
index=0
while [ "$index" -lt "$expected" ]; do
    if kill -0 "${server_pids[$index]}" 2>/dev/null; then
        running=$((running + 1))
        echo "  running  ${server_labels[$index]}"
    else
        echo "  stopped  ${server_labels[$index]}"
    fi
    index=$((index + 1))
done

echo ''
if [ "$running" -ne "$expected" ]; then
    hint=""
    if [ -n "$log_directory" ]; then
        hint="; see $log_directory"
    fi
    echo "error: $running of $expected servers are running${hint}" >&2
fi

echo "Started $running of $expected servers."
echo "The gate listens on 5731, the account server on 5732 and the HTTP API on $http_port."
if [ -n "$log_directory" ]; then
    echo "Logs are in $log_directory; press Ctrl+C to stop every server."
else
    echo "Press Ctrl+C to stop every server."
fi

# Stays in the foreground until the last server exits or the user interrupts it,
# which is when the servers are stopped.
wait "${server_pids[@]}" 2>/dev/null || true

stop_servers
echo 'Every server has stopped'
