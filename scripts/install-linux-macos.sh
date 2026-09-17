#!/usr/bin/env bash
#
# Installs the whole deployment from the published container images: the gate,
# the account server, one gameplay lobby per container (Free Battle, Replays,
# Survival, Basic Training, Combat Training, Survival Hosts, Automatching,
# Registration, Tournament), a gameplay server, the HTTP API, the name server, the
# port-check responder and PostgreSQL.
#
# Linux and macOS. Windows runs scripts/install-windows.ps1 instead.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
#   scripts/install-linux-macos.sh                          # registry prefix from MGO2_IMAGE_PREFIX
#   scripts/install-linux-macos.sh ghcr.io/owner/repo       # registry prefix from the argument
#
# Running it again installs the update: it refreshes the compose file, pulls the
# newest images, recreates the containers whose image or configuration changed
# and removes old dangling images, so a new release is one command away.
#
# The script runs against a clone when it is started from one; otherwise it
# creates a deployment directory in a platform default and downloads
# compose.yaml and appsettings.example.json into it. Everything else is
# configured in appsettings.json, which is created from appsettings.example.json
# on the first run with a random JWT_SECRET and never overwritten afterwards,
# except for ADVERTISED_ADDRESS: the script detects the private address of this
# machine and writes it there so clients on the network can reach the published
# ports.
#
# The deployment directory holds compose.yaml and appsettings.json next to each
# other, because the compose file mounts ./appsettings.json into the containers:
# the directory is the deployment. Running as root installs the machine-wide one,
# /opt/mgo2; running as a user installs the user one, ~/.local/share/mgo2 on
# Linux and ~/Library/Application Support/mgo2 on macOS. ADVERTISED_ADDRESS
# skips address detection.

set -euo pipefail

# Where a deployment that is not run from a clone comes from, and the registry
# the CI workflow publishes the images to.
readonly default_source_url="https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main"
readonly default_image_prefix="ghcr.io/miguelripoll23/mgo2-server/"
readonly root_deployment_directory="/opt/mgo2"

usage() {
    cat <<'TEXT'
usage: scripts/install-linux-macos.sh [registry-prefix]

  registry-prefix  registry path the images are pulled from, including the
                   trailing slash, for example
                   ghcr.io/your-account/your-repository/

  The script detects the private address of this machine and writes
  ADVERTISED_ADDRESS into appsettings.json. Setting ADVERTISED_ADDRESS answers
  without detection.

  The servers log at Warning by default (LOG_LEVEL of appsettings.json changes
  it) and always send their metrics over OpenTelemetry: appsettings.example.json
  ships OTEL_ENABLED=true and OTEL_PORT=4317, which the script leaves alone.
  The collector the metrics are sent to is the operator's change: nothing of it
  is touched or checked here.

  When it is omitted, MGO2_IMAGE_PREFIX is used: the environment variable, or
  the registry the images are published to by default.

  Without a clone the deployment is installed into /opt/mgo2 when the script
  runs as root, and into ~/.local/share/mgo2 (Linux) or ~/Library/Application
  Support/mgo2 (macOS) otherwise.

  The script pulls the images of every container (the gate, the account server,
  the nine gameplay lobbies, a gameplay server, the HTTP API, the name server, the
  port-check responder and PostgreSQL) and installs them. Running it again
  installs the update.
TEXT
}

# Reports the deployment directory a run that is not started from a clone
# installs into: /opt/mgo2 for the machine-wide install of a root run, and the
# user one of $HOME otherwise. The user home of a sudo run is the one of the
# user behind sudo, so an install of a piped script run with sudo does not land
# in the root home.
resolve_deployment_directory() {
    local home="${HOME:-}"

    if [ "$(id -u)" = '0' ] && [ -n "${SUDO_USER:-}" ] && [ "${SUDO_USER}" != 'root' ]; then
        local sudo_home=''
        sudo_home="$(su -s /bin/sh -c 'printf %s "$HOME"' "${SUDO_USER}" 2>/dev/null || true)"
        if [ -z "${sudo_home}" ]; then
            case "${SUDO_USER}" in
                *[!a-zA-Z0-9._-]*) ;;
                *) sudo_home="$(eval "echo ~${SUDO_USER}" 2>/dev/null || true)" ;;
            esac
        fi
        case "${sudo_home}" in
            '' | /root | /var/root) ;;
            *) home="${sudo_home}" ;;
        esac
    fi

    if [ "$(id -u)" = '0' ] && [ "${home}" = '/root' -o "${home}" = '/var/root' ]; then
        printf '%s' "${root_deployment_directory}"
        return 0
    fi

    if [ -z "${home}" ]; then
        printf '%s' "${root_deployment_directory}"
        return 0
    fi

    case "$(uname -s 2>/dev/null || true)" in
        Darwin)
            printf '%s' "${home}/Library/Application Support/mgo2"
            ;;
        *)
            printf '%s' "${home}/.local/share/mgo2"
            ;;
    esac
}

