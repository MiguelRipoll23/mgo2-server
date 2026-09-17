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

    The script runs against a clone when it is started from one; otherwise it
    creates a deployment directory in a platform default (%ProgramData%\mgo2 for
    an elevated run, %LOCALAPPDATA%\mgo2 otherwise) and downloads
    compose.yaml and appsettings.example.json into it. Everything else is
    configured in appsettings.json, which is created from appsettings.example.json
    on the first run with a random JWT_SECRET and never overwritten afterwards,
    except for ADVERTISED_ADDRESS: the script detects the private address of this
    machine and writes it there so clients on the network can reach the published
    ports.

    Set ADVERTISED_ADDRESS to skip detection. The deployment directory holds
    compose.yaml and appsettings.json next to each other, because the compose
    file mounts .\appsettings.json into the containers: the directory is the
    deployment. The servers log at Warning and always send their metrics over
    gRPC to the collector, whose port OTEL_PORT of appsettings.json carries: the
    collector itself is the operator's change, and nothing of it is touched or
    checked here.

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

# Where a deployment that is not run from a clone comes from, and the registry
# the CI workflow publishes the images to.
$DefaultSourceUrl = 'https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main'
$DefaultImagePrefix = 'ghcr.io/miguelripoll23/mgo2-server/'
$DeploymentDirectoryName = 'mgo2'

# Reports the deployment directory a run that is not started from a clone
# installs into: the machine-wide one of %ProgramData%\mgo2 for an elevated
# run, and the user one of %LOCALAPPDATA%\mgo2 otherwise. A piped run of a
# non-elevated terminal keeps the user directory, so updating needs no
# elevation either.
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

# Prints the value of a setting of appsettings.json, or nothing when it is not
# set. A quoted string and a bare number or boolean are both read; a quoted
# value comes back without the surrounding quotes.
function Read-JsonValue([string]$Name, [string]$ProjectDirectory) {
    $settingsFile = Join-Path $ProjectDirectory 'appsettings.json'
    if (-not (Test-Path $settingsFile)) {
        return ''
    }

    $pattern = '"' + [regex]::Escape($Name) + '":\s*(.*?),?\s*$'
    $match = Select-String -Path $settingsFile -Pattern $pattern | Select-Object -Last 1
    if ($null -eq $match) {
        return ''
    }

    $value = $match.Matches[0].Groups[1].Value.Trim()
    if ($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    return $value
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

# Sets a setting of appsettings.json, replacing its value or inserting the key
# before the closing brace when it is absent, and leaves every other setting
# alone. A quoted value is written between double quotes; a bare value (a number
# or a boolean) is not.
function Set-JsonValue([string]$Name, [string]$Value, [bool]$Quoted, [string]$SettingsFile) {
    $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
    if ($Quoted) {
        $literal = '"' + $Name + '": "' + $escaped + '",'
    }
    else {
        $literal = '"' + $Name + '": ' + $escaped + ','
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]](Get-Content -Path $SettingsFile))

    $keyPattern = '^\s*"' + [regex]::Escape($Name) + '":'
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match $keyPattern) {
            $lines[$index] = '  ' + $literal
            [IO.File]::WriteAllLines($SettingsFile, $lines, [Text.UTF8Encoding]::new($false))
            return
        }
    }

    # Inserts the key before the closing brace, and gives the setting that
    # precedes it the comma the insertion takes away.
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Trim() -eq '}') {
            if ($index -gt 0 -and -not $lines[$index - 1].TrimEnd().EndsWith(',')) {
                $lines[$index - 1] = $lines[$index - 1] + ','
            }
            $lines.Insert($index, '  ' + $literal.TrimEnd(','))
            break
        }
    }

    [IO.File]::WriteAllLines($SettingsFile, $lines, [Text.UTF8Encoding]::new($false))
}

# Reports the private address clients are told to connect to. An empty answer
# means nothing was decided, so an existing value is kept.
function Resolve-AdvertisedIP([string]$ProjectDirectory) {
    if ($null -ne $env:ADVERTISED_ADDRESS) {
        return $env:ADVERTISED_ADDRESS
    }

    $current = Read-JsonValue 'ADVERTISED_ADDRESS' $ProjectDirectory

    $detected = Get-PrivateIPv4
    if (-not [string]::IsNullOrWhiteSpace($detected)) {
        return $detected
    }

    if (Test-IPv4 $current) {
        return $current
    }

    Write-Host 'warning: the private address of this machine could not be detected; set ADVERTISED_ADDRESS in appsettings.json' -ForegroundColor Yellow
    return ''
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

# A script started from disk next to a compose file runs against that clone; a
# piped script (irm ... | iex) downloads the deployment instead.
if ($PSScriptRoot -and (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'compose.yaml'))) {
    $projectDirectory = Split-Path -Parent $PSScriptRoot
    $downloadsDeployment = $false
} elseif (Test-Path (Join-Path $PWD.Path 'compose.yaml')) {
    $projectDirectory = $PWD.Path
    $downloadsDeployment = $false
} else {
    $projectDirectory = Get-DefaultDeploymentDirectory
    $downloadsDeployment = $true
}

$sourceUrl = if ($env:MGO2_SOURCE_URL) { $env:MGO2_SOURCE_URL.TrimEnd('/') } else { $DefaultSourceUrl }

if ($downloadsDeployment) {
    New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
    Write-Host "Installing into $projectDirectory"
    Save-DeploymentFile 'compose.yaml' $projectDirectory $sourceUrl
    Save-DeploymentFile 'appsettings.example.json' $projectDirectory $sourceUrl
}

Push-Location $projectDirectory
try {
    $settingsFile = Join-Path $projectDirectory 'appsettings.json'
    if (-not (Test-Path $settingsFile)) {
        Copy-Item (Join-Path $projectDirectory 'appsettings.example.json') $settingsFile
        $jwtSecret = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
        Set-JsonValue 'JWT_SECRET' $jwtSecret $true $settingsFile
        Write-Host 'Created appsettings.json from appsettings.example.json with a random JWT_SECRET. Review it before exposing the deployment.'
    }

    $resolvedAdvertisedIp = Resolve-AdvertisedIP $projectDirectory
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        Set-JsonValue 'ADVERTISED_ADDRESS' $resolvedAdvertisedIp $true $settingsFile
    }

    # Telemetry is always on: appsettings.example.json ships OTEL_ENABLED=true
    # and OTEL_PORT=4317, so nothing has to be written here. The collector the
    # metrics are sent to is the operator's change: nothing of it is touched
    # or checked here. The log level is left alone as well: the example ships
    # Warning, and a deployment that changed it keeps it across updates.
    Write-Host ''
    Write-Host 'Telemetry is on: the servers send their metrics over gRPC on port 4317.'
    Write-Host 'Change OTEL_PORT in appsettings.json to point them elsewhere; the collector has to listen on the same one.'

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

    $accountHost = Read-JsonValue 'ADVERTISED_ADDRESS' $projectDirectory
    if ([string]::IsNullOrWhiteSpace($accountHost) -or $accountHost -eq '0.0.0.0') {
        $accountHost = Get-PrivateIPv4
    }

    Write-Host ''
    Write-Host "Installed $running containers."
    Write-Host "Config: $(Join-Path $projectDirectory 'appsettings.json')"
    if (-not [string]::IsNullOrWhiteSpace($accountHost)) {
        Write-Host "To create an account, go to http://$accountHost"
    }
    Write-Host 'Run this script again to install the update; stop the deployment with: docker compose down'
}
finally {
    Pop-Location
}
