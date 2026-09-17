<#
.SYNOPSIS
    Removes the whole deployment the install scripts put in place.

.DESCRIPTION
    Removes every container, image, volume and network of the mgo2 compose
    project, and the configuration that came with it (appsettings.json, and the
    whole downloaded deployment directory when the deployment was installed with
    a piped script).

    The script asks for confirmation before it removes anything. -Yes (or
    MGO2_ASSUME_YES=1) answers the question without a prompt, which a run
    without a terminal needs. Docker itself is never removed.

    When the script is started from a clone of the repository it never removes
    the source tree: the repository and everything else in it stay, and only
    appsettings.json and the Docker resources go. A deployment directory a piped
    install downloaded (compose.yaml, appsettings.example.json and
    appsettings.json) is removed entirely.

.PARAMETER Yes
    Skips the confirmation prompt.

.EXAMPLE
    irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/uninstall-windows.ps1 | iex

.EXAMPLE
    .\scripts\uninstall-windows.ps1
    .\scripts\uninstall-windows.ps1 -Yes
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$DeploymentDirectoryName = 'mgo2-server'
$ComposeProjectName = 'mgo2'

# The fixed container names compose.yaml gives every service.
$ContainerNames = @(
    'mgo2-postgres'
    'mgo2-gate'
    'mgo2-account'
    'mgo2-free-battle'
    'mgo2-replays'
    'mgo2-survival'
    'mgo2-basic-training'
    'mgo2-combat-training'
    'mgo2-survival-hosts'
    'mgo2-automatching'
    'mgo2-registration'
    'mgo2-tournament'
    'mgo2-gameplay-1'
    'mgo2-http'
    'mgo2-dns'
    'mgo2-stun'
)

# The image names every container is built from or pulled as, without the
# registry prefix and the tag.
$ImageNames = @(
    'mgo2-postgres'
    'mgo2-gate-lobby-server'
    'mgo2-account-lobby-server'
    'mgo2-game-lobby-server'
    'mgo2-gameplay-server'
    'mgo2-http'
    'mgo2-dns'
    'mgo2-stun'
)

# The volumes compose.yaml declares; compose stores each as
# <project-name>_<volume-name>.
$VolumeNames = @(
    'mgo2_postgres-data'
    'mgo2_http-keys'
)

# Answers true specifically for a yes answer.
function Test-Yes([string]$Value) {
    return $Value -imatch '^(y|yes|true|1)$'
}

# Runs a docker command, ignores the output and reports a failed one instead of
# stopping.
function Invoke-BestEffort([string[]]$Arguments) {
    & docker @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "warning: docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE" -ForegroundColor Yellow
    }
}

# Runs a docker command whose output becomes the result, and an empty list when
# it fails.
function Get-DockerResult([string[]]$Arguments) {
    $output = & docker @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }
    return @($output)
}

# Answers true when the directory is a repository checkout, which is where a
# clone of this repository can be told apart from a downloaded deployment.
function Test-RepositoryCheckout([string]$Path) {
    return (Test-Path (Join-Path $Path '.git')) -or
           (Test-Path (Join-Path $Path 'scripts\install-windows.ps1'))
}

# Removes a downloaded deployment directory, but only when it actually holds the
# deployment and is neither the filesystem root nor the home directory.
function Remove-DeploymentDirectory([string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Directory) -or $Directory -eq '/' -or $Directory -eq $HOME) {
        Write-Host "refusing to remove $Directory" -ForegroundColor Yellow
        return
    }

    $hasMarker = (Test-Path (Join-Path $Directory 'compose.yaml')) -or
                 (Test-Path (Join-Path $Directory 'appsettings.json')) -or
                 (Test-Path (Join-Path $Directory 'appsettings.example.json'))
    if (-not $hasMarker) {
        Write-Host "warning: refusing to remove $Directory; it holds no downloaded deployment" -ForegroundColor Yellow
        return
    }

    try {
        Remove-Item -LiteralPath $Directory -Recurse -Force -ErrorAction Stop
        Write-Host "Removed the deployment directory $Directory"
    }
    catch {
        Write-Host "warning: could not remove ${Directory}: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'the docker command was not found; install Docker Desktop first'
}

& docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'the docker compose plugin was not found; it ships with Docker Desktop'
}

# A script started from disk next to a compose file runs against that clone; a
# piped script (irm ... | iex) targets the downloaded deployment instead, the
# same way the install scripts choose where they act.
if ($PSScriptRoot -and (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'compose.yaml'))) {
    $projectDirectory = Split-Path -Parent $PSScriptRoot
} elseif (Test-Path (Join-Path $PWD.Path 'compose.yaml')) {
    $projectDirectory = $PWD.Path
} else {
    $projectDirectory = if ($env:MGO2_HOME) { $env:MGO2_HOME } else { Join-Path $PWD.Path $DeploymentDirectoryName }
}

$projectPresent = Test-Path -LiteralPath $projectDirectory

# A repository checkout keeps everything but appsettings.json; a downloaded
# deployment is removed in full.
$repositoryCheckout = $false
if ($projectPresent) {
    $repositoryCheckout = Test-RepositoryCheckout $projectDirectory
}

