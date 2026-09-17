#!/bin/sh
# Loads the dotenv of the deployment into the environment of the server before
# it starts. compose mounts the file read-only from the deployment directory, so
# an edited value reaches the process on the next start of the container: a
# plain 'docker compose restart' applies it, exactly like the mounted
# appsettings.json this entrypoint leaves to the framework. A file that is not
# there is fine: a clone runs on the defaults alone.
#
# Any upper-case key is accepted, and a quoted value is rejected, so a typo in
# the file stops the container instead of running the server with a half-read
# configuration.

set -eu

env_file="${DEPLOYMENT_ENV_FILE:-/app/deployment.env}"
carriage_return="$(printf '\r')"

if [ -f "${env_file}" ]; then
    while IFS= read -r line || [ -n "${line}" ]; do
        # A file edited on Windows carries a carriage return the shell would
        # keep as part of the value.
        line=${line%"${carriage_return}"}
        case "${line}" in
            '' | '#'*) continue ;;
        esac

        key=${line%%=*}
        value=${line#*=}

        case "${key}" in
            '' | *[!A-Z0-9_]*)
                echo "entrypoint: ${env_file} sets '${key}', which is not an upper-case setting name" >&2
                exit 1
                ;;
        esac

        case "${value}" in
            *'"'* | *"'"*)
                echo "entrypoint: the value of '${key}' must not be quoted" >&2
                exit 1
                ;;
        esac

        export "${key}=${value}"
    done < "${env_file}"
fi

# exec keeps the process as PID 1 so the container stops on SIGTERM.
exec dotnet "${MGO2_SERVER_ASSEMBLY}".dll
