$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $repoRoot "artifacts\release\GestureClip"
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
$packageVersion = $version.ToLowerInvariant()
$fullZipPath = Join-Path $repoRoot "artifacts\release\GestureClip-v$packageVersion-win-x64.zip"
$updateZipPath = Join-Path $repoRoot "artifacts\release\GestureClip-v$packageVersion-update-win-x64.zip"
$hashPath = Join-Path $repoRoot "artifacts\release\SHA256SUMS.txt"

if (-not (Test-Path $releaseDir)) {
    throw "Release directory not found: $releaseDir"
}

$exe = Join-Path $releaseDir "GestureClip.exe"
if (-not (Test-Path $exe)) {
    throw "GestureClip executable not found in: $releaseDir"
}

foreach ($path in @($fullZipPath, $updateZipPath, $hashPath)) {
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
Write-Host "Full release zip:" $fullZipPath
Write-Host "Update release zip:" $updateZipPath
Write-Host "SHA256:" $hashPath
Write-Host ""
Write-Host "Files:"
Get-ChildItem $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host ""
Write-Host "SHA256SUMS:"
Get-Content -LiteralPath $hashPath
Write-Host ""
Write-Host "Manual verification: run docs/regression-checklist.md before sharing the build."
