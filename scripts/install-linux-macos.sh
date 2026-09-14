#!/usr/bin/env bash
#
# Installs the whole deployment from the published container images: the gate,
# the account server, one gameplay lobby per container (Free Battle, Replays,
# Survival, Basic Training, Combat Training, Survival Hosts, Automatching,
# Registration, Tournament), a gameplay server, the HTTP API, the name server and
# PostgreSQL.
#
# Linux and macOS. Windows runs scripts/install-windows.ps1 instead.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
#   scripts/install-linux-macos.sh                          # registry prefix from .env
#   scripts/install-linux-macos.sh ghcr.io/owner/repo       # registry prefix from the argument
#
# Running it again installs the update: it refreshes the compose file, pulls the
# newest images, recreates the containers whose image or configuration changed
# and removes old dangling images, so a new release is one command away.
#
# The script runs against a clone when it is started from one; otherwise it
# creates a deployment directory (./mgo2-server, or MGO2_HOME) and downloads
# compose.yaml and .env.example into it. Everything else is configured in .env,
# which is created from .env.example on the first run and never overwritten,
# except for PUBLIC_IP: the script asks whether only clients on this machine or
# the clients on the local network should be served and writes the answer there.
#
# Set MGO2_NETWORK=local or MGO2_NETWORK=private (or PUBLIC_IP) to answer that
# question without a prompt, which is what an unattended run needs.

set -euo pipefail

# Where a deployment that is not run from a clone comes from, and the registry
# the CI workflow publishes the images to.
readonly default_source_url="https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main"
readonly default_image_prefix="ghcr.io/miguelripoll23/mgo2-server/"
readonly deployment_directory_name="mgo2-server"

usage() {
    cat <<'TEXT'
usage: scripts/install-linux-macos.sh [registry-prefix]

  registry-prefix  registry path the images are pulled from, including the
                   trailing slash, for example
                   ghcr.io/your-account/your-repository/

  The script asks whether only clients on this machine or the clients on the
  local network should be served, and writes PUBLIC_IP accordingly. Setting
  MGO2_NETWORK=local or MGO2_NETWORK=private answers without a prompt.

  It also asks which log level the servers run at (Debug, Information, Warning
  or Error) and writes LOG_LEVEL, defaulting to Debug. Setting MGO2_LOG_LEVEL
  answers without a prompt.

  When it is omitted, MGO2_IMAGE_PREFIX is used: the setting of .env, or the
  environment variable, or the registry the images are published to by default.

  The script pulls the images of every container (the gate, the account server,
  the nine gameplay lobbies, a gameplay server, the HTTP API, the name server and
  PostgreSQL) and installs them. Running it again installs the update.
TEXT
}

# Prints the value of a setting of .env, or nothing when it is not set.
read_env_value() {
    local name="$1"
    [ -f "${project_directory}/.env" ] || return 0
    sed -n "s/^${name}=//p" "${project_directory}/.env" | tail -n 1 | tr -d '\r'
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

# Answers true for one of the log levels the servers know how to parse.
is_log_level() {
    case "$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" in
        debug|information|warning|error) return 0 ;;
        *) return 1 ;;
    esac
}

# Spells a log level the one way the servers document it, so .env holds a value
# that reads the same however it was typed.
normalise_log_level() {
    case "$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" in
        debug) printf '%s' 'Debug' ;;
        information) printf '%s' 'Information' ;;
        warning) printf '%s' 'Warning' ;;
        error) printf '%s' 'Error' ;;
    esac
}

# Answers true for a dotted IPv4 address whose four octets each fit in a byte.
is_ipv4() {
    [ -n "$1" ] || return 1

    printf '%s' "$1" | awk -F. '
        NF != 4 { exit 1 }
        { for (i = 1; i <= 4; i++) if ($i !~ /^[0-9]{1,3}$/ || $i > 255) exit 1 }
    '
}

