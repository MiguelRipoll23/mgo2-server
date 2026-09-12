<#
.SYNOPSIS
    Builds every project of the solution with the .NET SDK.

.DESCRIPTION
    The servers are started from these binaries by scripts/run-windows.ps1,
    which builds nothing on its own; run this script again after changing the
    code.

    The .NET SDK is installed into the user profile on the first run when it is
    missing. No elevation is required and none is looked for.

.PARAMETER Configuration
    Build configuration, Debug by default. MGO2_CONFIGURATION sets it as well.

.EXAMPLE
    .\scripts\build-windows.ps1

.EXAMPLE
    .\scripts\build-windows.ps1 -Configuration Release
#>

[CmdletBinding()]
param(
    [string]$Configuration = $env:MGO2_CONFIGURATION
)

$ErrorActionPreference = 'Stop'

$SolutionName = 'Mgo2Server.slnx'
$DotNetChannel = '10.0'

# Runs a native command and stops with a clear message when it fails.
function Invoke-Native([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
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
    Initialize-DotNet

    Write-Host ''
    Write-Host "Building every project ($Configuration)"
    Invoke-Native 'dotnet' @('build', $SolutionName, '--configuration', $Configuration)

    Write-Host ''
    Write-Host "Built $Configuration binaries into src/*/bin/$Configuration/net10.0; start them with .\scripts\run-windows.ps1"
}
finally {
    Pop-Location
}
