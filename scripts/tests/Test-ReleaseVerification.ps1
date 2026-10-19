#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$checker = Join-Path $repoRoot 'scripts\check-release.ps1'
$project = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'src\GestureClip.App\GestureClip.App.csproj') -Raw)
$version = ([string]$project.Project.PropertyGroup.Version).ToLowerInvariant()
$portableName = "GestureClip-v$version-win-x64.zip"
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('GestureClip-release-tests-' + [guid]::NewGuid())

function New-Fixture {
    $root = Join-Path $fixtureRoot ([guid]::NewGuid().ToString())
    $payload = Join-Path $root 'GestureClip'
    $null = New-Item -ItemType Directory -Path $payload -Force
    Set-Content -LiteralPath (Join-Path $payload 'GestureClip.exe') -Value 'test executable bytes'
    Set-Content -LiteralPath (Join-Path $payload 'PRIVACY.md') -Value 'test privacy documentation'
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath (Join-Path $root $portableName)
    Write-Manifest $root
    return $root
}

function Write-Manifest([string]$Root) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $Root $portableName) -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $Root 'SHA256SUMS.txt') -Value "$hash  $portableName"
}

function Assert-Rejected([string]$Root, [string]$ExpectedMessage, [switch]$RequireSetup) {
    try { & $checker -ReleaseRoot $Root -RequireSetup:$RequireSetup }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessage*") { throw }
        Write-Host "Rejected as expected: $ExpectedMessage"
        return
    }
    throw "Release checker accepted invalid fixture: $ExpectedMessage"
}

try {
$valid = New-Fixture
& $checker -ReleaseRoot $valid
Assert-Rejected $valid 'Setup ZIP missing' -RequireSetup
$unhashed = New-Fixture
Set-Content -LiteralPath (Join-Path $unhashed 'GestureClip-Setup-old.exe') -Value 'old installer'
Assert-Rejected $unhashed 'Release artifact missing from SHA256SUMS'
$corrupted = New-Fixture
Add-Content -LiteralPath (Join-Path $corrupted $portableName) -Value 'corruption'
Assert-Rejected $corrupted 'SHA256 mismatch'
$stale = New-Fixture
Set-Content -LiteralPath (Join-Path $stale 'GestureClip\GestureClip.exe') -Value 'new executable bytes'
Assert-Rejected $stale 'Archive payload differs'
$forbidden = New-Fixture
Set-Content -LiteralPath (Join-Path $forbidden 'GestureClip\gestureclip.db') -Value 'private data'
Compress-Archive -Path (Join-Path $forbidden 'GestureClip\*') -DestinationPath (Join-Path $forbidden $portableName) -Force
Remove-Item -LiteralPath (Join-Path $forbidden 'GestureClip\gestureclip.db')
Write-Manifest $forbidden
Assert-Rejected $forbidden 'Forbidden release content'
$missing = New-Fixture
Set-Content -LiteralPath (Join-Path $missing 'SHA256SUMS.txt') -Value ''
Assert-Rejected $missing 'Invalid SHA256SUMS entry'
$duplicate = New-Fixture
$manifestPath = Join-Path $duplicate 'SHA256SUMS.txt'
Add-Content -LiteralPath $manifestPath -Value (Get-Content -LiteralPath $manifestPath)
Assert-Rejected $duplicate 'Duplicate SHA256SUMS entry'
$missingPrivacy = New-Fixture
Remove-Item -LiteralPath (Join-Path $missingPrivacy 'GestureClip\PRIVACY.md')
Assert-Rejected $missingPrivacy 'Cannot find path'
Write-Host 'Release verification tests passed.'
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedFixture.StartsWith($temporaryRoot + 'GestureClip-release-tests-', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected release-test directory: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
