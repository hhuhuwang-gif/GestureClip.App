$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $repoRoot "artifacts\release\GestureClip"
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = $projectXml.Project.PropertyGroup.Version
$zipPath = Join-Path $repoRoot "artifacts\release\GestureClip-v$version-win-x64.zip"

if (-not (Test-Path $releaseDir)) {
    throw "Release directory not found: $releaseDir"
}

$exe = Join-Path $releaseDir "GestureClip.exe"
if (-not (Test-Path $exe)) {
    $fallback = Join-Path $releaseDir "GestureClip.App.exe"
    if (Test-Path $fallback) {
        $exe = $fallback
    }
    else {
        throw "GestureClip executable not found in: $releaseDir"
    }
}

Write-Host "Release directory:" $releaseDir
Write-Host "Executable:" $exe
Write-Host "Release zip:" $zipPath
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Release zip not found: $zipPath"
}
Write-Host ""
Write-Host "Files:"
Get-ChildItem $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host ""
Write-Host "Manual verification: run docs/regression-checklist.md before sharing the build."