$accepted = [bool]$Yes
if (-not $accepted -and $env:MGO2_ASSUME_YES) {
    if (Test-Yes $env:MGO2_ASSUME_YES) {
        $accepted = $true
    }
    else {
        Write-Host "warning: MGO2_ASSUME_YES='$($env:MGO2_ASSUME_YES)' is not a yes; asking instead" -ForegroundColor Yellow
    }
}

if (-not $accepted) {
    if (-not [Environment]::UserInteractive) {
        Write-Host 'error: no terminal to ask for confirmation; run again with -Yes or set MGO2_ASSUME_YES=1' -ForegroundColor Red
        exit 1
    }

    $description = if ($repositoryCheckout) { (Join-Path $projectDirectory 'appsettings.json') } else { "the deployment directory $projectDirectory" }

    Write-Host ''
    Write-Host "This removes every container, image, volume and network of the mgo2 deployment, and $description."
    $answer = Read-Host 'Continue? [y/N]'
    if ([string]::IsNullOrWhiteSpace($answer)) {
        $answer = 'n'
    }

    if (-not (Test-Yes $answer)) {
        Write-Host 'Aborted; nothing was removed.'
        return
    }
}

Write-Host ''
if ($projectPresent -and (Test-Path (Join-Path $projectDirectory 'compose.yaml'))) {
    Write-Host 'Removing the containers, networks and volumes of compose.yaml'
    Invoke-BestEffort @('compose', '-f', (Join-Path $projectDirectory 'compose.yaml'), 'down', '--volumes', '--remove-orphans', '--rmi', 'all', '--timeout', '30')
}

Write-Host ''
Write-Host 'Removing every container of the project'
$existingContainers = @(Get-DockerResult @('ps', '-a', '--format', '{{.Names}}'))
$projectContainers = @(Get-DockerResult @('ps', '-a', '--filter', "label=com.docker.compose.project=$ComposeProjectName", '--format', '{{.Names}}'))
foreach ($name in (@($projectContainers) + $ContainerNames | Select-Object -Unique)) {
    if ($name -and ($existingContainers -contains $name)) {
        Invoke-BestEffort @('rm', '--force', $name)
    }
}

Write-Host 'Removing every volume of the project'
$existingVolumes = @(Get-DockerResult @('volume', 'ls', '--format', '{{.Name}}'))
$projectVolumes = @(Get-DockerResult @('volume', 'ls', '--filter', "label=com.docker.compose.project=$ComposeProjectName", '--format', '{{.Name}}'))
foreach ($name in (@($projectVolumes) + $VolumeNames | Select-Object -Unique)) {
    if ($name -and ($existingVolumes -contains $name)) {
        Invoke-BestEffort @('volume', 'rm', $name)
    }
}

Write-Host 'Removing the network of the project'
$existingNetworks = @(Get-DockerResult @('network', 'ls', '--format', '{{.Name}}'))
$projectNetworks = @(Get-DockerResult @('network', 'ls', '--filter', "label=com.docker.compose.project=$ComposeProjectName", '--format', '{{.Name}}')) + @('mgo2_default')
foreach ($name in ($projectNetworks | Select-Object -Unique)) {
    if ($name -and ($existingNetworks -contains $name)) {
        Invoke-BestEffort @('network', 'rm', $name)
    }
}

Write-Host 'Removing the images of the project'
$builtImages = @(Get-DockerResult @('image', 'ls', '--filter', "label=com.docker.compose.project=$ComposeProjectName", '--format', '{{.ID}}'))
foreach ($imageId in $builtImages) {
    if ($imageId) {
        Invoke-BestEffort @('rmi', '--force', $imageId)
    }
}

$allImages = @(Get-DockerResult @('image', 'ls', '--format', '{{.Repository}}:{{.Tag}}'))
foreach ($name in $ImageNames) {
    $escaped = [regex]::Escape($name)
    $references = @($allImages | Where-Object { $_ -match "(^|/)${escaped}:" })
    foreach ($reference in $references) {
        Write-Host "Removing image $reference"
        Invoke-BestEffort @('rmi', '--force', $reference)
    }
}

Invoke-BestEffort @('image', 'prune', '--force')

Write-Host ''
if (-not $projectPresent) {
    Write-Host "Nothing to remove on disk: $projectDirectory does not exist."
}
elseif ($repositoryCheckout) {
    $appsettingsFile = Join-Path $projectDirectory 'appsettings.json'
    if (Test-Path -LiteralPath $appsettingsFile) {
        Remove-Item -LiteralPath $appsettingsFile -Force
        Write-Host "Removed $appsettingsFile"
    }
    else {
        Write-Host "No appsettings.json config to remove in $projectDirectory"
    }
}
else {
    Remove-DeploymentDirectory $projectDirectory
}

Write-Host ''
Write-Host 'The mgo2 deployment has been removed.'
Write-Host 'Check for leftovers with: docker ps -a, docker images, docker volume ls, docker network ls'
Write-Host 'Docker itself was not removed.'