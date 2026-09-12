<#
.SYNOPSIS
    Runs the whole deployment with the .NET SDK instead of containers.

.DESCRIPTION
    Runs the gate, the account server, one gameplay lobby per process (Free
    Battle, Replays, Survival, Basic Training, Combat Training, Survival Hosts,
    Automatching, Registration, Tournament), a gameplay server, the HTTP API and
    the name server: the same servers compose.yaml starts, minus PostgreSQL.

    PostgreSQL is not started by this script. The servers connect to the remote
    database of DATABASE_CONNECTION_STRING, which must be set in .env or in the
    environment. The compose placeholder (Host=postgres) is ignored. Either the
    keyword form Npgsql reads or the postgresql:// URL form Neon and other
    providers hand out is accepted; the URL is translated before it reaches the
    servers.

    The schema of an empty database is created by the servers themselves on
    their first start, so nothing has to be applied by hand.

    The .NET runtime is what the servers are exec'd through, so the SDK is
    installed into the user profile on the first run when it is missing. The
    binaries themselves are built by scripts/build-windows.ps1, which this
    script refuses to start without. No elevation is required and none is
    looked for: the servers bind their own ports, and every one of them is
    configurable through .env.

    The servers run in the foreground and Ctrl+C stops all of them. In Debug
    builds their output goes to .logs\<server>.log; Release builds write to
    the console only.

    Every other setting is read from .env, which is created from .env.example on
    the first run and never overwritten. The process environment wins over .env.

.PARAMETER Configuration
    Build configuration, Debug by default. MGO2_CONFIGURATION sets it as well.

.EXAMPLE
    .\scripts\run-windows.ps1

.EXAMPLE
    .\scripts\run-windows.ps1 -Configuration Release
#>

[CmdletBinding()]
param(
    [string]$Configuration = $env:MGO2_CONFIGURATION
)

$ErrorActionPreference = 'Stop'

$SolutionName = 'Mgo2Server.slnx'

$DotNetChannel = '10.0'

# Database connection string. Required; set DATABASE_CONNECTION_STRING in the
# environment or in .env. The compose placeholder (Host=postgres) is ignored.

# Npgsql keyword, keyed by the libpq parameter a PostgreSQL URL carries. Npgsql
# reads keyword=value pairs only, so a URL is translated through this table
# instead of being handed to the servers as it is.
$NpgsqlKeywords = @{
    'sslmode'          = 'SSL Mode'
    'channel_binding'  = 'Channel Binding'
    'application_name' = 'Application Name'
    'connect_timeout'  = 'Timeout'
}

# The servers this script started, so a failure anywhere still stops them.
$serverProcesses = @()
$serverLabels = @()
$stopping = $false

# Copies .env into the process environment, so the servers see the same settings
# they would get from compose. A setting that is already in the environment is
# left alone, which is what lets the shell override .env.
function Import-EnvFile([string]$Path) {
    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) {
            continue
        }

        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) {
            continue
        }

        $name = $trimmed.Substring(0, $separator)
        if ($name -notmatch '^[A-Za-z0-9_]+$') {
            continue
        }

        if ($null -eq [Environment]::GetEnvironmentVariable($name, 'Process')) {
            [Environment]::SetEnvironmentVariable($name, $trimmed.Substring($separator + 1), 'Process')
        }
    }
}

