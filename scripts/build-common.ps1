#Requires -Version 5.1
$ErrorActionPreference = "Stop"

function Get-DotNetSdk {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    $candidates = @(
        if ($command) { $command.Source }
        if ($env:DOTNET_ROOT) { Join-Path $env:DOTNET_ROOT "dotnet.exe" }
        Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    ) | Select-Object -Unique
    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $installedSdks = & $candidate --list-sdks
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect .NET SDKs at $candidate (exit code $LASTEXITCODE)."
        }
        if ($installedSdks -match '^8\.') { return $candidate }
    }
    throw ".NET 8 SDK not found. Install it or set DOTNET_ROOT to its installation directory."
}

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments, [string]$Name)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Invoke-DotNetTests {
    param([string]$DotNet, [string[]]$Arguments)
    $testTimeoutMilliseconds = 60000
    $quotedArguments = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    $process = Start-Process -FilePath $DotNet -ArgumentList $quotedArguments -NoNewWindow -PassThru
    try {
        $null = $process.Handle
        if (-not $process.WaitForExit($testTimeoutMilliseconds)) {
            & taskkill.exe /PID $process.Id /T /F
            if ($LASTEXITCODE -ne 0) { throw "Could not terminate timed-out test process $($process.Id)." }
            throw "dotnet test exceeded the 60-second timeout."
        }
        if ($process.ExitCode -ne 0) {
            throw "dotnet test failed with exit code $($process.ExitCode)"
        }
    }
    finally { $process.Dispose() }
}

function Assert-BuildPath {
    param([string]$Path, [string]$WorkspaceRoot)
    $resolvedRoot = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\') + '\'
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Build output must remain inside $resolvedRoot`: $resolvedPath"
    }
}

function Assert-PublishOutputAvailable {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    foreach ($file in (Get-ChildItem -LiteralPath $Path -Recurse -File -Force)) {
        try {
            $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $stream.Dispose()
        }
        catch {
            throw "Published file is locked or not writable: $($file.FullName). Close GestureClip before rebuilding. $($_.Exception.Message)"
        }
    }
}
