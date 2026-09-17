<#
.SYNOPSIS
    Installs the whole deployment from the published container images.

.DESCRIPTION
    Installs the gate, the account server, one gameplay lobby per container (Free
    Battle, Replays, Survival, Basic Training, Combat Training, Survival Hosts,
    Automatching, Registration, Tournament), a gameplay server, the HTTP API, the
    name server, the port-check responder and PostgreSQL.

    Running it again installs the update: it refreshes the compose file, pulls
    the newest images, recreates the containers whose image or configuration
    changed and removes old dangling images, so a new release is one command
    away.

    The script installs the deployment into a platform default (%ProgramData%\mgo2 for
    an elevated run, %LOCALAPPDATA%\mgo2 otherwise) and downloads
    compose.yaml and appsettings.example.json into it. The shared settings of
    the servers live in appsettings.json, which is created from the example once
    and never touched again: the operator edits it and an update keeps the
    edits. The two settings that belong to the deployment rather than to the
    servers, the JWT secret and the private address of this machine, are written
    into deployment.env instead, which the container entrypoint loads into the
    environment on every start; the environment overrides appsettings.json, so
    an edit of either file is applied by restarting the container. The secret is
    written once, the address is detected again on every run so an update
    follows a machine that changed networks, and ADVERTISED_ADDRESS skips the
    detection.

    The deployment directory holds compose.yaml, appsettings.json and
    deployment.env next to each other, because the compose file mounts
    .\appsettings.json and reads deployment.env from it: the directory is the
    deployment.

.PARAMETER ImagePrefix
    Registry path the images are pulled from, including the trailing slash, for
    example ghcr.io/your-account/your-repository/. When it is omitted,
    MGO2_IMAGE_PREFIX is used: the environment variable, or the registry the
    images are published to by default.

.EXAMPLE
    irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-windows.ps1 | iex

.EXAMPLE
    .\scripts\install-windows.ps1
    .\scripts\install-windows.ps1 -ImagePrefix ghcr.io/your-account/your-repository/
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ImagePrefix = ''
)

$ErrorActionPreference = 'Stop'

# The source of the deployment files, and the registry the CI workflow
# publishes the images to.
$DefaultSourceUrl = 'https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main'
$DefaultImagePrefix = 'ghcr.io/miguelripoll23/mgo2-server/'
$DeploymentDirectoryName = 'mgo2'

# Reports the deployment directory every run installs into: the machine-wide
# one of %ProgramData%\mgo2 for an elevated run, and the user one of
# %LOCALAPPDATA%\mgo2 otherwise. A piped run of a non-elevated terminal keeps
# the user directory, so updating needs no elevation either.
function Get-DefaultDeploymentDirectory {
    $userProfile = if ($env:USERPROFILE) { $env:USERPROFILE } else { 'C:\Users\Public' }

    $elevated = $false
    try {
        $principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        $elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    catch {
        # The identity could not be read; the machine-wide directory is the safe
        # answer when the run turns out to have the rights for it.
        $elevated = $true
    }

    if ($elevated) {
        $programData = if ($env:ProgramData) { $env:ProgramData } else { 'C:\ProgramData' }
        return Join-Path $programData $DeploymentDirectoryName
    }

    $localAppData = if ($env:LOCALAPPDATA) { $env:LOCALAPPDATA } else { Join-Path $userProfile 'AppData\Local' }
    return Join-Path $localAppData $DeploymentDirectoryName
}

# Reports the private IPv4 address of this machine, or nothing when it cannot be
# determined. The default route picks the interface the clients on the network
# reach.
function Get-PrivateIPv4 {
    try {
        $route = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop |
            Sort-Object -Property RouteMetric |
            Select-Object -First 1

        if ($route) {
            $address = Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction Stop |
                Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
                Select-Object -First 1

            if ($address) {
                return $address.IPAddress
            }
        }
    }
    catch {
        # The NetTCPIP module is unavailable; the caller falls back.
    }

    return ''
}

# Answers true for a dotted IPv4 address whose four octets each fit in a byte.
function Test-IPv4([string]$Value) {
    if ($Value -notmatch '^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$') {
        return $false
    }

    foreach ($octet in $Matches[1..4]) {
        if ([int]$octet -gt 255) {
            return $false
        }
    }

    return $true
}

# Reports the private address clients are told to connect to: the environment
# decides without detection, which is what an unattended run needs, and the
# detection answers otherwise. An empty answer means the address is unknown.
function Resolve-AdvertisedIP {
    if ($null -ne $env:ADVERTISED_ADDRESS) {
        return $env:ADVERTISED_ADDRESS
    }

    return Get-PrivateIPv4
}

# Sets KEY=VALUE in a dotenv file, replacing the line of the key or appending it
# at the end, and leaves every other line alone. The values written here are a
# base64 secret and a dotted address, neither of which carries a newline.
function Set-EnvValue([string]$Key, [string]$Value, [string]$File) {
    $lines = [System.Collections.Generic.List[string]]::new()
    if (Test-Path $File) {
        $lines.AddRange([string[]](Get-Content -Path $File))
    }

    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match "^$([regex]::Escape($Key))=") {
            $lines[$index] = "$Key=$Value"
            [IO.File]::WriteAllLines($File, $lines, [Text.UTF8Encoding]::new($false))
            return
        }
    }

    $lines.Add("$Key=$Value")
    [IO.File]::WriteAllLines($File, $lines, [Text.UTF8Encoding]::new($false))
}