# Reports whether a dotnet command able to build net10.0 is on the PATH.
function Test-DotNetUsable {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'SilentlyContinue'
    try {
        $version = (& dotnet --version 2>$null | Select-Object -First 1)
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    return [bool]($version -match '^(1[0-9]|[2-9][0-9])\.')
}

# Puts a locally installed SDK on the PATH, whether it was installed by this
# script or by the user.
function Add-DotNetToPath([string]$InstallDirectory) {
    if (-not (Test-Path (Join-Path $InstallDirectory 'dotnet.exe'))) {
        return
    }

    $env:DOTNET_ROOT = $InstallDirectory
    $env:PATH = "$InstallDirectory;$env:PATH"
}

# Installs the .NET SDK the projects target when the machine has none. The
# installer runs as a child process because it exits the session it runs in.
function Install-DotNet([string]$InstallDirectory) {
    $installerPath = Join-Path ([IO.Path]::GetTempPath()) 'dotnet-install.ps1'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath

    Write-Host "The .NET SDK was not found; installing .NET $DotNetChannel into $InstallDirectory"
    $installer = Start-Process -FilePath 'powershell.exe' -NoNewWindow -Wait -PassThru -ArgumentList @(
        '-NoProfile'
        '-ExecutionPolicy'
        'Bypass'
        '-File'
        $installerPath
        '-Channel'
        $DotNetChannel
        '-InstallDir'
        $InstallDirectory
    )

    Remove-Item -Path $installerPath -Force

    if ($installer.ExitCode -ne 0) {
        throw "the .NET SDK installer failed with exit code $($installer.ExitCode)"
    }
}

# Installs the .NET SDK when the machine has none, and reports the version that
# will build the projects.
function Initialize-DotNet {
    $installDirectory = if ($env:DOTNET_INSTALL_DIR) { $env:DOTNET_INSTALL_DIR } else { Join-Path $env:USERPROFILE '.dotnet' }

    Add-DotNetToPath $installDirectory
    if (Test-DotNetUsable) {
        return
    }

    Install-DotNet $installDirectory
    Add-DotNetToPath $installDirectory

    if (-not (Test-DotNetUsable)) {
        throw 'the .NET SDK was installed but is not usable; open a new PowerShell window and run again'
    }

    Write-Host "Installed $(dotnet --version)"
}

# Rewrites a postgresql:// URL as the keyword=value string Npgsql reads,
# percent-decoding the credentials and the database name and carrying the
# parameters it knows over. A string that is already keyword=value is returned
# untouched.
function ConvertTo-NpgsqlConnectionString([string]$ConnectionString) {
    if ($ConnectionString -notmatch '(?i)^\s*postgres(ql)?://') {
        return $ConnectionString
    }

    $uri = [Uri]$ConnectionString
    if (-not $uri.Host) {
        throw 'the PostgreSQL URL of the connection string has no host'
    }

    $settings = @("Host=$($uri.Host)")
    if ($uri.Port -gt 0) {
        $settings += "Port=$($uri.Port)"
    }

    $database = $uri.AbsolutePath.Trim('/')
    if ($database) {
        $settings += "Database=$([Uri]::UnescapeDataString($database))"
    }

    if ($uri.UserInfo) {
        $credentials = $uri.UserInfo -split ':', 2
        $settings += "Username=$([Uri]::UnescapeDataString($credentials[0]))"
        if ($credentials.Count -gt 1) {
            $settings += "Password=$([Uri]::UnescapeDataString($credentials[1]))"
        }
    }

    foreach ($parameter in $uri.Query.TrimStart('?') -split '&') {
        if (-not $parameter) {
            continue
        }

        $pair = $parameter -split '=', 2
        $name = [Uri]::UnescapeDataString($pair[0]).ToLowerInvariant()
        $value = if ($pair.Count -gt 1) { [Uri]::UnescapeDataString($pair[1]) } else { '' }

        if ($NpgsqlKeywords.ContainsKey($name)) {
            $settings += "$($NpgsqlKeywords[$name])=$value"
        }
        else {
            Write-Host "warning: '$name' of the connection string has no Npgsql equivalent and is ignored" -ForegroundColor Yellow
        }
    }

    return ($settings -join ';')
}

# Reports the connection string of the remote database: the one in the process
# environment or the one in .env, ignoring the compose placeholder. The schema
# of an empty database is created by the servers on their first start.
function Resolve-DatabaseConnectionString {
    $connectionString = $env:DATABASE_CONNECTION_STRING

    if ([string]::IsNullOrWhiteSpace($connectionString) -or $connectionString -match '(?i)(^|;)\s*Host=postgres\s*(;|$)') {
        throw 'DATABASE_CONNECTION_STRING is not set or is the compose placeholder. Set it in .env or in the environment'
    }

    $connectionString = ConvertTo-NpgsqlConnectionString $connectionString

    Write-Host "Using the remote database $($connectionString -replace '(?i)(Password=)[^;]*', '${1}***')"
    return $connectionString
}

# Starts one built server. The settings are name=value pairs applied to this
# one process only. In Debug builds the app writes log files through Serilog
# when LOG_DIRECTORY is set; Release builds write to the console only.
function Start-Server([string]$Name, [string]$Project, [string]$Label, [hashtable]$Settings) {
    $assembly = "Mgo2Server.$Project"
    $dll = Join-Path $projectDirectory "src/$Project/bin/$Configuration/net10.0/$assembly.dll"
    if (-not (Test-Path $dll)) {
        throw "$dll was not built; run .\scripts\build-windows.ps1 first"
    }

    # Merge lobby settings and the log directory into one environment change
    # block so Start-Process sees them both and the session is left as it was.
    $envOverrides = @{}
    foreach ($setting in $Settings.Keys) {
        $envOverrides[$setting] = [string]$Settings[$setting]
    }

    # Always pass LOG_DIRECTORY so Serilog writes to disk regardless of
    # build configuration. The app no longer relies on shell redirection.
    $envOverrides['LOG_DIRECTORY'] = $logDirectory

    $savedSettings = @{}
    foreach ($name in $envOverrides.Keys) {
        $savedSettings[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $envOverrides[$name], 'Process')
    }

    try {
        $process = Start-Process -FilePath 'dotnet' -NoNewWindow -PassThru `
            -ArgumentList $dll `
            -WorkingDirectory $projectDirectory
    }
    finally {
        foreach ($name in $envOverrides.Keys) {
            [Environment]::SetEnvironmentVariable($name, $savedSettings[$name], 'Process')
        }
    }

    $script:serverProcesses += $process
    $script:serverLabels += $Label
}

# Stops every server this script started.
function Stop-Servers {
    if ($script:stopping -or $script:serverProcesses.Count -eq 0) {
        return
    }

    $script:stopping = $true
    Write-Host ''
    Write-Host 'Stopping every server'

    foreach ($process in $script:serverProcesses) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    }

    Wait-Process -Id $script:serverProcesses.Id -ErrorAction SilentlyContinue
}

