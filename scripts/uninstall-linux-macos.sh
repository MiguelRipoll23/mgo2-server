#!/usr/bin/env bash
#
# Removes the whole deployment the install scripts put in place: every
# container, image, volume and network of the mgo2 compose project, and the
# configuration that came with it (.env, and the whole downloaded deployment
# directory when the deployment was installed with a piped script).
#
# Linux and macOS. Windows runs scripts/uninstall-windows.ps1 instead.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/uninstall-linux-macos.sh | bash -s -- --yes
#   scripts/uninstall-linux-macos.sh
#   scripts/uninstall-linux-macos.sh --yes
#
# The script asks for confirmation before it removes anything. --yes (or
# MGO2_ASSUME_YES=1) answers the question without a prompt, which a run without
# a terminal needs. Docker itself is never removed.
#
# When the script is started from a clone of the repository it never removes the
# source tree: the repository and everything else in it stay, and only .env and
# the Docker resources go. A deployment directory a piped install downloaded
# (compose.yaml, .env.example and .env) is removed entirely.

set -euo pipefail

readonly deployment_directory_name="mgo2-server"
readonly compose_project_name="mgo2"

# The fixed container names compose.yaml gives every service.
container_names=(
    mgo2-postgres
    mgo2-gate
    mgo2-account
    mgo2-free-battle
    mgo2-replays
    mgo2-survival
    mgo2-basic-training
    mgo2-combat-training
    mgo2-survival-hosts
    mgo2-automatching
    mgo2-registration
    mgo2-tournament
    mgo2-gameplay-1
    mgo2-http
    mgo2-dns
    mgo2-stun
)
readonly container_names

# The image names every container is built from or pulled as, without the
# registry prefix and the tag.
image_names=(
    mgo2-postgres
    mgo2-gate-lobby-server
    mgo2-account-lobby-server
    mgo2-game-lobby-server
    mgo2-gameplay-server
    mgo2-http
    mgo2-dns
    mgo2-stun
)
readonly image_names

# The volumes compose.yaml declares; compose stores each as
# <project-name>_<volume-name>.
volume_names=(
    mgo2_postgres-data
    mgo2_http-keys
)
readonly volume_names

usage() {
    cat <<'TEXT'
usage: scripts/uninstall-linux-macos.sh [--yes]

  --yes  skip the confirmation; MGO2_ASSUME_YES=1 does the same

  Removes every container, image, volume and network the deployment created,
  and the configuration that came with it. Started from a clone, only .env of
  the repository and the Docker resources are removed; started from a downloaded
  deployment, the whole deployment directory (compose.yaml, .env.example and
  .env) is removed. Docker itself is not removed.
TEXT
}

# Answers true for a yes/no answer.
is_yes_no() {
    case "$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" in
        y | yes | true | 1 | n | no | false | 0) return 0 ;;
        *) return 1 ;;
    esac
}

# Answers true specifically for a yes answer.
is_yes() {
    case "$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" in
        y | yes | true | 1) return 0 ;;
        *) return 1 ;;
    esac
}

# Runs one docker removal command per object read from stdin, so an object that
# is gone already cannot fail the run.
remove_each() {
    local object
    while IFS= read -r object; do
        [ -n "$object" ] || continue
        "$@" "${object}" >/dev/null 2>&1 || true
    done
}

# Removes the containers of the compose project and the ones compose.yaml names.
remove_stale_containers() {
    docker ps -a --filter "label=com.docker.compose.project=${compose_project_name}" \
        --format '{{.Names}}' 2>/dev/null \
        | remove_each docker rm --force || true

    local name
    for name in "${container_names[@]}"; do
        docker rm --force "${name}" >/dev/null 2>&1 || true
    done
}

# Removes the volumes of the compose project and the ones compose.yaml declares.
remove_stale_volumes() {
    docker volume ls --filter "label=com.docker.compose.project=${compose_project_name}" \
        --format '{{.Name}}' 2>/dev/null \
        | remove_each docker volume rm || true

    local name
    for name in "${volume_names[@]}"; do
        docker volume rm "${name}" >/dev/null 2>&1 || true
    done
}

# Removes the network of the compose project.
remove_stale_networks() {
    docker network ls --filter "label=com.docker.compose.project=${compose_project_name}" \
        --format '{{.Name}}' 2>/dev/null \
        | remove_each docker network rm || true

    docker network rm mgo2_default >/dev/null 2>&1 || true
}

