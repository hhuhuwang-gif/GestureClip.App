#Requires -Version 5.1
[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build-common.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'GestureClip.sln'
$dotnet = Get-DotNetSdk
$scopes = @(
    'FullyQualifiedName~ClipboardOverlayViewModelTests.Load',
    'FullyQualifiedName~ClipboardOverlayViewModelTests.SearchText_debounces',
    'FullyQualifiedName~ClipboardServiceTests.CopyItemsAsync',
    'FullyQualifiedName~ClipboardRepositoryTests',
    'FullyQualifiedName~ClipboardRetryPolicyTests',
    'FullyQualifiedName~DirectionGestureRecognizerPerformanceTests',
    'FullyQualifiedName~ClipboardInteractionLatencyTests',
    'FullyQualifiedName~ClipboardOverlayResponsivenessTests',
    'FullyQualifiedName~Stage10UiThreadGuardTests',
    'FullyQualifiedName~GestureOverlayService_throttles_trace_updates'
)
Invoke-DotNetTests $dotnet @(
    'test', $solution, '--configuration', $Configuration,
    '--filter', ($scopes -join '|'), '--verbosity', 'minimal'
)
Write-Host 'Performance regression checks passed.'
