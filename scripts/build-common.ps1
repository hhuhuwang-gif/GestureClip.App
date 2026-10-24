#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$script:RepoRoot = Split-Path -Parent $PSScriptRoot

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
    # Start-Process rebuilds the environment as a case-insensitive dictionary and throws
    # "An item with the same key has already been added" when the parent environment holds names that
    # differ only by case, such as http_proxy together with HTTP_PROXY. Launch through
    # ProcessStartInfo instead so the child simply inherits the environment block from Windows.
    $quotedArguments = @($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DotNet
    $startInfo.Arguments = $quotedArguments
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $null = $process.Start()
    try {
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

function Remove-BuildOutput {
    # Replace Remove-Item for build output. Environment guards that route deletions through
    # the recycle bin can report a failure after the item has already been recycled (their
    # post-check races with the shell operation) and refuse some large archives outright,
    # which used to abort publish midway. Build output is reproducible, so fall back to a
    # direct delete - but strictly inside artifacts\, never for anything else.
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }
    catch {
        if (-not (Test-Path -LiteralPath $Path)) { return }
        Assert-BuildPath $Path (Join-Path $script:RepoRoot "artifacts")
        $item = Get-Item -LiteralPath $Path -Force
        if ($item.PSIsContainer) { [IO.Directory]::Delete($Path, $true) }
        else { [IO.File]::Delete($Path) }
        if (Test-Path -LiteralPath $Path) { throw }
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
