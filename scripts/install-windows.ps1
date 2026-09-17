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
    creates a deployment directory (.\mgo2-server, or MGO2_HOME) and downloads
    compose.yaml and appsettings.example.json into it. Everything else is
    configured in appsettings.json, which is created from appsettings.example.json
    on the first run with a random JWT_SECRET and never overwritten afterwards,
    except for ADVERTISED_ADDRESS: the script detects the private address of this
    machine and writes it there so clients on the network can reach the published
    ports.

    Set ADVERTISED_ADDRESS to skip detection. Set MGO2_LOG_LEVEL to answer the
    log-level question without a prompt, and MGO2_TELEMETRY to answer the
    telemetry question without a prompt. Telemetry defaults to yes: the servers
    then send their metrics over gRPC on port 4317, which OTEL_PORT changes. The
    collector the metrics are sent to is the operator's change: nothing of it is
    touched or checked here. With telemetry off no OpenTelemetry integration is
    configured.

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
$DeploymentDirectoryName = 'mgo2-server'

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
    $current = Read-JsonValue 'LOG_LEVEL' $ProjectDirectory
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

# Answers true for a yes/no answer.
function Test-YesNo([string]$Value) {
    return $Value -imatch '^(y|yes|true|1|n|no|false|0)$'
}

# Spells a yes/no answer the one way appsettings.json stores it.
function Get-NormalisedYesNo([string]$Value) {
    if ($Value -imatch '^(y|yes|true|1)$') {
        return 'true'
    }

    return 'false'
}

# Answers true for a whole number that is a usable port.
function Test-Port([string]$Value) {
    if ($Value -notmatch '^\d{1,5}$') {
        return $false
    }

    return [int]$Value -ge 1 -and [int]$Value -le 65535
}

# Reports whether the servers should send telemetry. Yes is the default, so an
# unattended run installs with it on; MGO2_TELEMETRY answers without a prompt.
function Resolve-Telemetry([string]$ProjectDirectory) {
    if ($env:MGO2_TELEMETRY) {
        if (Test-YesNo $env:MGO2_TELEMETRY) {
            return (Get-NormalisedYesNo $env:MGO2_TELEMETRY)
        }

        Write-Host "warning: MGO2_TELEMETRY='$($env:MGO2_TELEMETRY)' is not a yes or no; using yes" -ForegroundColor Yellow
        return 'true'
    }

    # A deployment that was installed with telemetry off stays off by default,
    # so running the script again to install an update does not silently turn it
    # on; a fresh deployment starts with it on.
    $current = Read-JsonValue 'OTEL_ENABLED' $ProjectDirectory
    $defaultChoice = if ($current -eq 'false') { 'no' } else { 'yes' }

    # A run without an interactive host cannot be asked anything, so the default
    # is what such a run gets.
    if (-not [Environment]::UserInteractive) {
        return (Get-NormalisedYesNo $defaultChoice)
    }

    Write-Host ''
    Write-Host 'Should the servers send telemetry over OpenTelemetry?'
    Write-Host '  yes) Export metrics over gRPC (default)'
    Write-Host '  no)  Do not configure OpenTelemetry'

    $answer = Read-Host "Send telemetry? [$defaultChoice]"
    if ([string]::IsNullOrWhiteSpace($answer)) {
        $answer = $defaultChoice
    }

    if (-not (Test-YesNo $answer)) {
        Write-Host "warning: '$answer' is not a yes or no; using $defaultChoice" -ForegroundColor Yellow
        $answer = $defaultChoice
    }

    return (Get-NormalisedYesNo $answer)
}

# Reports the port the OTLP/gRPC collector the metrics are sent to listens on. 4317 is
# the default; OTEL_PORT in the environment or in appsettings.json answers without
# a prompt.
function Resolve-OtelPort([string]$ProjectDirectory) {
    if ($env:OTEL_PORT -and (Test-Port $env:OTEL_PORT)) {
        return $env:OTEL_PORT
    }

    $current = Read-JsonValue 'OTEL_PORT' $ProjectDirectory
    if (Test-Port $current) {
        return $current
    }

    return '4317'
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

    # The level decides what a container writes to its standard output, so it is
    # answered and written before the images are pulled.
    $resolvedLogLevel = Resolve-LogLevel $projectDirectory
    Set-JsonValue 'LOG_LEVEL' $resolvedLogLevel $true $settingsFile

    # Whether the servers send telemetry is answered and written before the
    # images are pulled. Pointing the collector at the chosen port is the
    # operator's change: nothing about the collector is touched here.
    $resolvedTelemetry = Resolve-Telemetry $projectDirectory
    if ($resolvedTelemetry -eq 'true') {
        $resolvedOtelPort = Resolve-OtelPort $projectDirectory
        Set-JsonValue 'OTEL_ENABLED' 'true' $false $settingsFile
        Set-JsonValue 'OTEL_PORT' $resolvedOtelPort $false $settingsFile
        Write-Host ''
        Write-Host "OpenTelemetry is enabled: the servers send their metrics over gRPC on port $resolvedOtelPort."
        Write-Host 'Set OTEL_PORT to change the port; the collector has to listen on the same one.'
    }
    else {
        Set-JsonValue 'OTEL_ENABLED' 'false' $false $settingsFile
        Write-Host ''
        Write-Host 'OpenTelemetry is disabled: no OpenTelemetry integration is configured.'
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

    $accountHost = Read-JsonValue 'ADVERTISED_ADDRESS' $projectDirectory
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
