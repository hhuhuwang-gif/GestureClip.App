#Requires -Version 5.1
<#
.SYNOPSIS
  Incremental build of the app project, used by Start-GestureClip.bat.

.DESCRIPTION
  scripts\publish-win-x64.ps1 runs restore + the full test suite + a self-contained
  single-file publish, which takes minutes. When you only want to look at the current
  working tree (for example after a visual tweak), a plain build is enough and finishes
  in seconds.

  -Configuration Release (launcher default)
      src\GestureClip.App\bin\Release\net8.0-windows10.0.19041.0\GestureClip.exe
  -Configuration Debug (launcher --quick)
      src\GestureClip.App\bin\Debug\net8.0-windows10.0.19041.0\GestureClip.exe
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$dotnet = Get-DotNetSdk

Write-Host "Using .NET SDK host: $dotnet"
Invoke-Checked $dotnet @("build", $project, "-c", $Configuration, "--nologo", "-v", "m") "dotnet build"
