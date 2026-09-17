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
    compose.yaml and appsettings.example.json into it. The settings of the
    servers live in appsettings.json, created from the example once and edited
    by the operator. The example ships two values that belong to the deployment
    rather than to the servers, the JWT secret and the private address of this
    machine, as the REPLACE_ME placeholder; the first run replaces the secret
    with a random one and the address with a detected one, and a placeholder
    left after a run that could not detect the address is how the operator sets
    it by hand. An edit of appsettings.json is applied by restarting the
    container.

    The deployment directory holds compose.yaml and appsettings.json next to
    each other, because the compose file mounts .\appsettings.json: the
    directory is the deployment.

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

# Replaces the REPLACE_ME placeholder of one setting of appsettings.json with a
# value, as a plain text swap; a text without the placeholder comes back
# unchanged.
function Set-SettingPlaceholder([string]$Key, [string]$Value, [string]$Text) {
    $placeholder = '\"' + $Key + '\": \"REPLACE_ME\"'
    $replacement = '\"' + $Key + '\": \"' + $Value + '\"'
    return $Text.Replace($placeholder, $replacement)
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
    # The settings of the deployment are copied from the example once; an
    # update run never touches the file again, so edits survive. The example
    # ships the two values that belong to the deployment as the REPLACE_ME
    # placeholder, which the first run replaces below.
    if (-not (Test-Path 'appsettings.json')) {
        Copy-Item 'appsettings.example.json' 'appsettings.json'
        Write-Host 'Created appsettings.json from appsettings.example.json.'
    }

    $appSettingsPath = Join-Path $PWD 'appsettings.json'
    $settingsText = [IO.File]::ReadAllText($appSettingsPath)

    # The secret of the deployment replaces the JWT_SECRET placeholder on the
    # first install, from a cryptographic random generator rather than the
    # module one; an update run, whose secret already replaced it, leaves the
    # file alone.
    if ($settingsText.Contains('\"JWT_SECRET\": \"REPLACE_ME\"')) {
        $secretBytes = [byte[]]::new(48)
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $rng.GetBytes($secretBytes)
        }
        finally {
            $rng.Dispose()
        }
        $settingsText = Set-SettingPlaceholder 'JWT_SECRET' ([Convert]::ToBase64String($secretBytes)) $settingsText
        Write-Host 'Replaced the JWT_SECRET placeholder with a random secret. Review it before exposing the deployment.'
    }

    # The address clients are told to connect to replaces its placeholder. An
    # operator who needs a fixed one sets ADVERTISED_ADDRESS in appsettings.json,
    # and the ADVERTISED_ADDRESS environment variable of the run answers without
    # detection.
    $resolvedAdvertisedIp = Resolve-AdvertisedIP
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        $settingsText = Set-SettingPlaceholder 'ADVERTISED_ADDRESS' $resolvedAdvertisedIp $settingsText
    }
    else {
        Write-Host 'warning: the private address of this machine could not be detected; set the ADVERTISED_ADDRESS placeholder in appsettings.json' -ForegroundColor Yellow
    }

    [IO.File]::WriteAllText($appSettingsPath, $settingsText, [Text.UTF8Encoding]::new($false))

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
    Write-Host "Config: $(Join-Path $PWD 'appsettings.json')"
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        Write-Host "To create an account, go to http://$resolvedAdvertisedIp"
    }
    Write-Host 'Run this script again to install the update; stop the deployment with: docker compose down'
}
finally {
    Pop-Location
}
