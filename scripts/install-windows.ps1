<#
.SYNOPSIS
    Installs the whole deployment from the published container images.

.DESCRIPTION
    Installs the gate, the account server, one gameplay lobby per container (Free
    Battle, Replays, Survival, Basic Training, Combat Training, Survival Hosts,
    Automatching, Registration, Tournament), a gameplay server, the HTTP API, the
    name server and PostgreSQL.

    Running it again installs the update: it refreshes the compose file, pulls
    the newest images, recreates the containers whose image or configuration
    changed and removes old dangling images, so a new release is one command
    away.

    The script runs against a clone when it is started from one; otherwise it
    creates a deployment directory (.\mgo2-server, or MGO2_HOME) and downloads
    compose.yaml and .env.example into it. Everything else is configured in
    .env, which is created from .env.example on the first run and never
    overwritten, except for ADVERTISED_ADDRESS: the script detects the private
    address of this machine and writes it there so clients on the network can
    reach the published ports.

    Set ADVERTISED_ADDRESS to skip detection. Set MGO2_LOG_LEVEL to answer the
    log-level question without a prompt.

.PARAMETER ImagePrefix
    Registry path the images are pulled from, including the trailing slash, for
    example ghcr.io/your-account/your-repository/. When it is omitted,
    MGO2_IMAGE_PREFIX is used: the setting of .env, or the environment
    variable, or the registry the images are published to by default.

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
$DeploymentDirectoryName = 'mgo2-server'

