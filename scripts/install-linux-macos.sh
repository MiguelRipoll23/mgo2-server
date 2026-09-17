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
# The script installs the deployment into a platform default directory and
# downloads compose.yaml and appsettings.example.json into it. The shared
# settings of the servers live in appsettings.json, which is created from the
# example once and never touched again: the operator edits it and an update
# keeps the edits. The
# two settings that belong to the deployment rather than to the servers, the
# JWT secret and the private address of this machine, are written into
# deployment.json instead, which every server reads after the appsettings.json
# it also mounts; an edit of either file is applied by restarting the
# container. The secret is written once, the address is detected again on
# every run so an update follows a machine that changed networks, and
# ADVERTISED_ADDRESS skips the detection.
#
# The deployment directory holds compose.yaml, appsettings.json and
# deployment.json next to each other, because the compose file mounts
# ./appsettings.json and ./deployment.json: the directory is the
# deployment. Running as root installs the machine-wide one, /opt/mgo2; running
# as a user installs the user one, ~/.local/share/mgo2 on Linux and
# ~/Library/Application Support/mgo2 on macOS.

set -euo pipefail

# The source of the deployment files, and the registry the CI workflow
# publishes the images to.
readonly default_source_url="https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main"
readonly default_image_prefix="ghcr.io/miguelripoll23/mgo2-server/"
readonly root_deployment_directory="/opt/mgo2"

usage() {
    cat <<'TEXT'
usage: scripts/install-linux-macos.sh [registry-prefix]

  registry-prefix  registry path the images are pulled from, including the
                   trailing slash, for example
                   ghcr.io/your-account/your-repository/

  The script detects the private address of this machine on every run and
  writes ADVERTISED_ADDRESS into deployment.json, which every server reads
  container as an environment variable. Setting ADVERTISED_ADDRESS answers
  without detection.

  The secret of the deployment, JWT_SECRET, is written into deployment.json
  once, on the first install. The shared settings of the servers live in
  appsettings.json, created from appsettings.example.json on the first run and
  never written by this script: the servers log at Warning and always send
  their metrics over OpenTelemetry (OTEL_ENABLED=true, OTEL_PORT=4317), and
  editing the file is how anything else changes.

  When the registry prefix is omitted, MGO2_IMAGE_PREFIX is used: the
  environment variable, or the registry the images are published to by
  default.

  The deployment is installed into /opt/mgo2 when the script runs as root, and
  into ~/.local/share/mgo2 (Linux) or ~/Library/Application Support/mgo2
  (macOS) otherwise.

  The script pulls the images of every container (the gate, the account server,
  the nine gameplay lobbies, a gameplay server, the HTTP API, the name server, the
  port-check responder and PostgreSQL) and installs them. Running it again
  installs the update.
TEXT
}

# Reports the deployment directory every run installs into: /opt/mgo2 for the
# machine-wide install of a root run, and the user one of $HOME otherwise. The user home of a sudo run is the one of the
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

# Reports the private address clients are told to connect to: the environment
# decides without detection, which is what an unattended run needs, and the
# detection answers otherwise. An empty answer means the address is unknown.
resolve_advertised_ip() {
    if [ -n "${ADVERTISED_ADDRESS:-}" ]; then
        printf '%s' "$ADVERTISED_ADDRESS"
        return 0
    fi

    detect_private_ipv4
}

# Sets "KEY": "VALUE" in the flat deployment.json, replacing the value of the
# key or inserting it before the closing brace, and leaves every other line
# alone. The values written here are a base64 secret and a dotted address,
# neither of which collides with the | delimiter or carries a quote.
set_json_value() {
    local key="$1" value="$2" file="$3"

    if grep -q "\"${key}\":" "$file" 2>/dev/null; then
        sed -i.bak "s|\"${key}\": \"[^\"]*\"|\"${key}\": \"${value}\"|" "$file"
    else
        # Inserts the setting before the closing brace, with the comma the
        # brace takes away.
        awk -v line="  \"${key}\": \"${value}\"," '
            BEGIN { inserted = 0 }
            /^}[[:space:]]*$/ && !inserted { print line; inserted = 1 }
            { print }
        ' "$file" > "${file}.tmp"
        mv "${file}.tmp" "$file"
    fi

    # The file carries the secret of the deployment, so it is readable by its
    # owner only, whatever the umask of the run was.
    chmod 600 "$file"
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

project_directory="$(resolve_deployment_directory)"
source_url="${MGO2_SOURCE_URL:-${default_source_url}}"

mkdir --parents "${project_directory}"
echo "Installing into ${project_directory}"
download_deployment_file compose.yaml
download_deployment_file appsettings.example.json

cd "${project_directory}"

# The shared settings of the servers are copied from the example once; an
# update run never touches the file again, so edits survive.
if [ ! -f appsettings.json ]; then
    cp appsettings.example.json appsettings.json
    echo "Created appsettings.json from appsettings.example.json."
fi

# The secret of the deployment is written once, on the first install.
if [ ! -f deployment.json ]; then
    printf '{\n  "JWT_SECRET": "%s"\n}\n' "$(openssl rand -base64 48)" > deployment.json
    chmod 600 deployment.json
    echo "Created deployment.json with a random JWT_SECRET. Review it before exposing the deployment."
fi

# The address clients are told to connect to is detected again on every run, so
# an update follows a machine that changed networks. An operator who needs a
# fixed one sets ADVERTISED_ADDRESS in deployment.json (or in the environment,
# which answers without detection for this run only).
resolved_advertised_ip="$(resolve_advertised_ip)"
if [ -n "$resolved_advertised_ip" ]; then
    set_json_value ADVERTISED_ADDRESS "$resolved_advertised_ip" deployment.json
else
    echo 'warning: the private address of this machine could not be detected; set ADVERTISED_ADDRESS in deployment.json' >&2
fi

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

echo
echo "Installed ${running} containers."
echo "Config: ${project_directory}/appsettings.json and ${project_directory}/deployment.json"
if [ -n "$resolved_advertised_ip" ]; then
    echo "To create an account, go to http://${resolved_advertised_ip}"
fi
echo "Run this script again to install the update; stop the deployment with: docker compose down"
