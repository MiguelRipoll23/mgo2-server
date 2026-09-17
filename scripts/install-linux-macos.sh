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
# downloads compose.yaml and appsettings.example.json into it. The settings of
# the servers live in appsettings.json, created from the example once and
# edited by the operator. Two of its values belong to the deployment rather
# than to the servers: the JWT secret and the private address of this machine.
# The example ships both as the REPLACE_ME placeholder; the first run replaces
# the secret with a random one and the address with a detected one, and a
# placeholder left after a run that could not detect the address is how the
# operator sets it by hand. An edit of appsettings.json is applied by
# restarting the container.
#
# The deployment directory holds compose.yaml and appsettings.json next to each
# other, because the compose file mounts ./appsettings.json: the directory is
# the deployment. Running as root installs the machine-wide one, /opt/mgo2;
# running as a user installs the user one, ~/.local/share/mgo2 on Linux and
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

  The script replaces the REPLACE_ME placeholders of appsettings.json: the
  JWT_SECRET with a random secret and the ADVERTISED_ADDRESS with the private
  address of this machine, which every server reads as an environment
  variable. Setting ADVERTISED_ADDRESS to a fixed value answers for the
  address, and an operator who skips the detection edits appsettings.json.

  The JWT_SECRET of the deployment is a random value on the first install, when
  the placeholder is still there; an operator who set one keeps it. The servers
  log at Warning and always send their metrics over OpenTelemetry
  (OTEL_ENABLED=true, OTEL_PORT=4317), and editing appsettings.json is how
  anything else changes.

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

# Replaces the REPLACE_ME placeholder of one setting of appsettings.json with a
# value. The settings live on their own quoted lines, so the substitution is a
# plain text swap; a run whose file holds no placeholder leaves it alone. The
# values written here are a base64 secret and a dotted address, neither of which
# collides with the | delimiter or carries a quote.
replace_setting_placeholder() {
    local key="$1" value="$2" file="$3"

    sed -i "s|\"${key}\": \"REPLACE_ME\"|\"${key}\": \"${value}\"|" "$file"
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

# The settings of the deployment are copied from the example once; an update
# run never touches the file again, so edits survive. The example ships the two
# values that belong to the deployment as the REPLACE_ME placeholder, which the
# first run replaces below. The file carries the secret of the deployment, so it
# is readable by its owner only, whatever the umask of the run was.
if [ ! -f appsettings.json ]; then
    cp appsettings.example.json appsettings.json
    chmod 600 appsettings.json
    echo "Created appsettings.json from appsettings.example.json."
fi

# The secret of the deployment replaces the JWT_SECRET placeholder on the first
# install; an update run, whose secret already replaced it, leaves the file
# alone.
if grep -q '"JWT_SECRET": "REPLACE_ME"' appsettings.json 2>/dev/null; then
    replace_setting_placeholder JWT_SECRET "$(openssl rand -base64 48)" appsettings.json
    echo "Replaced the JWT_SECRET placeholder with a random secret. Review it before exposing the deployment."
fi

# The address clients are told to connect to replaces its placeholder. An
# operator who needs a fixed one sets ADVERTISED_ADDRESS in appsettings.json,
# and the ADVERTISED_ADDRESS environment variable of the run answers without
# detection.
resolved_advertised_ip="$(resolve_advertised_ip)"
if [ -n "$resolved_advertised_ip" ]; then
    replace_setting_placeholder ADVERTISED_ADDRESS "$resolved_advertised_ip" appsettings.json
else
    echo 'warning: the private address of this machine could not be detected; set the ADVERTISED_ADDRESS placeholder in appsettings.json' >&2
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
echo "Config: ${project_directory}/appsettings.json"
if [ -n "$resolved_advertised_ip" ]; then
    echo "To create an account, go to http://${resolved_advertised_ip}"
fi
echo "Run this script again to install the update; stop the deployment with: docker compose down"
