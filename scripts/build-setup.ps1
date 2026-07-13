#Requires -Version 5.1
<#
.SYNOPSIS
  Build a Windows end-user Setup package from the published app folder.

.DESCRIPTION
  Produces:
    artifacts/release/GestureClip-Setup-v{version}-win-x64.zip
      Setup.cmd          <- double-click to install (current user)
      install.ps1
      payload/*          <- program files
    artifacts/release/installer/GestureClip.iss  (optional Inno Setup source)

  Does NOT require admin. Installs by default to:
    %LOCALAPPDATA%\Programs\GestureClip

  User data always remains in:
    %LOCALAPPDATA%\GestureClip\
#>
[CmdletBinding()]
param(
    [string]$PublishDir = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$publishScript = Join-Path $repoRoot "scripts\publish-win-x64.ps1"

if (-not $SkipPublish) {
    Write-Host "Publishing app first..."
    powershell -ExecutionPolicy Bypass -File $publishScript
}

$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
$packageVersion = $version.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $releaseRoot "GestureClip"
}
if (-not (Test-Path (Join-Path $PublishDir "GestureClip.exe"))) {
    throw "Published app not found: $PublishDir\GestureClip.exe — run publish-win-x64.ps1 first."
}

$setupRoot = Join-Path $releaseRoot "setup-staging"
$payload = Join-Path $setupRoot "payload"
Remove-Item $setupRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload | Out-Null

Copy-Item (Join-Path $PublishDir "*") $payload -Recurse -Force
Copy-Item (Join-Path $repoRoot "scripts\install\install.ps1") (Join-Path $setupRoot "install.ps1") -Force
Copy-Item (Join-Path $repoRoot "scripts\install\Setup.cmd") (Join-Path $setupRoot "Setup.cmd") -Force

# Friendly README for end users
@"
# GestureClip 安装说明

## 一键安装（推荐）
1. 解压本 zip
2. 双击 **Setup.cmd**
3. 默认安装到：`%LOCALAPPDATA%\Programs\GestureClip`
4. 开始菜单会出现 GestureClip

## 静默安装 / 覆盖升级
```bat
Setup.cmd /S
```

## 卸载
- 设置 → 应用 → GestureClip → 卸载
- 或运行安装目录下的 uninstall.ps1

## 数据不会丢
剪贴板历史、设置、工位小熊数据在：
`%LOCALAPPDATA%\GestureClip\`
安装/升级/卸载程序文件都不会删除这里。

版本：$version
"@ | Set-Content -LiteralPath (Join-Path $setupRoot "安装说明.txt") -Encoding UTF8

$setupZip = Join-Path $releaseRoot "GestureClip-Setup-v$packageVersion-win-x64.zip"
Remove-Item $setupZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $setupRoot "*") -DestinationPath $setupZip -Force

# Optional Inno Setup script for true single-file Setup.exe when ISCC is available
$issDir = Join-Path $releaseRoot "installer"
New-Item -ItemType Directory -Force -Path $issDir | Out-Null
$issPath = Join-Path $issDir "GestureClip.iss"
$payloadEscaped = $payload.Replace("\", "\\")
@"
; GestureClip Inno Setup script
; Build (if Inno Setup installed):
;   iscc artifacts\release\installer\GestureClip.iss
; Output: artifacts\release\GestureClip-Setup-v$packageVersion-win-x64.exe

#define MyAppName "GestureClip"
#define MyAppVersion "$version"
#define MyAppPublisher "GestureClip"
#define MyAppExeName "GestureClip.exe"
#define MyAppId "GestureClip.App"

[Setup]
AppId={{A7C3E8F1-4B2D-4E9A-9C11-GESTURECLIP01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=$($releaseRoot.Replace('\','\\'))
OutputBaseFilename=GestureClip-Setup-v$packageVersion-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
Source: "$payloadEscaped\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 GestureClip"; Flags: nowait postinstall skipifsilent

[Code]
// User data under %LOCALAPPDATA%\GestureClip is intentionally never removed.
"@ | Set-Content -LiteralPath $issPath -Encoding UTF8

$setupExe = $null
if (Get-Command iscc -ErrorAction SilentlyContinue) {
    Write-Host "Inno Setup found — building Setup.exe..."
    & iscc $issPath
    $candidate = Join-Path $releaseRoot "GestureClip-Setup-v$packageVersion-win-x64.exe"
    if (Test-Path $candidate) { $setupExe = $candidate }
} else {
    Write-Host "Inno Setup (iscc) not found — Setup.zip is ready; install Inno Setup to also produce Setup.exe."
}

# Update SHA256SUMS to include setup package(s)
$hashPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$lines = @()
if (Test-Path $hashPath) {
    $lines = Get-Content $hashPath | Where-Object { $_ -and ($_ -notmatch "Setup") }
}
foreach ($file in @($setupZip, $setupExe)) {
    if (-not $file -or -not (Test-Path $file)) { continue }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    $lines += "$hash  $([IO.Path]::GetFileName($file))"
}
$lines | Set-Content -LiteralPath $hashPath -Encoding UTF8

Write-Host ""
Write-Host "=== Setup package ready ==="
Write-Host "Install zip : $setupZip"
if ($setupExe) { Write-Host "Install exe : $setupExe" }
Write-Host "Inno script : $issPath"
Write-Host "End users: unzip Setup zip -> double-click Setup.cmd"
Write-Host "Silent   : Setup.cmd /S"
Write-Host "Data dir : %LOCALAPPDATA%\GestureClip\ (preserved)"
