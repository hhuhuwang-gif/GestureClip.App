#Requires -Version 5.1
<#
.SYNOPSIS
  Install or upgrade GestureClip for the current user (no admin required by default).

.DESCRIPTION
  Default install root: %LOCALAPPDATA%\Programs\GestureClip
  User data remains in: %LOCALAPPDATA%\GestureClip\ (never deleted by install/upgrade)
  Supports silent:  powershell -File install.ps1 -Silent
#>
[CmdletBinding()]
param(
    [string]$SourceDir = $PSScriptRoot,
    [string]$InstallDir = "",
    [switch]$Silent,
    [switch]$AllUsers,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$ProductName = "GestureClip"
$ExeName = "GestureClip.exe"
$Publisher = "GestureClip"
$AppId = "GestureClip.App"

function Write-Info([string]$Message) {
    if (-not $Silent) { Write-Host $Message }
}

function Get-DefaultInstallDir {
    if ($AllUsers) {
        return Join-Path ${env:ProgramFiles} $ProductName
    }
    return Join-Path $env:LOCALAPPDATA "Programs\$ProductName"
}

function Stop-GestureClipProcesses {
    Get-Process -Name "GestureClip" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Info "Stopping running GestureClip (PID $($_.Id))..."
        try { $_.CloseMainWindow() | Out-Null } catch {}
        Start-Sleep -Milliseconds 800
        if (-not $_.HasExited) {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function New-Shortcut([string]$LinkPath, [string]$TargetPath, [string]$WorkingDirectory) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($LinkPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "GestureClip - 本地剪贴板历史 + 鼠标手势"
    $shortcut.Save()
}

function Register-UninstallEntry(
    [string]$InstallRoot,
    [string]$Version,
    [string]$UninstallCommand
) {
    $keyPath = if ($AllUsers) {
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
    } else {
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
    }

    if (-not (Test-Path $keyPath)) {
        New-Item -Path $keyPath -Force | Out-Null
    }

    $exe = Join-Path $InstallRoot $ExeName
    $sizeKb = 0
    if (Test-Path $InstallRoot) {
        $sizeKb = [int]((Get-ChildItem -LiteralPath $InstallRoot -Recurse -File -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum / 1KB)
    }

    New-ItemProperty -Path $keyPath -Name "DisplayName" -Value $ProductName -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "DisplayIcon" -Value $exe -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "Publisher" -Value $Publisher -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "InstallLocation" -Value $InstallRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "UninstallString" -Value $UninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "QuietUninstallString" -Value "$UninstallCommand -Silent" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "DisplayVersion" -Value $Version -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "EstimatedSize" -Value $sizeKb -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null
}

function Unregister-UninstallEntry {
    $paths = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
    )
    foreach ($path in $paths) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-PackageVersion([string]$Root) {
    $exe = Join-Path $Root $ExeName
    if (Test-Path $exe) {
        try {
            $v = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
            if (-not [string]::IsNullOrWhiteSpace($v)) { return $v.Trim() }
        } catch {}
    }
    return "0.0.0"
}

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Get-DefaultInstallDir
}

if ($Uninstall) {
    Write-Info "Uninstalling $ProductName from $InstallDir ..."
    Stop-GestureClipProcesses
    $startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\$ProductName.lnk"
    $desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "$ProductName.lnk"
    Remove-Item -LiteralPath $startMenu -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $desktop -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Unregister-UninstallEntry
    Write-Info "Uninstall complete. User data under %LOCALAPPDATA%\GestureClip was preserved."
    exit 0
}

$payload = Join-Path $SourceDir "payload"
if (-not (Test-Path (Join-Path $payload $ExeName))) {
    # Allow running install.ps1 directly from a published folder.
    if (Test-Path (Join-Path $SourceDir $ExeName)) {
        $payload = $SourceDir
    } else {
        throw "Cannot find $ExeName under $SourceDir (expected payload\ or published folder)."
    }
}

$version = Get-PackageVersion $payload
Write-Info "Installing $ProductName $version"
Write-Info "Target: $InstallDir"
Write-Info "User data stays in: $env:LOCALAPPDATA\GestureClip\"

Stop-GestureClipProcesses
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# Copy program files; never touch LocalAppData user DB.
$exclude = @("gestureclip.db", "gestureclip.db-shm", "gestureclip.db-wal", "logs")
Get-ChildItem -LiteralPath $payload -Force | ForEach-Object {
    if ($exclude -contains $_.Name) { return }
    $dest = Join-Path $InstallDir $_.Name
    if ($_.PSIsContainer) {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
    } else {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    }
}

# Write install manifest for updater
$manifest = @{
    product = $ProductName
    version = $version
    installDir = $InstallDir
    installedAt = (Get-Date).ToString("o")
    channel = "win-x64"
} | ConvertTo-Json
Set-Content -LiteralPath (Join-Path $InstallDir "install-manifest.json") -Value $manifest -Encoding UTF8

# Shortcuts
$exePath = Join-Path $InstallDir $ExeName
$startMenuDir = [Environment]::GetFolderPath("StartMenu")
$programs = Join-Path $startMenuDir "Programs"
New-Item -ItemType Directory -Force -Path $programs | Out-Null
New-Shortcut -LinkPath (Join-Path $programs "$ProductName.lnk") -TargetPath $exePath -WorkingDirectory $InstallDir

# Uninstall helper next to app
$uninstallPs1 = Join-Path $InstallDir "uninstall.ps1"
Copy-Item -LiteralPath $PSCommandPath -Destination $uninstallPs1 -Force
$uninstallCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallPs1`" -Uninstall"
Register-UninstallEntry -InstallRoot $InstallDir -Version $version -UninstallCommand $uninstallCmd

Write-Info "Install complete."
if (-not $Silent) {
    $launch = Read-Host "Launch GestureClip now? [Y/n]"
    if ([string]::IsNullOrWhiteSpace($launch) -or $launch -match '^[Yy]') {
        Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
    }
} else {
    # Silent upgrade path used by in-app updater: restart app
    Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
}

exit 0
