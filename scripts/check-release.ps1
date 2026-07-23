$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $repoRoot "artifacts\release\GestureClip"
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
$packageVersion = $version.ToLowerInvariant()
$fullZipPath = Join-Path $repoRoot "artifacts\release\GestureClip-v$packageVersion-win-x64.zip"
$hashPath = Join-Path $repoRoot "artifacts\release\SHA256SUMS.txt"

if (-not (Test-Path $releaseDir)) {
    throw "Release directory not found: $releaseDir"
}

$exe = Join-Path $releaseDir "GestureClip.exe"
if (-not (Test-Path $exe)) {
    throw "GestureClip executable not found in: $releaseDir"
}

foreach ($path in @($fullZipPath, $hashPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Release artifact not found: $path"
    }
}

$forbidden = @(
    "gestureclip.db",
    "gestureclip.db-shm",
    "gestureclip.db-wal",
    ".git",
    "obj",
    "logs"
)

$files = Get-ChildItem $releaseDir -Recurse -Force
foreach ($item in $files) {
    foreach ($pattern in $forbidden) {
        if ($item.FullName -match [regex]::Escape($pattern)) {
            throw "Forbidden release content found: $($item.FullName)"
        }
    }
}

Write-Host "Release directory:" $releaseDir
Write-Host "Executable:" $exe
Write-Host "Release zip:" $fullZipPath
Write-Host "SHA256:" $hashPath
Write-Host ""
Write-Host "Files:"
Get-ChildItem $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host ""
Write-Host "SHA256SUMS:"
Get-Content -LiteralPath $hashPath
Write-Host ""

$setupZip = Join-Path $repoRoot "artifacts\release\GestureClip-Setup-v$packageVersion-win-x64.zip"
$installScript = Join-Path $repoRoot "scripts\install\install.ps1"
$setupCmd = Join-Path $repoRoot "scripts\install\Setup.cmd"
$buildSetup = Join-Path $repoRoot "scripts\build-setup.ps1"
$signScript = Join-Path $repoRoot "scripts\sign-release.ps1"

if (-not (Test-Path -LiteralPath $installScript)) {
    throw "Installer script missing: $installScript"
}
if (-not (Test-Path -LiteralPath $setupCmd)) {
    throw "Setup.cmd missing: $setupCmd"
}
if (-not (Test-Path -LiteralPath $buildSetup)) {
    throw "build-setup.ps1 missing: $buildSetup"
}
if (-not (Test-Path -LiteralPath $signScript)) {
    throw "sign-release.ps1 missing: $signScript"
}

if (Test-Path -LiteralPath $setupZip) {
    Write-Host "Setup zip:" $setupZip
} else {
    Write-Host "Setup zip not built yet (optional until scripts/build-setup.ps1 is run)."
}


Write-Host "Manual verification: run docs/regression-checklist.md before sharing the build."
