#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\obstelegramoverlay"
)

$ErrorActionPreference = 'Stop'

$Repo   = "ArtemKiyashko/obs-telegram-overlay"
$Binary = "obstelegramoverlay_bot.exe"

# ── Detect arch ───────────────────────────────────────────────────────────────
$Arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$Rid  = switch ($Arch) {
    'X64'   { 'win-x64'   }
    'Arm64' { 'win-arm64' }
    default {
        Write-Error "Unsupported architecture: $Arch"
        exit 1
    }
}

# ── Resolve latest release ────────────────────────────────────────────────────
Write-Host "Fetching latest release for $Repo..."
$Release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
$Tag     = $Release.tag_name

if (-not $Tag) {
    Write-Error "Could not determine latest release tag."
    exit 1
}

Write-Host "Latest release: $Tag"

# ── Download ──────────────────────────────────────────────────────────────────
$FileName    = "${Binary}" -replace '\.exe$', "_${Rid}.exe"
$DownloadUrl = "https://github.com/$Repo/releases/download/$Tag/$FileName"
$TmpPath     = Join-Path $env:TEMP $FileName

Write-Host "Downloading $FileName..."
Invoke-WebRequest -Uri $DownloadUrl -OutFile $TmpPath -UseBasicParsing

# ── Install ───────────────────────────────────────────────────────────────────
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir | Out-Null
}

$Dest = Join-Path $InstallDir $Binary
Move-Item -Path $TmpPath -Destination $Dest -Force

Write-Host ""
Write-Host "Installed: $Dest"

# ── Add to user PATH if needed ────────────────────────────────────────────────
$UserPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
if ($UserPath -notlike "*$InstallDir*") {
    $NewPath = "$UserPath;$InstallDir"
    [System.Environment]::SetEnvironmentVariable('PATH', $NewPath, 'User')
    Write-Host ""
    Write-Host "Added $InstallDir to user PATH."
    Write-Host "Restart your terminal for the change to take effect."
}

Write-Host ""
Write-Host "Run: $Binary --bot-api-token YOUR_TOKEN"