# Sets a setting of .env, replacing its value or appending it when the key is
# absent, and leaves every other setting alone.
set_env_value() {
    local name="$1" value="$2" env_file="$3"

    if grep -q "^${name}=" "$env_file"; then
        sed -i.bak "s|^${name}=.*|${name}=${value}|" "$env_file"
        rm -f "${env_file}.bak"
    else
        printf '%s=%s\n' "$name" "$value" >> "$env_file"
    fi
}

# Reports the address the clients are told to connect to. An empty answer serves
# only a client on this machine; the sentinel means nothing was decided, so an
# existing value is kept.
resolve_public_ip() {
    local unchanged='__unchanged__'

    # The environment decides without asking, which is what a piped or otherwise
    # unattended run needs.
    if [ -n "${PUBLIC_IP+x}" ]; then
        printf '%s' "$PUBLIC_IP"
        return 0
    fi

    if [ -n "${MGO2_NETWORK+x}" ]; then
        case "$MGO2_NETWORK" in
            local)
                printf '%s' ''
                return 0
                ;;
            private|lan|network)
                local detected
                detected="$(detect_private_ipv4)"
                if [ -z "$detected" ]; then
                    echo 'warning: the private address of this machine could not be detected; set PUBLIC_IP in .env' >&2
                    printf '%s' "$unchanged"
                    return 0
                fi
                printf '%s' "$detected"
                return 0
                ;;
            *)
                echo "warning: MGO2_NETWORK='$MGO2_NETWORK' is neither 'local' nor 'private'; asking instead" >&2
                ;;
        esac
    fi

    # A run without a terminal cannot be asked anything. Actually opening the
    # terminal is what proves there is one: the permission test alone passes on a
    # machine whose /dev/tty exists but cannot be opened.
    if ! { : < /dev/tty; } 2>/dev/null; then
        printf '%s' "$unchanged"
        return 0
    fi

    local current detected default_choice choice answer
    current="$(read_env_value PUBLIC_IP)"
    detected="$(detect_private_ipv4)"
    if [ -n "$current" ]; then
        default_choice=2
    else
        default_choice=1
    fi

    printf '\nWho should be able to play?\n' > /dev/tty
    printf '  1) Clients on this machine only\n' > /dev/tty
    printf '  2) Consoles and computers on this network%s\n' "${detected:+ (${detected})}" > /dev/tty

    choice=''
    read -r -p "Choice [${default_choice}]: " choice < /dev/tty || choice=''
    choice="${choice:-$default_choice}"

    if [ "$choice" != '2' ]; then
        printf '%s' ''
        return 0
    fi

    # The address is picked rather than asked for: the detected address is what
    # the clients have to be told, and an existing value is only kept when no
    # address can be detected at all.
    answer="${detected:-$current}"

    if ! is_ipv4 "$answer"; then
        echo 'warning: the private address of this machine could not be detected; set PUBLIC_IP in .env' >&2
        printf '%s' "$unchanged"
        return 0
    fi

    printf '%s' "$answer"
}