# Removes the images built or pulled under the mgo2 image names, whatever the
# registry prefix or tag. compose down --rmi all already removed the exact
# images of compose.yaml; this picks up variants and leftovers.
remove_stale_images() {
    docker image ls --filter "label=com.docker.compose.project=${compose_project_name}" \
        --format '{{.ID}}' 2>/dev/null \
        | while IFS= read -r id; do
            [ -n "$id" ] || continue
            docker rmi --force "${id}" >/dev/null 2>&1 || true
        done || true

    local name reference
    for name in "${image_names[@]}"; do
        docker image ls --format '{{.Repository}}:{{.Tag}}' 2>/dev/null \
            | grep -E "(^|/)${name}:" \
            | while IFS= read -r reference; do
                [ -n "$reference" ] || continue
                echo "Removing image ${reference}"
                docker rmi --force "${reference}" >/dev/null 2>&1 || true
            done || true
    done

    docker image prune --force >/dev/null 2>&1 || true
}

# Removes a downloaded deployment directory, but only when it actually holds the
# deployment and is neither the filesystem root nor the home directory.
remove_deployment_directory() {
    local directory="$1"

    case "${directory}" in
        '' | '/' | "$HOME") return 1 ;;
    esac

    if [ ! -e "${directory}/compose.yaml" ] &&
        [ ! -e "${directory}/.env" ] &&
        [ ! -e "${directory}/.env.example" ]; then
        echo "warning: refusing to remove ${directory}: it holds no downloaded deployment" >&2
        return 0
    fi

    rm -rf -- "${directory}"
    echo "Removed the deployment directory ${directory}"
}

accepted=false
for argument in "$@"; do
    case "${argument}" in
        -h | --help)
            usage
            exit 0
            ;;
        -y | --yes)
            accepted=true
            ;;
        *)
            echo "error: unknown argument '${argument}'" >&2
            echo >&2
            usage >&2
            exit 2
            ;;
    esac
done

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
# piped script (curl ... | bash) targets the downloaded deployment instead, the
# same way the install scripts choose where they act.
script_path="${BASH_SOURCE[0]:-}"
script_directory=""
if [ -n "${script_path}" ] && [ -f "${script_path}" ]; then
    script_directory="$(cd "$(dirname "${script_path}")" && pwd)"
fi

if [ -n "${script_directory}" ] && [ -f "${script_directory}/../compose.yaml" ]; then
    project_directory="$(cd "${script_directory}/.." && pwd)"
elif [ -f "${PWD}/compose.yaml" ]; then
    project_directory="${PWD}"
else
    project_directory="${MGO2_HOME:-${PWD}/${deployment_directory_name}}"
fi

project_present=false
if [ -d "${project_directory}" ]; then
    project_present=true
fi

# A repository checkout keeps everything but .env; a downloaded deployment is
# removed in full.
repository_checkout=false
if [ "${project_present}" = "true" ] &&
    { [ -d "${project_directory}/.git" ] ||
        [ -f "${project_directory}/scripts/install-linux-macos.sh" ]; }; then
    repository_checkout=true
fi

if [ "${accepted}" != "true" ] && [ -n "${MGO2_ASSUME_YES:-}" ]; then
    if is_yes "${MGO2_ASSUME_YES}"; then
        accepted=true
    else
        echo "warning: MGO2_ASSUME_YES='${MGO2_ASSUME_YES}' is not a yes; asking instead" >&2
    fi
fi

if [ "${accepted}" != "true" ]; then
    if ! { : < /dev/tty; } 2>/dev/null; then
        echo >&2
        echo 'error: no terminal to ask for confirmation; run again with --yes or MGO2_ASSUME_YES=1' >&2
        exit 1
    fi

    if [ "${repository_checkout}" = "true" ]; then
        description="${project_directory}/.env"
    else
        description="the deployment directory ${project_directory}"
    fi

    echo
    echo "This removes every container, image, volume and network of the mgo2 deployment, and ${description}."
    read -r -p "Continue? [y/N] " answer < /dev/tty || answer=''
    if ! is_yes "${answer:-n}"; then
        echo 'Aborted; nothing was removed.'
        exit 0
    fi
fi

if [ "${project_present}" = "true" ]; then
    cd "${project_directory}"
fi

echo
if [ "${project_present}" = "true" ] && [ -f compose.yaml ]; then
    echo "Removing the containers, networks and volumes of compose.yaml"
    docker compose down --volumes --remove-orphans --rmi all --timeout 30 || true
fi

echo
echo 'Removing every container of the project'
remove_stale_containers

echo 'Removing every volume of the project'
remove_stale_volumes

echo 'Removing the network of the project'
remove_stale_networks

echo 'Removing the images of the project'
remove_stale_images

echo
if [ "${project_present}" = "false" ]; then
    echo "Nothing to remove on disk: ${project_directory} does not exist."
elif [ "${repository_checkout}" = "true" ]; then
    if [ -f .env ]; then
        rm -f .env
        echo "Removed ${project_directory}/.env"
    else
        echo "No .env config to remove in ${project_directory}"
    fi
else
    remove_deployment_directory "${project_directory}"
fi

echo
echo 'The mgo2 deployment has been removed.'
echo 'Check for leftovers with: docker ps -a, docker images, docker volume ls, docker network ls'
echo 'Docker itself was not removed.'