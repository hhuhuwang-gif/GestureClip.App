#Requires -Version 5.1
<#
.SYNOPSIS
  Print STALE when source files are newer than the published executable.

.DESCRIPTION
  Start-GestureClip.bat starts a build of the working tree (Release by default, Debug with
  --quick), so any copy can silently lag behind the sources. This helper lets the launcher
  decide between "launch now" and "rebuild first" with one cheap timestamp comparison.

  Output is a single token on stdout: STALE, FRESH or UNKNOWN.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$SourceRoot
)

$ErrorActionPreference = "SilentlyContinue"

if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) { "UNKNOWN"; exit 0 }
if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { "UNKNOWN"; exit 0 }

$exeTime = (Get-Item -LiteralPath $Exe).LastWriteTimeUtc
$newest = Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Include *.cs, *.xaml, *.csproj |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $newest) { "UNKNOWN"; exit 0 }
if ($newest.LastWriteTimeUtc -gt $exeTime) { "STALE" } else { "FRESH" }