# Prints the value of a setting of .env, or nothing when it is not set.
function Read-EnvValue([string]$Name, [string]$ProjectDirectory) {
    $envFile = Join-Path $ProjectDirectory '.env'
    if (-not (Test-Path $envFile)) {
        return ''
    }

    $match = Select-String -Path $envFile -Pattern "^$Name=" | Select-Object -Last 1
    if ($null -eq $match) {
        return ''
    }

    return ($match.Line -replace "^$Name=", '').Trim()
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

# Answers true for one of the log levels the servers know how to parse.
function Test-LogLevel([string]$Value) {
    return $Value -imatch '^(debug|information|warning|error)$'
}

# Spells a log level the one way the servers document it.
function Get-NormalisedLogLevel([string]$Value) {
    switch ($Value.ToLowerInvariant()) {
        'debug' { return 'Debug' }
        'information' { return 'Information' }
        'warning' { return 'Warning' }
        'error' { return 'Error' }
    }

    return 'Warning'
}

# Maps a log level to its menu number.
function Get-LogLevelChoice([string]$Value) {
    switch (Get-NormalisedLogLevel $Value) {
        'Debug' { return '1' }
        'Information' { return '2' }
        'Warning' { return '3' }
        'Error' { return '4' }
    }
    return ''
}

# Maps a menu number to its log level.
function Get-ChoiceLogLevel([string]$Value) {
    switch ($Value) {
        '1' { return 'Debug' }
        '2' { return 'Information' }
        '3' { return 'Warning' }
        '4' { return 'Error' }
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

# Sets a setting of .env, replacing its value and leaving every other setting alone.
function Set-EnvValue([string]$Name, [string]$Value, [string]$EnvFile) {
    $written = $false

    $result = @(foreach ($line in (Get-Content -Path $EnvFile)) {
        if ($line -match "^$Name=") {
            if (-not $written) {
                "$Name=$Value"
                $written = $true
            }
        }
        else {
            $line
        }
    })

    if (-not $written) {
        $result += "$Name=$Value"
    }

    [IO.File]::WriteAllLines($EnvFile, $result, [Text.UTF8Encoding]::new($false))
}

# Reports the private address clients are told to connect to. An empty answer
# means nothing was decided, so an existing value is kept.
function Resolve-AdvertisedIP([string]$ProjectDirectory) {
    if ($null -ne $env:ADVERTISED_ADDRESS) {
        return $env:ADVERTISED_ADDRESS
    }

    $current = Read-EnvValue 'ADVERTISED_ADDRESS' $ProjectDirectory

    $detected = Get-PrivateIPv4
    if (-not [string]::IsNullOrWhiteSpace($detected)) {
        return $detected
    }

    if (Test-IPv4 $current) {
        return $current
    }

    Write-Host 'warning: the private address of this machine could not be detected; set ADVERTISED_ADDRESS in .env' -ForegroundColor Yellow
    return ''
}

# Reports the log level the servers run at. Every container writes its log to
# its standard output, so the answer is what the level of 'docker compose logs'
# is set to. Warning is the default; MGO2_LOG_LEVEL answers without a prompt.
function Resolve-LogLevel([string]$ProjectDirectory) {
    if ($env:MGO2_LOG_LEVEL) {
        if (Test-LogLevel $env:MGO2_LOG_LEVEL) {
            return (Get-NormalisedLogLevel $env:MGO2_LOG_LEVEL)
        }

        Write-Host "warning: MGO2_LOG_LEVEL='$($env:MGO2_LOG_LEVEL)' is not a log level; using Warning" -ForegroundColor Yellow
        return 'Warning'
    }

    # A level that is already configured stays the default, so running the
    # script again to install an update does not silently rescale the logs; a
    # fresh deployment starts at Warning.
    $current = Read-EnvValue 'LOG_LEVEL' $ProjectDirectory
    $defaultChoice = if (Test-LogLevel $current) { Get-LogLevelChoice $current } else { '3' }

    # A run without an interactive host cannot be asked anything, so the default
    # is what such a run gets.
    if (-not [Environment]::UserInteractive) {
        return (Get-ChoiceLogLevel $defaultChoice)
    }

    Write-Host ''
    Write-Host 'Which log level should the servers use?'
    foreach ($entry in @(@{ N = '1'; L = 'Debug' }, @{ N = '2'; L = 'Information' }, @{ N = '3'; L = 'Warning' }, @{ N = '4'; L = 'Error' })) {
        if ($defaultChoice -eq $entry.N) {
            Write-Host "  $($entry.N)) $($entry.L) (default)"
        }
        else {
            Write-Host "  $($entry.N)) $($entry.L)"
        }
    }

    $choice = Read-Host "Choice [$defaultChoice]"
    if ([string]::IsNullOrWhiteSpace($choice)) {
        $choice = $defaultChoice
    }

    $selected = Get-ChoiceLogLevel $choice
    if ([string]::IsNullOrWhiteSpace($selected)) {
        Write-Host "warning: '$choice' is not a log level; using $(Get-ChoiceLogLevel $defaultChoice)" -ForegroundColor Yellow
        $selected = Get-ChoiceLogLevel $defaultChoice
    }

    return $selected
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

# A script started from disk next to a compose file runs against that clone; a
# piped script (irm ... | iex) downloads the deployment instead.
if ($PSScriptRoot -and (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'compose.yaml'))) {
    $projectDirectory = Split-Path -Parent $PSScriptRoot
    $downloadsDeployment = $false
} elseif (Test-Path (Join-Path $PWD.Path 'compose.yaml')) {
    $projectDirectory = $PWD.Path
    $downloadsDeployment = $false
} else {
    $projectDirectory = if ($env:MGO2_HOME) { $env:MGO2_HOME } else { Join-Path $PWD.Path $DeploymentDirectoryName }
    $downloadsDeployment = $true
}

$sourceUrl = if ($env:MGO2_SOURCE_URL) { $env:MGO2_SOURCE_URL.TrimEnd('/') } else { $DefaultSourceUrl }

if ($downloadsDeployment) {
    New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
    Write-Host "Installing into $projectDirectory"
    Save-DeploymentFile 'compose.yaml' $projectDirectory $sourceUrl
    Save-DeploymentFile '.env.example' $projectDirectory $sourceUrl
}

Push-Location $projectDirectory
try {
    $envFile = Join-Path $projectDirectory '.env'
    if (-not (Test-Path $envFile)) {
        Copy-Item (Join-Path $projectDirectory '.env.example') $envFile
        $jwtSecret = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
        (Get-Content $envFile) -replace '^JWT_SECRET=.*', "JWT_SECRET=$jwtSecret" | Set-Content $envFile
        Write-Host 'Created .env from .env.example with a random JWT_SECRET. Review it before exposing the deployment.'
    }

    $resolvedAdvertisedIp = Resolve-AdvertisedIP $projectDirectory
    if (-not [string]::IsNullOrWhiteSpace($resolvedAdvertisedIp)) {
        Set-EnvValue 'ADVERTISED_ADDRESS' $resolvedAdvertisedIp $envFile
    }

    # The level decides what a container writes to its standard output, so it is
    # answered and written before the images are pulled.
    $resolvedLogLevel = Resolve-LogLevel $projectDirectory
    Set-EnvValue 'LOG_LEVEL' $resolvedLogLevel $envFile

    if ([string]::IsNullOrWhiteSpace($ImagePrefix)) {
        $ImagePrefix = if ($env:MGO2_IMAGE_PREFIX) { $env:MGO2_IMAGE_PREFIX } else { Read-EnvValue 'MGO2_IMAGE_PREFIX' $projectDirectory }
    }

    $resolvedPrefix = Get-NormalisedPrefix $ImagePrefix
    if ([string]::IsNullOrWhiteSpace($resolvedPrefix)) {
        $resolvedPrefix = Get-NormalisedPrefix $DefaultImagePrefix
    }

    $resolvedTag = if ($env:MGO2_IMAGE_TAG) { $env:MGO2_IMAGE_TAG } else { Read-EnvValue 'MGO2_IMAGE_TAG' $projectDirectory }
    if ([string]::IsNullOrWhiteSpace($resolvedTag)) {
        $resolvedTag = 'latest'
    }

    # The process environment wins over .env, so exporting is enough to point
    # compose at the chosen registry.
    $env:MGO2_IMAGE_PREFIX = $resolvedPrefix
    $env:MGO2_IMAGE_TAG = $resolvedTag

    Write-Host "Installing the images of $resolvedPrefix (tag $resolvedTag)"
    Invoke-Compose @('pull')

    Write-Host ''
    Write-Host 'Starting every container and waiting for them to come up'
    Invoke-Compose @('up', '--detach', '--no-build', '--remove-orphans', '--wait', '--wait-timeout', '180')

    Write-Host ''
    Write-Host 'Removing old images left behind by the update'
    & docker image prune --force

    $expected = @(docker compose config --services).Count
    $running = @(docker compose ps --status running --services).Count

    Write-Host ''
    docker compose ps

    if ($running -ne $expected) {
        Write-Host "error: $running of $expected containers are running; see 'docker compose logs'" -ForegroundColor Red
        exit 1
    }

    $accountHost = Read-EnvValue 'ADVERTISED_ADDRESS' $projectDirectory
    if ([string]::IsNullOrWhiteSpace($accountHost) -or $accountHost -eq '0.0.0.0') {
        $accountHost = Get-PrivateIPv4
    }

    Write-Host ''
    Write-Host "Installed $running containers."
    if (-not [string]::IsNullOrWhiteSpace($accountHost)) {
        Write-Host "To create an account, go to http://$accountHost"
    }
    Write-Host 'Run this script again to install the update; stop the deployment with: docker compose down'
}
finally {
    Pop-Location
}
