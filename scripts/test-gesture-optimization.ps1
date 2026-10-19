param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-common.ps1")
$dotnet = Get-DotNetSdk
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$testProject = Join-Path $repoRoot "tests/GestureClip.Tests/GestureClip.Tests.csproj"

function Invoke-TestScope {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    Write-Host ""
    Write-Host "== Gesture optimization regression: $Name =="
    Invoke-DotNetTests $dotnet @(
        "test", $testProject, "--nologo", "--verbosity", "minimal",
        "--configuration", $Configuration, "--filter", $Filter
    )
}

Push-Location $repoRoot
try {
    Invoke-TestScope "Gestures + overlay + hook behavior" "FullyQualifiedName~GestureClip.Tests.Gestures"
    Invoke-TestScope "Hotkeys" "FullyQualifiedName~GestureClip.Tests.Hotkeys"
    Invoke-TestScope "Diagnostics" "FullyQualifiedName~DiagnosticsServiceTests"
    Invoke-TestScope "Settings" "FullyQualifiedName~GestureClip.Tests.Settings"
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Gesture optimization regression pack passed."