# Adds the trailing slash the compose file expects, and nothing when empty.
function Get-NormalisedPrefix([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    return $Value.TrimEnd('/') + '/'
}

# Runs a native command and stops with a clear message when it fails.
function Invoke-Compose([string[]]$Arguments) {
    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

# Copies a file of the published deployment into the deployment directory.
function Save-DeploymentFile([string]$Name, [string]$ProjectDirectory, [string]$SourceUrl) {
    Invoke-WebRequest -UseBasicParsing -Uri "$SourceUrl/$Name" -OutFile (Join-Path $ProjectDirectory $Name)
    Write-Host "Downloaded $Name from $SourceUrl"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'the docker command was not found; install Docker Desktop first'
}

& docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'the docker compose plugin was not found; it ships with Docker Desktop'
}

# The first pull fails with a bare permission error when the daemon is not
# reachable, so the access is checked here with the answer ready.
$dockerInfoOutput = ''
try {
    $dockerInfoOutput = (& docker info 2>&1 | Out-String)
}
catch {
    # A native error stops the pipeline under Windows PowerShell 5.1; the stderr
    # text arrives inside the error record and is kept for the hint below.
    $dockerInfoOutput = "$_"
}
if ($LASTEXITCODE -ne 0) {
    Write-Host 'error: the docker daemon is not reachable; start Docker Desktop first' -ForegroundColor Red
    if ($dockerInfoOutput -match 'permission denied') {
        Write-Host '       this user cannot reach the docker daemon: add it to the docker-users group and' -ForegroundColor Red
        Write-Host '       sign in again, or run this script from an elevated terminal, which installs the' -ForegroundColor Red
        Write-Host '       machine-wide deployment into %ProgramData%\mgo2' -ForegroundColor Red
    }
    exit 1
}

$projectDirectory = Get-DefaultDeploymentDirectory
$sourceUrl = if ($env:MGO2_SOURCE_URL) { $env:MGO2_SOURCE_URL.TrimEnd('/') } else { $DefaultSourceUrl }

New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
Write-Host "Installing into $projectDirectory"
Save-DeploymentFile 'compose.yaml' $projectDirectory $sourceUrl
Save-DeploymentFile 'appsettings.example.json' $projectDirectory $sourceUrl

Push-Location $projectDirectory
try {
    # The shared settings of the servers are copied from the example once; an
    # update run never touches the file again, so edits survive.
    if (-not (Test-Path 'appsettings.json')) {
        Copy-Item 'appsettings.example.json' 'appsettings.json'
        Write-Host 'Created appsettings.json from appsettings.example.json.'
    }

    # The secret of the deployment is written once, on the first install.
    if (-not (Test-Path 'deployment.env')) {
        $jwtSecret = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
        [IO.File]::WriteAllText("$PWD/deployment.env", "JWT_SECRET=$jwtSecret`n")
        Write-Host 'Created deployment.env with a random JWT_SECRET. Review it before exposing the deployment.'
    }

    # The address clients are told to connect to is detected again on every run,
    # so an update follows a machine that changed networks. An operator who
    # needs a fixed one sets ADVERTISED_ADDRESS in deployment.env (or in the
    # environment, which answers without detection for this run only).
    $resolvedAdvertisedIp = Resolve-AdvertisedIP
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        Set-EnvValue 'ADVERTISED_ADDRESS' $resolvedAdvertisedIp "$PWD/deployment.env"
    }
    else {
        Write-Host 'warning: the private address of this machine could not be detected; set ADVERTISED_ADDRESS in deployment.env' -ForegroundColor Yellow
    }

    if ([string]::IsNullOrWhiteSpace($ImagePrefix)) {
        $ImagePrefix = if ($env:MGO2_IMAGE_PREFIX) { $env:MGO2_IMAGE_PREFIX } else { '' }
    }

    $resolvedPrefix = Get-NormalisedPrefix $ImagePrefix
    if ([string]::IsNullOrWhiteSpace($resolvedPrefix)) {
        $resolvedPrefix = Get-NormalisedPrefix $DefaultImagePrefix
    }

    $resolvedTag = if ($env:MGO2_IMAGE_TAG) { $env:MGO2_IMAGE_TAG } else { 'latest' }

    # The exported variables point compose at the chosen registry.
    $env:MGO2_IMAGE_PREFIX = $resolvedPrefix
    $env:MGO2_IMAGE_TAG = $resolvedTag

    Write-Host "Installing the images of $resolvedPrefix (tag $resolvedTag)"
    Invoke-Compose @('pull')

    Write-Host ''
    Write-Host 'Starting every container and waiting for them to come up'
    Invoke-Compose @('up', '--detach', '--remove-orphans', '--wait', '--wait-timeout', '180')

    Write-Host ''
    Write-Host 'Removing old images left behind by the update'
    & docker image prune --force

    # Every service is expected to be running once the deployment is up.
    $expected = @(docker compose config --services).Count
    $running = @(docker compose ps --status running --services).Count

    Write-Host ''
    docker compose ps

    if ($running -ne $expected) {
        Write-Host "error: $running of $expected containers are running; see 'docker compose logs'" -ForegroundColor Red
        exit 1
    }

    Write-Host ''
    Write-Host "Installed $running containers."
    Write-Host "Config: $(Join-Path $PWD 'appsettings.json') and $(Join-Path $PWD 'deployment.env')"
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        Write-Host "To create an account, go to http://$resolvedAdvertisedIp"
    }
    Write-Host 'Run this script again to install the update; stop the deployment with: docker compose down'
}
finally {
    Pop-Location
}