# Reports the log level the servers run at. Every container writes its log to
# its standard output, so the answer is what the level of 'docker compose logs'
# is set to. Debug is the default, because a deployment whose logs are missing
# detail is the harder one to support; MGO2_LOG_LEVEL answers without a prompt.
resolve_log_level() {
    local current default_choice choice

    current="$(read_env_value LOG_LEVEL)"

    # The environment decides without asking, which is what a piped or otherwise
    # unattended run needs.
    if [ -n "${MGO2_LOG_LEVEL+x}" ]; then
        if is_log_level "$MGO2_LOG_LEVEL"; then
            normalise_log_level "$MGO2_LOG_LEVEL"
        else
            echo "warning: MGO2_LOG_LEVEL='$MGO2_LOG_LEVEL' is not a log level; using Debug" >&2
            printf '%s' 'Debug'
        fi
        return 0
    fi

    # A level that is already configured stays the default, so running the
    # script again to install an update does not silently rescale the logs;
    # a fresh deployment starts at Debug.
    if is_log_level "$current"; then
        default_choice="$(normalise_log_level "$current")"
    else
        default_choice='Debug'
    fi

    # A run without a terminal cannot be asked anything, so the default is what
    # such a run gets.
    if ! { : < /dev/tty; } 2>/dev/null; then
        printf '%s' "$default_choice"
        return 0
    fi

    printf '\nWhich log level should the servers use?\n' > /dev/tty
    printf '  Debug, Information, Warning or Error (later levels log less)\n' > /dev/tty

    choice=''
    read -r -p "Log level [${default_choice}]: " choice < /dev/tty || choice=''
    choice="${choice:-$default_choice}"

    if ! is_log_level "$choice"; then
        echo "warning: '$choice' is not a log level; using ${default_choice}" >&2
        choice="$default_choice"
    fi

    normalise_log_level "$choice"
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
    project_directory="${MGO2_HOME:-${PWD}/${deployment_directory_name}}"
    downloads_deployment=true
fi

source_url="${MGO2_SOURCE_URL:-${default_source_url}}"

if [ "${downloads_deployment}" = "true" ]; then
    mkdir --parents "${project_directory}"
    echo "Installing into ${project_directory}"
    download_deployment_file compose.yaml
    download_deployment_file .env.example
fi

cd "${project_directory}"

if [ ! -f .env ]; then
    cp .env.example .env
    jwt_secret="$(openssl rand -base64 48)"
    sed -i.bak "s/^JWT_SECRET=.*/JWT_SECRET=${jwt_secret}/" .env
    rm -f .env.bak
    echo "Created .env from .env.example with a random JWT_SECRET. Review it before exposing the deployment."
fi

# A client that is not on this machine is told to connect to the address of this
# machine on the network, so the question is answered before the images are
# pulled rather than after a connection that cannot work.
resolved_public_ip="$(resolve_public_ip)"
if [ "$resolved_public_ip" != '__unchanged__' ]; then
    set_env_value PUBLIC_IP "$resolved_public_ip" .env
    if [ -z "$resolved_public_ip" ]; then
        echo 'Only a client on this machine will be able to connect.'
    fi
fi

# The level decides what a container writes to its standard output, so it is
# answered and written before the images are pulled.
resolved_log_level="$(resolve_log_level)"
set_env_value LOG_LEVEL "$resolved_log_level" .env
echo "The servers log at ${resolved_log_level}."

image_prefix="$(normalise_prefix "${1:-${MGO2_IMAGE_PREFIX:-$(read_env_value MGO2_IMAGE_PREFIX)}}")"
image_prefix="${image_prefix:-$(normalise_prefix "${default_image_prefix}")}"
image_tag="${MGO2_IMAGE_TAG:-$(read_env_value MGO2_IMAGE_TAG)}"
image_tag="${image_tag:-latest}"

# The shell environment wins over .env, so exporting is enough to point compose
# at the chosen registry.
export MGO2_IMAGE_PREFIX="${image_prefix}"
export MGO2_IMAGE_TAG="${image_tag}"

echo "Installing the images of ${image_prefix} (tag ${image_tag})"
docker compose pull

echo
echo "Starting every container and waiting for them to come up"
docker compose up --detach --no-build --remove-orphans --wait --wait-timeout 180

echo
echo "Removing old images left behind by the update"
docker image prune --force

expected="$(docker compose config --services | wc -l | tr -d ' ')"
running="$(docker compose ps --status running --services | wc -l | tr -d ' ')"

echo
docker compose ps

if [ "${running}" != "${expected}" ]; then
    echo >&2
    echo "error: ${running} of ${expected} containers are running; see 'docker compose logs'" >&2
    exit 1
fi

echo
echo "Installed ${running} containers."
echo "The gate listens on 5731, the account server on 5732 and the HTTP API on 80."
echo "Run this script again to install the update; stop the deployment with: docker compose down"
