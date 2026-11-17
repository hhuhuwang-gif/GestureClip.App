#Requires -Version 5.1
<#
.SYNOPSIS
    UI 性能护栏：启动耗时与设置窗构建耗时超出预算时返回非零退出码。

.DESCRIPTION
    以 --trace-ui --smoke-exit-after-startup 启动应用，从日志里读取
    「starting → started」与「SettingsWindow constructed in N ms」，
    多次采样取中位数后与预算比较。

    默认预算留了余量（本机基线：启动 ~1.1s、建窗 ~0.55s），
    在较慢的机器或 CI 上仍能通过；但如果有人把分页改回全量构建、
    或在启动路径上加同步 IO，就会立刻超预算。

.PARAMETER Runs
    采样次数，取中位数，默认 3。

.PARAMETER ExePath
    指定 GestureClip.exe；默认按 Release 产物 → Debug 产物顺序查找。
#>
[CmdletBinding()]
param(
    [int]$MaxStartupMs = 3000,
    [int]$MaxSettingsConstructMs = 1200,
    [int]$MaxSettingsShowMs = 1800,
    [int]$MaxPageReadyMs = 1500,
    [int]$Runs = 3,
    [string]$ExePath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'ui-performance-common.ps1')

$root = Split-Path -Parent $PSScriptRoot
$launcher = Get-GestureClipExePath -Root $root -Explicit $ExePath
Write-Host "Executable : $launcher"

$running = Get-Process -Name 'GestureClip' -ErrorAction SilentlyContinue
if ($null -ne $running) {
    throw "GestureClip is already running (pid $($running[0].Id)). Close it first: single-instance mode would make the sample empty."
}

$logPath = Join-Path $env:LOCALAPPDATA ("GestureClip\logs\{0}.log" -f (Get-Date -Format 'yyyy-MM-dd'))
Write-Host "Log        : $logPath"

$startupSamples = New-Object System.Collections.Generic.List[double]
$constructSamples = New-Object System.Collections.Generic.List[double]
$showSamples = New-Object System.Collections.Generic.List[double]
$pageSamples = New-Object System.Collections.Generic.List[double]

for ($index = 1; $index -le $Runs; $index++) {
    $offset = 0
    if (Test-Path -LiteralPath $logPath) {
        $offset = (Get-Item -LiteralPath $logPath).Length
    }

    $process = [System.Diagnostics.Process]::Start([System.Diagnostics.ProcessStartInfo]@{
            FileName         = $launcher
            Arguments        = '--trace-ui --smoke-exit-after-startup'
            UseShellExecute  = $true
            WorkingDirectory = (Split-Path -Parent $launcher)
        })

    if (-not $process.WaitForExit(90000)) {
        try { $process.Kill() } catch { }
        throw "Run $index timed out after 90s."
    }

    Start-Sleep -Milliseconds 400

    if (-not (Test-Path -LiteralPath $logPath)) {
        throw "Log file was not produced: $logPath"
    }

    $stream = [System.IO.File]::Open($logPath, 'Open', 'Read', 'ReadWrite')
    try {
        $null = $stream.Seek($offset, 'Begin')
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
    }
    finally {
        $stream.Dispose()
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Run $index produced no log output. Another instance may hold the single-instance lock."
    }

    $sample = Read-UiTimingSamples -Text $text
    if ($null -ne $sample.StartupMs) {
        $startupSamples.Add($sample.StartupMs)
        Write-Host ("  run {0}: startup   {1:N0} ms" -f $index, $sample.StartupMs)
    }
    else {
        Write-Warning "Run ${index}: startup markers not found in the log."
    }

    if ($null -ne $sample.ConstructMs) {
        $constructSamples.Add($sample.ConstructMs)
        Write-Host ("  run {0}: construct {1:N0} ms" -f $index, $sample.ConstructMs)
    }

    if ($null -ne $sample.ShowMs) {
        $showSamples.Add($sample.ShowMs)
        Write-Host ("  run {0}: show      {1:N0} ms" -f $index, $sample.ShowMs)
    }

    if ($null -ne $sample.PageMs) {
        $pageSamples.Add($sample.PageMs)
        Write-Host ("  run {0}: page      {1:N0} ms" -f $index, $sample.PageMs)
    }
}

$checks = @(
    [pscustomobject]@{ Name = 'startup'; Samples = $startupSamples; Budget = $MaxStartupMs; Required = $true },
    [pscustomobject]@{ Name = 'construct'; Samples = $constructSamples; Budget = $MaxSettingsConstructMs; Required = $true },
    [pscustomobject]@{ Name = 'show'; Samples = $showSamples; Budget = $MaxSettingsShowMs; Required = $false },
    [pscustomobject]@{ Name = 'page'; Samples = $pageSamples; Budget = $MaxPageReadyMs; Required = $false }
)

$failures = New-Object System.Collections.Generic.List[string]

Write-Host ''
Write-Host '--- medians ---------------------------------------------------'
foreach ($check in $checks) {
    $median = Get-MedianValue -Values $check.Samples.ToArray()
    if ($null -eq $median) {
        Write-Host ("{0,-10} n/a (budget {1} ms)" -f $check.Name, $check.Budget)
    }
    else {
        $status = if ($median -le $check.Budget) { 'ok' } else { 'OVER' }
        Write-Host ("{0,-10} {1,7:N0} ms (budget {2} ms) {3}" -f $check.Name, $median, $check.Budget, $status)
    }

    # show/page 依赖窗口真的完成渲染；--smoke-exit-after-startup 下可能采不到，
    # 此时只提示，不算失败，避免 CI 上因为时序差异误报。
    if ($null -eq $median -and -not $check.Required) {
        continue
    }

    $failure = Get-UiBudgetFailure -Name $check.Name -Value $median -Budget $check.Budget
    if ($null -ne $failure) {
        $failures.Add($failure)
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    foreach ($failure in $failures) {
        Write-Host "FAIL $failure" -ForegroundColor Red
    }

    exit 1
}

Write-Host ''
Write-Host 'OK: all UI performance budgets met.'
exit 0
