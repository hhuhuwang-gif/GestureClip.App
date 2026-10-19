#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ReleaseRoot = '',
    [switch]$RequireSetup
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$artifactRoot = if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts\release'))
} else {
    [IO.Path]::GetFullPath($ReleaseRoot)
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\GestureClip.App\GestureClip.App.csproj'
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$packageVersion = ([string]$projectXml.Project.PropertyGroup.Version).ToLowerInvariant()
$portableName = "GestureClip-v$packageVersion-win-x64.zip"
$setupName = "GestureClip-Setup-v$packageVersion-win-x64.zip"
$releaseDir = Join-Path $artifactRoot 'GestureClip'
$forbiddenNames = @('gestureclip.db', 'gestureclip.db-shm', 'gestureclip.db-wal', '.git', 'obj', 'logs')

function Assert-PackagePath([string]$RelativePath) {
    foreach ($segment in ($RelativePath -split '[\\/]')) {
        if ($segment -in $forbiddenNames) {
            throw "Forbidden release content: $RelativePath"
        }
    }
}

function Read-Checksums {
    $manifestPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
    $checksums = @{}
    foreach ($line in (Get-Content -LiteralPath $manifestPath)) {
        if ($line -notmatch '^([A-Fa-f0-9]{64})  (.+)$') {
            throw "Invalid SHA256SUMS entry: $line"
        }
        $expectedHash = $Matches[1]
        $name = $Matches[2]
        if ([IO.Path]::GetFileName($name) -ne $name -or $name -in @('.', '..')) {
            throw "Invalid artifact filename in SHA256SUMS: $name"
        }
        if ($checksums.ContainsKey($name)) {
            throw "Duplicate SHA256SUMS entry: $name"
        }
        $actualHash = (Get-FileHash -LiteralPath (Join-Path $artifactRoot $name) -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) {
            throw "SHA256 mismatch: $name"
        }
        $checksums[$name] = $expectedHash
    }
    return $checksums
}

function Get-EntryHash([IO.Compression.ZipArchiveEntry]$Entry) {
    $stream = $Entry.Open()
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '')
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Assert-Archive([string]$Name, [string]$PayloadPrefix) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $artifactRoot $Name))
    try {
        foreach ($entry in $archive.Entries) {
            Assert-PackagePath $entry.FullName
        }
        if ($PayloadPrefix.Length -gt 0) {
            foreach ($installerFile in @('Setup.cmd', 'install.ps1')) {
                if (-not $archive.GetEntry($installerFile)) {
                    throw "Setup ZIP missing ${installerFile}: $Name"
                }
            }
        }
        foreach ($requiredName in @('GestureClip.exe', 'PRIVACY.md')) {
            $entryName = "$PayloadPrefix$requiredName"
            $entries = @($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $entryName })
            if ($entries.Count -ne 1) {
                throw "Archive must contain exactly one ${entryName}: $Name"
            }
            $expectedHash = (Get-FileHash -LiteralPath (Join-Path $releaseDir $requiredName) -Algorithm SHA256).Hash
            if ((Get-EntryHash $entries[0]) -ne $expectedHash) {
                throw "Archive payload differs from published ${requiredName}: $Name"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$null = Get-Item -LiteralPath (Join-Path $releaseDir 'GestureClip.exe')
$null = Get-Item -LiteralPath (Join-Path $releaseDir 'PRIVACY.md')
foreach ($item in (Get-ChildItem -LiteralPath $releaseDir -Recurse -Force)) {
    Assert-PackagePath $item.FullName.Substring($releaseDir.TrimEnd('\', '/').Length)
}
$checksums = Read-Checksums
foreach ($artifact in (Get-ChildItem -LiteralPath $artifactRoot -File | Where-Object { $_.Extension -in @('.zip', '.exe') })) {
    if (-not $checksums.ContainsKey($artifact.Name)) {
        throw "Release artifact missing from SHA256SUMS: $($artifact.Name)"
    }
}
if (-not $checksums.ContainsKey($portableName)) {
    throw "Portable ZIP missing from SHA256SUMS: $portableName"
}
Assert-Archive $portableName ''
if ($RequireSetup -or (Test-Path -LiteralPath (Join-Path $artifactRoot $setupName))) {
    if (-not $checksums.ContainsKey($setupName)) {
        throw "Setup ZIP missing from SHA256SUMS: $setupName"
    }
    Assert-Archive $setupName 'payload/'
}
Write-Host 'Release verification passed: checksums, payload consistency, privacy documentation, local-data exclusion.'
Write-Host 'Manual GUI verification: docs/regression-checklist.md'