if (-not $Configuration) {
    $Configuration = 'Debug'
}

# A script started from disk next to the solution runs against that checkout.
if ($PSScriptRoot -and (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) $SolutionName))) {
    $projectDirectory = Split-Path -Parent $PSScriptRoot
}
elseif (Test-Path (Join-Path $PWD.Path $SolutionName)) {
    $projectDirectory = $PWD.Path
}
else {
    throw "$SolutionName was not found; run this script from a checkout of the repository"
}

Push-Location $projectDirectory
try {
    if (-not (Test-Path '.env')) {
        Copy-Item '.env.example' '.env'
        Write-Host 'Created .env from .env.example. Review it before exposing the deployment.'
    }

    Import-EnvFile (Join-Path $projectDirectory '.env')
    $env:DATABASE_CONNECTION_STRING = Resolve-DatabaseConnectionString

    $logDirectory = Join-Path $projectDirectory '.logs'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

    Initialize-DotNet

    $httpPort = if ($env:HTTP_PORT) { $env:HTTP_PORT } else { '80' }
    $dnsPort = if ($env:DNS_PORT) { $env:DNS_PORT } else { '53' }
    $launcherServer = if ($env:LAUNCHER_SERVER) { $env:LAUNCHER_SERVER } else { 'http://mgo2pc.com' }

    try {
        Write-Host ''
        Write-Host 'Starting every server'

        Start-Server 'gate-lobby-5731' 'GateLobbyServer' 'Gate (5731/tcp)' @{}
        Start-Server 'account-lobby-5732' 'AccountLobbyServer' 'Account (5732/tcp)' @{}

        # One process per gameplay lobby, with the identity and the attributes
        # the compose file gives each container.
        $lobbies = @(
            @{ Name = 'Free Battle'; Subtype = 'FREE BATTLE'; Port = '5733' }
            @{ Name = 'Replays'; Subtype = 'TRAINING'; Port = '5734' }
            @{ Name = 'Survival'; Subtype = 'SURVIVAL'; Port = '5735' }
            @{ Name = 'Basic Training'; Subtype = 'TRAINING'; Port = '5737' }
            @{ Name = 'Combat Training'; Subtype = 'TRAINING'; Port = '5738' }
            @{ Name = 'Survival Hosts'; Subtype = 'UNKNOWN'; Port = '5739' }
            @{ Name = 'Automatching'; Subtype = 'AUTOMATCHING'; Port = '5740' }
            @{ Name = 'Registration'; Subtype = 'TOURNAMENT REGISTRATION'; Port = '5741' }
            @{ Name = 'Tournament'; Subtype = 'TOURNAMENT'; Port = '5742' }
        )

        foreach ($lobby in $lobbies) {
            Start-Server "game-lobby-$($lobby.Port)" 'GameLobbyServer' "$($lobby.Name) ($($lobby.Port)/tcp)" @{
                LOBBY_NAME = $lobby.Name
                LOBBY_SUBTYPE = $lobby.Subtype
                LOBBY_PORT = $lobby.Port
                LOBBY_BEGINNER_ONLY = 'false'
                LOBBY_EXPANSION_ONLY = 'false'
                LOBBY_NO_HEADSHOT = 'false'
                LOBBY_REPLAYS_ONLY = 'false'
            }
        }

        Start-Server 'gameplay-5730' 'GameplayServer' 'Gameplay server (5730/udp)' @{
            GAMEPLAY_SERVER_PORT = '5730'
            GAMEPLAY_SERVER_LOBBY_NAME = 'Free Battle'
            P2P_HOST = '127.0.0.1'
        }

        Start-Server 'http' 'Http' "HTTP API ($httpPort/tcp)" @{
            HTTP_PORT = $httpPort
            LAUNCHER_SERVER = $launcherServer
        }

        Start-Server 'dns' 'Dns' "DNS ($dnsPort/udp)" @{
            DNS_PORT = $dnsPort
        }

        # Give the processes a moment to fail fast before the state is reported.
        Start-Sleep -Seconds 3

        $expected = $script:serverProcesses.Count
        $running = 0
        for ($index = 0; $index -lt $expected; $index++) {
            if (-not $script:serverProcesses[$index].HasExited) {
                $running++
                Write-Host "  running  $($script:serverLabels[$index])"
            }
            else {
                Write-Host "  stopped  $($script:serverLabels[$index])"
            }
        }

        Write-Host ''
        if ($running -ne $expected) {
            $hint = if ($logDirectory) { "; see $logDirectory" } else { '' }
            Write-Host "error: $running of $expected servers are running$hint" -ForegroundColor Red
        }

        Write-Host "Started $running of $expected servers."
        Write-Host "The gate listens on 5731, the account server on 5732 and the HTTP API on $httpPort."
        if ($logDirectory) {
            Write-Host "Logs are in $logDirectory; press Ctrl+C to stop every server."
        }
        else {
            Write-Host 'Press Ctrl+C to stop every server.'
        }

        # Stays in the foreground until the last server exits or the user
        # interrupts it, which is when the servers are stopped.
        Wait-Process -Id $script:serverProcesses.Id -ErrorAction SilentlyContinue
    }
    finally {
        Stop-Servers
    }

    Write-Host 'Every server has stopped'
}
finally {
    Pop-Location
}