# Prints the value of a setting of appsettings.json, or nothing when it is not
# set. A quoted string and a bare number or boolean are both read; a quoted
# value comes back without the surrounding quotes.
read_json_value() {
    local name="$1" value
    [ -f "${project_directory}/appsettings.json" ] || return 0
    value="$(sed -n "s|.*\"${name}\": \(.*\)$|\1|p" "${project_directory}/appsettings.json" | tail -n 1 | tr -d '\r')"
    value="${value%,}"
    case "$value" in
        \"*\") value="${value#\"}"; value="${value%\"}" ;;
    esac
    printf '%s\n' "$value"
}

# Reports the private IPv4 address of this machine, or nothing when it cannot be
# determined. The route lookup picks the interface the default route uses, which
# is the one the clients on the network reach.
detect_private_ipv4() {
    local address=''

    if command -v ip >/dev/null 2>&1; then
        address="$(ip -4 route get 1.1.1.1 2>/dev/null \
            | awk '{ for (i = 1; i <= NF; i++) if ($i == "src") { print $(i + 1); exit } }' || true)"
    fi

    if [ -z "$address" ] && command -v hostname >/dev/null 2>&1; then
        address="$(hostname -I 2>/dev/null | awk '{ print $1 }' || true)"
    fi

    if [ -z "$address" ] && [ "$(uname -s 2>/dev/null || true)" = 'Darwin' ] &&
        command -v ipconfig >/dev/null 2>&1; then
        address="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || true)"
    fi

    # A tool that was not recognised can print anything, so only a well formed
    # address is reported.
    if ! is_ipv4 "$address"; then
        address=''
    fi

    printf '%s' "$address"
}

# Answers true for a dotted IPv4 address whose four octets each fit in a byte.
is_ipv4() {
    [ -n "$1" ] || return 1

    printf '%s' "$1" | awk -F. '
        NF != 4 { exit 1 }
        { for (i = 1; i <= 4; i++) if ($i !~ /^[0-9]{1,3}$/ || $i > 255) exit 1 }
    '
}

# Sets a setting of appsettings.json, replacing its value or inserting the key
# before the closing brace when it is absent, and leaves every other setting
# alone. A quoted value is written between double quotes; a bare value (a
# number or a boolean) is not.
set_json_value() {
    local name="$1" value="$2" quoted="$3" settings_file="$4"
    local escaped replacement

    escaped="$(printf '%s' "$value" | sed 's/[&|]/\\&/g')"
    if [ "$quoted" = 'true' ]; then
        replacement="\"${name}\": \"${escaped}\","
    else
        replacement="\"${name}\": ${escaped},"
    fi

    if grep -q "\"${name}\":" "$settings_file"; then
        sed -i.bak "s|\"${name}\": .*|${replacement}|" "$settings_file"
        rm -f "${settings_file}.bak"
    else
        # Inserts the key before the closing brace, and gives the setting that
        # precedes it the comma the insertion takes away.
        awk -v line="  ${replacement%,}" '
            { lines[NR] = $0 }
            END {
                for (i = 1; i <= NR; i++) {
                    if (lines[i] == "}" && i > 1 &&
                        lines[i - 1] !~ /,[[:space:]]*$/) {
                        lines[i - 1] = lines[i - 1] ","
                    }
                }
                for (i = 1; i <= NR; i++) {
                    if (lines[i] == "}") print line
                    print lines[i]
                }
            }' "$settings_file" > "${settings_file}.tmp"
        mv "${settings_file}.tmp" "$settings_file"
    fi
}

# Reports the private address clients are told to connect to. An empty answer
# means nothing was decided, so an existing value is kept.
resolve_advertised_ip() {
    # The environment decides without detection, which is what a piped or
    # otherwise unattended run needs.
    if [ -n "${ADVERTISED_ADDRESS+x}" ]; then
        printf '%s' "$ADVERTISED_ADDRESS"
        return 0
    fi

    local current detected
    current="$(read_json_value ADVERTISED_ADDRESS)"

    detected="$(detect_private_ipv4)"
    if [ -n "$detected" ]; then
        printf '%s' "$detected"
        return 0
    fi

    if is_ipv4 "$current"; then
        printf '%s' "$current"
        return 0
    fi

    echo 'warning: the private address of this machine could not be detected; set ADVERTISED_ADDRESS in appsettings.json' >&2
    printf '%s' ''
}

# Adds the trailing slash the compose file expects, and nothing when empty.
normalise_prefix() {
    local value="$1"
    [ -n "$value" ] || return 0
    printf '%s/' "${value%/}"
}

