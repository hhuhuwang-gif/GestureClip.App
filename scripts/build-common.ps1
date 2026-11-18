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

function Format-WorkflowAnnotation {
    # GitHub 只认 %0A 换行与 %25 转义；注解消息过长会被截断，所以只保留最有用的尾部。
    param([string]$Text, [int]$MaxLength = 3500)
    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
    $escaped = $Text.Trim() -replace '%', '%25' -replace "`r`n", "%0A" -replace "`n", "%0A" -replace "`r", "%0A"
    if ($escaped.Length -le $MaxLength) { return $escaped }
    return "..." + $escaped.Substring($escaped.Length - $MaxLength)
}

function Get-TrxFailureSummary {
    # 从 TRX 里挑出失败测试名、实际耗时与断言/堆栈信息，避免整段堆栈淹没注解。
    param([string]$Directory, [int]$MaxLength = 3500)
    if (-not (Test-Path -LiteralPath $Directory)) { return "" }
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($file in (Get-ChildItem -LiteralPath $Directory -Filter *.trx -File -ErrorAction SilentlyContinue)) {
        try { [xml]$document = Get-Content -LiteralPath $file.FullName -Raw } catch { continue }
        $results = @($document.TestRun.Results.UnitTestResult)
        foreach ($result in $results) {
            if ($result.outcome -ne 'Failed' -and $result.outcome -ne 'Error') { continue }
            $message = ""
            $frames = @()
            if ($result.Output -and $result.Output.ErrorInfo) {
                $message = [string]$result.Output.ErrorInfo.Message
                if ($result.Output.ErrorInfo.StackTrace) {
                    $frames = @(([string]$result.Output.ErrorInfo.StackTrace) -split "`r?`n" |
                        Where-Object { $_.Trim() } | Select-Object -First 3)
                }
            }
            $message = ($message -replace '\s+', ' ').Trim()
            if ($message.Length -gt 400) { $message = $message.Substring(0, 400) + "..." }
            $entry = "$($result.outcome) [$($result.duration)] $($result.testName) :: $message"
            if ($frames.Count -gt 0) {
                $entry += " @ " + (($frames | ForEach-Object { $_.Trim() }) -join " <- ")
            }
            $lines.Add($entry)
        }
    }
    if ($lines.Count -eq 0) { return "" }
    return Format-WorkflowAnnotation ($lines -join "`n") $MaxLength
}

function Invoke-DotNetTests {
    # CI runner 上首次还原 + 编译 + 跑完整 WPF 测试用 60 秒是跑不完的，超时会伪装成“测试失败”，
    # 因此默认放宽到 15 分钟；个别性能脚本需要更严格的时限时显式传 TimeoutSeconds。
    param([string]$DotNet, [string[]]$Arguments, [int]$TimeoutSeconds = 900)
    # Start-Process rebuilds the environment as a case-insensitive dictionary and throws
    # "An item with the same key has already been added" when the parent environment holds names that
    # differ only by case, such as http_proxy together with HTTP_PROXY. Launch through
    # ProcessStartInfo instead so the child simply inherits the environment block from Windows.
    $resultsDirectory = Join-Path $script:RepoRoot "artifacts\test-results"
    $effectiveArguments = @($Arguments)
    if ($Arguments.Count -gt 0 -and $Arguments[0] -eq 'test') {
        # 有 TRX 才能把失败测试名写成 CI 注记；日志需要管理员权限时也能定位问题。
        if (Test-Path -LiteralPath $resultsDirectory) {
            Get-ChildItem -LiteralPath $resultsDirectory -Filter *.trx -File -ErrorAction SilentlyContinue |
                ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }
        }
        $effectiveArguments += @(
            '--logger', 'trx;LogFileName=gestureclip-tests.trx',
            '--results-directory', $resultsDirectory
        )
    }
    $quotedArguments = @($effectiveArguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DotNet
    $startInfo.Arguments = $quotedArguments
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    # 固定工作目录：调用方可能是 PowerShell 的 Set-Location（它不改变进程当前目录），
    # 不指定时子进程会继承宿主的启动目录，导致相对路径指向别的仓库。
    $startInfo.WorkingDirectory = $script:RepoRoot
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $null = $process.Start()
    $exitCode = $null
    $timedOut = $false
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            & taskkill.exe /PID $process.Id /T /F | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Could not terminate timed-out test process $($process.Id)." }
        }
        $null = $process.WaitForExit()
        if (-not $timedOut) { $exitCode = $process.ExitCode }
    }
    finally { $process.Dispose() }

    if ($timedOut) {
        $message = "dotnet test exceeded the $TimeoutSeconds-second timeout."
        Write-Host "::error::$message"
        throw $message
    }
    if ($exitCode -ne 0) {
        $summary = Get-TrxFailureSummary $resultsDirectory
        if (-not [string]::IsNullOrWhiteSpace($summary)) {
            Write-Host "::error::$summary"
        } else {
            Write-Host "::error::dotnet test exited with code $exitCode before writing a TRX report."
        }
        throw "dotnet test failed with exit code $exitCode"
    }
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
