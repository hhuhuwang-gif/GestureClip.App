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

    # Wait briefly so file locks release before copy.
    for ($i = 0; $i -lt 10; $i++) {
        $still = Get-Process -Name "GestureClip" -ErrorAction SilentlyContinue
        if (-not $still) { break }
        Start-Sleep -Milliseconds 400
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

function Copy-Payload([string]$From, [string]$To) {
    $excludeNames = @(
        "gestureclip.db",
        "gestureclip.db-shm",
        "gestureclip.db-wal",
        "logs"
    )

    New-Item -ItemType Directory -Force -Path $To | Out-Null

    # Prefer robocopy for reliable overwrite when files are busy.
    $roboArgs = @(
        $From, $To, "/E", "/R:20", "/W:1",
        "/XF", "gestureclip.db", "gestureclip.db-shm", "gestureclip.db-wal",
        "/XD", "logs"
    )
    & robocopy @roboArgs | Out-Null
    $code = $LASTEXITCODE
    if ($code -ge 8) {
        throw "robocopy failed with exit code $code while copying payload."
    }

    # Drop accidental local data if present in source.
    foreach ($name in $excludeNames) {
        $bad = Join-Path $To $name
        if (Test-Path -LiteralPath $bad) {
            Remove-Item -LiteralPath $bad -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
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
Copy-Payload -From $payload -To $InstallDir

# Write install manifest for updater / diagnostics
$manifest = @{
    product     = $ProductName
    version     = $version
    installDir  = $InstallDir
    installedAt = (Get-Date).ToString("o")
    channel     = "win-x64"
} | ConvertTo-Json
Set-Content -LiteralPath (Join-Path $InstallDir "install-manifest.json") -Value $manifest -Encoding UTF8

# Shortcuts
$exePath = Join-Path $InstallDir $ExeName
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Install failed: $exePath was not created."
}

$startMenuDir = [Environment]::GetFolderPath("StartMenu")
$programs = Join-Path $startMenuDir "Programs"
New-Item -ItemType Directory -Force -Path $programs | Out-Null
New-Shortcut -LinkPath (Join-Path $programs "$ProductName.lnk") -TargetPath $exePath -WorkingDirectory $InstallDir

# Uninstall helper next to app
$uninstallPs1 = Join-Path $InstallDir "uninstall.ps1"
Copy-Item -LiteralPath $PSCommandPath -Destination $uninstallPs1 -Force
$uninstallCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallPs1`" -Uninstall"
Register-UninstallEntry -InstallRoot $InstallDir -Version $version -UninstallCommand $uninstallCmd

Write-Info ""
Write-Info "安装完成。"
Write-Info "程序: $exePath"
Write-Info "数据仍在: $env:LOCALAPPDATA\GestureClip\ （不会被安装覆盖）"
Write-Info "开始菜单已添加快捷方式。"
Write-Info ""

if (-not $Silent) {
    Write-Info "正在启动 GestureClip ..."
    Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
    Write-Info "若未自动启动，请从开始菜单打开 GestureClip。"
    Write-Info ""
    Write-Info "（安装过程不会打开任何 .txt 说明文件；本窗口仅作进度显示。）"
    Start-Sleep -Seconds 2
} else {
    # Silent upgrade path used by in-app updater: restart app
    Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
}

exit 0