# Copies a file of the published deployment into the deployment directory.
download_deployment_file() {
    local name="$1"
    local target="${project_directory}/${name}"

    if command -v curl >/dev/null 2>&1; then
        curl --fail --silent --show-error --location "${source_url}/${name}" --output "${target}"
    elif command -v wget >/dev/null 2>&1; then
        wget --quiet --output-document="${target}" "${source_url}/${name}"
    else
        echo "error: neither curl nor wget was found, so ${name} cannot be downloaded" >&2
        exit 1
    fi

    echo "Downloaded ${name} from ${source_url}"
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
    usage
    exit 0
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "error: the docker command was not found; install Docker first" >&2
    exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
    echo "error: the docker compose plugin was not found; it ships with Docker Desktop" >&2
    echo "       and with the docker-compose-plugin package on Linux" >&2
    exit 1
fi

# The first pull fails with a bare permission error when the daemon is not
# reachable, so the access is checked here with the answer ready.
docker_info_error=''
if ! docker_info_error="$(docker info 2>&1 >/dev/null)"; then
    echo 'error: the docker daemon is not reachable; start Docker first' >&2
    case "${docker_info_error}" in
        *'permission denied'*)
            echo "       this user cannot reach the docker daemon: add it to the docker group with" >&2
            echo "       'sudo usermod -aG docker $(id -un)' and log in again, or run this script with" >&2
            echo "       sudo, which installs the machine-wide deployment into /opt/mgo2" >&2
            ;;
    esac
    exit 1
fi

# A script started from disk next to a compose file runs against that clone; a
# piped script (curl ... | bash) downloads the deployment instead.
script_path="${BASH_SOURCE[0]:-}"
script_directory=""
if [ -n "${script_path}" ] && [ -f "${script_path}" ]; then
    script_directory="$(cd "$(dirname "${script_path}")" && pwd)"
fi

if [ -n "${script_directory}" ] && [ -f "${script_directory}/../compose.yaml" ]; then
    project_directory="$(cd "${script_directory}/.." && pwd)"
    downloads_deployment=false
elif [ -f "${PWD}/compose.yaml" ]; then
    project_directory="${PWD}"
    downloads_deployment=false
else
    project_directory="$(resolve_deployment_directory)"
    downloads_deployment=true
fi

source_url="${MGO2_SOURCE_URL:-${default_source_url}}"

if [ "${downloads_deployment}" = "true" ]; then
    mkdir --parents "${project_directory}"
    echo "Installing into ${project_directory}"
    download_deployment_file compose.yaml
    download_deployment_file appsettings.example.json
fi

cd "${project_directory}"

if [ ! -f appsettings.json ]; then
    cp appsettings.example.json appsettings.json
    jwt_secret="$(openssl rand -base64 48)"
    set_json_value JWT_SECRET "$jwt_secret" true appsettings.json
    echo "Created appsettings.json from appsettings.example.json with a random JWT_SECRET. Review it before exposing the deployment."
fi

resolved_advertised_ip="$(resolve_advertised_ip)"
if [ -n "$resolved_advertised_ip" ]; then
    set_json_value ADVERTISED_ADDRESS "$resolved_advertised_ip" true appsettings.json
fi

# Telemetry is always on: appsettings.example.json ships OTEL_ENABLED=true and
# OTEL_PORT=4317, so nothing has to be written here. The collector the metrics
# are sent to is the operator's change: nothing of it is touched or checked
# here. The log level is left alone as well: the example ships Warning, and a
# deployment that changed it keeps it across updates.
echo
echo 'Telemetry is on: the servers send their metrics over gRPC on port 4317.'
echo 'Change OTEL_PORT in appsettings.json to point them elsewhere; the collector has to listen on the same one.'

image_prefix="$(normalise_prefix "${1:-${MGO2_IMAGE_PREFIX:-}}")"
image_prefix="${image_prefix:-$(normalise_prefix "${default_image_prefix}")}"
image_tag="${MGO2_IMAGE_TAG:-latest}"

# The exported variables point compose at the chosen registry.
export MGO2_IMAGE_PREFIX="${image_prefix}"
export MGO2_IMAGE_TAG="${image_tag}"

echo "Installing the images of ${image_prefix} (tag ${image_tag})"
docker compose pull

echo
echo "Starting every container and waiting for them to come up"
docker compose up --detach --remove-orphans --wait --wait-timeout 180

echo
echo "Removing old images left behind by the update"
docker image prune --force

# Every service is expected to be running once the deployment is up.
expected="$(docker compose config --services | wc -l | tr -d ' ')"
running="$(docker compose ps --status running --services | wc -l | tr -d ' ')"

echo
docker compose ps

if [ "${running}" != "${expected}" ]; then
    echo >&2
    echo "error: ${running} of ${expected} containers are running; see 'docker compose logs'" >&2
    exit 1
fi

account_host="$(read_json_value ADVERTISED_ADDRESS)"
if [ -z "$account_host" ] || [ "$account_host" = '0.0.0.0' ]; then
    account_host="$(detect_private_ipv4)"
fi

echo
echo "Installed ${running} containers."
echo "Config: ${project_directory}/appsettings.json"
if [ -n "$account_host" ]; then
    echo "To create an account, go to http://${account_host}"
fi
echo "Run this script again to install the update; stop the deployment with: docker compose down"
