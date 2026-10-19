#Requires -Version 5.1
<#
.SYNOPSIS
  Sign release binaries when a code-signing certificate is available.

.DESCRIPTION
  Looks for (in order):
    1) -PfxPath / -PfxPassword parameters
    2) env GESTURECLIP_SIGN_PFX + GESTURECLIP_SIGN_PASSWORD
    3) env GESTURECLIP_SIGN_THUMBPRINT (certificate store CurrentUser\My or LocalMachine\My)

  If no certificate is configured, exits 0 with a clear skip message
  so local / CI builds still work without secrets.

.EXAMPLE
  .\scripts\sign-release.ps1 -Path artifacts\release\GestureClip
  .\scripts\sign-release.ps1 -Path artifacts\release\GestureClip.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [string]$PfxPath = $env:GESTURECLIP_SIGN_PFX,
    [string]$PfxPassword = $env:GESTURECLIP_SIGN_PASSWORD,
    [string]$Thumbprint = $env:GESTURECLIP_SIGN_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$Description = "GestureClip"
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Get-FilesToSign([string]$Target) {
    if (Test-Path -LiteralPath $Target -PathType Leaf) {
        return @(Get-Item -LiteralPath $Target)
    }
    if (-not (Test-Path -LiteralPath $Target -PathType Container)) {
        throw "Path not found: $Target"
    }
    return @(Get-ChildItem -LiteralPath $Target -Recurse -File |
        Where-Object { $_.Extension -match '^\.(exe|dll|msi)$' -and $_.Name -notmatch '\.vshost\.' })
}

$hasPfx = -not [string]::IsNullOrWhiteSpace($PfxPath)
$hasThumb = -not [string]::IsNullOrWhiteSpace($Thumbprint)

if (-not $hasPfx -and -not $hasThumb) {
    Write-Host "sign-release: no certificate configured (GESTURECLIP_SIGN_PFX / THUMBPRINT). Skipping."
    exit 0
}

if ($hasPfx -and -not (Test-Path -LiteralPath $PfxPath -PathType Leaf)) {
    throw "Signing certificate not found: $PfxPath"
}

$signtool = Find-SignTool
if (-not $signtool) {
    throw "sign-release: certificate configured but signtool.exe not found."
}

$files = Get-FilesToSign $Path
if ($files.Count -eq 0) {
    Write-Host "sign-release: no binaries to sign under $Path"
    exit 0
}

Write-Host "sign-release: signing $($files.Count) file(s) with $signtool"

foreach ($file in $files) {
    $signArguments = @(
        "sign",
        "/fd", "SHA256",
        "/td", "SHA256",
        "/tr", $TimestampUrl,
        "/d", $Description
    )

    if ($hasPfx) {
        $signArguments += @("/f", $PfxPath)
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $signArguments += @("/p", $PfxPassword)
        }
    } else {
        $signArguments += @("/sha1", $Thumbprint)
    }

    $signArguments += $file.FullName
    & $signtool @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $($file.FullName) with exit code $LASTEXITCODE"
    }
    Write-Host "  signed: $($file.Name)"
}

Write-Host "sign-release: done."
