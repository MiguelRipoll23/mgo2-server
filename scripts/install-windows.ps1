<#
.SYNOPSIS
    Installs the whole deployment from the published container images.

.DESCRIPTION
    Installs the gate, the account server, one gameplay lobby per container (Free
    Battle, Replays, Survival, Basic Training, Combat Training, Survival Hosts,
    Automatching, Registration, Tournament), a Gameplay server, the HTTP API, the
    name server and PostgreSQL.

    Running it again installs the update: it refreshes the compose file, pulls
    the newest images and recreates the containers whose image or configuration
    changed, so a new release is one command away.

    The script runs against a clone when it is started from one; otherwise it
    creates a deployment directory (.\mgo2-server, or MGO2_HOME) and downloads
    compose.yaml and .env.example into it. Everything else is configured in
    .env, which is created from .env.example on the first run and never
    overwritten.

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
    Write-Host 'The gate listens on 5731, the account server on 5732 and the HTTP API on 80.'
    Write-Host 'Run this script again to install the update; stop the deployment with: docker compose down'
}
finally {
    Pop-Location
}
