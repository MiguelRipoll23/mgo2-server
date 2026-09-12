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
# which is created from .env.example on the first run and never overwritten.

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
