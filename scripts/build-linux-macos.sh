#!/usr/bin/env bash
#
# Builds every project of the solution with the .NET SDK.
#
# The servers are started from these binaries by scripts/run-linux-macos.sh,
# which builds nothing on its own; run this script again after changing the
# code.
#
# The .NET SDK is installed into the user profile on the first run when it is
# missing. No elevation is required and none is looked for.
#
# Usage:
#   scripts/build-linux-macos.sh
#   MGO2_CONFIGURATION=Release scripts/build-linux-macos.sh

set -euo pipefail

project_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution_name="Mgo2Server.slnx"
dotnet_channel="10.0"
configuration="${MGO2_CONFIGURATION:-Debug}"

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

cd "$project_directory"

initialize_dotnet

echo ''
echo "Building every project ($configuration)"
dotnet build "$solution_name" --configuration "$configuration"

echo ''
echo "Built $configuration binaries into src/*/bin/$configuration/net10.0; start them with scripts/run-linux-macos.sh"
