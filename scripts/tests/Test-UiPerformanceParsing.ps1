#Requires -Version 5.1
<#
    验证 check-ui-performance.ps1 的日志解析与预算判定（不启动应用）。
    日志格式样例：2026-10-26T17:40:00.1234567+08:00 [Information] Category: message
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot '..\ui-performance-common.ps1')

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected: [$Expected]; actual: [$Actual]" }
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$log = @'
2026-10-26T17:40:00.0000000+08:00 [Information] GestureClip.App.App: GestureClip v0.6.46 Beta starting.
2026-10-26T17:40:00.1000000+08:00 [Information] GestureClip.App.AppLifecycleService: SettingsWindow constructed in 540 ms
2026-10-26T17:40:00.5200000+08:00 [Information] GestureClip.App.AppLifecycleService: Settings window shown in 620 ms (construction=cold)
2026-10-26T17:40:01.1300000+08:00 [Information] GestureClip.App.App: GestureClip v0.6.46 Beta started.
'@

$sample = Read-UiTimingSamples -Text $log
Assert-Equal 1130 ([int]$sample.StartupMs) 'Startup span must be the starting→started delta.'
Assert-Equal 540 ([int]$sample.ConstructMs) 'Construct sample must come from the timing line.'
Assert-Equal 620 ([int]$sample.ShowMs) 'Legacy "shown in" lines must still be understood.'

# 当前代码输出的是 "ready in"，并且每页会有一条 "Page 'x' ready in N ms"。
$current = @'
2026-10-27T09:14:28.4219766+08:00 [Information] GestureClip.App.App: GestureClip v0.6.45 Beta+1681d65 starting.
2026-10-27T09:14:28.4219766+08:00 [Information] GestureClip.App.App: UiTiming SettingsWindow XAML load 88 ms
2026-10-27T09:14:28.4239292+08:00 [Information] GestureClip.App.App: UiTiming SettingsWindow constructed in 184 ms
2026-10-27T09:14:28.6730696+08:00 [Information] GestureClip.App.App: UiTiming Settings window ready in 434 ms (cold)
2026-10-27T09:14:29.0000000+08:00 [Information] GestureClip.App.App: UiTiming Page 'home' ready in 377 ms
2026-10-27T09:14:29.4000000+08:00 [Information] GestureClip.App.App: UiTiming Page 'bindings' ready in 690 ms
2026-10-27T09:14:29.6891071+08:00 [Information] GestureClip.App.App: GestureClip v0.6.45 Beta+1681d65 started.
'@

$currentSample = Read-UiTimingSamples -Text $current
Assert-Equal 184 ([int]$currentSample.ConstructMs) 'Construct sample must come from the current wording.'
Assert-Equal 434 ([int]$currentSample.ShowMs) '"ready in" must be understood as the show sample.'
Assert-Equal 690 ([int]$currentSample.PageMs) 'The slowest page is the one worth guarding.'
Assert-Equal 1267 ([int]$currentSample.StartupMs) 'Startup span must tolerate the version suffix.'

# 缺少结束标记时不能凭空算出一个启动耗时。
$incomplete = '2026-10-26T17:40:00.0000000+08:00 [Information] App: GestureClip v0.6.46 Beta starting.'
$incompleteSample = Read-UiTimingSamples -Text $incomplete
Assert-True ($null -eq $incompleteSample.StartupMs) 'An unfinished startup must produce no startup sample.'
Assert-True ($null -eq $incompleteSample.ConstructMs) 'Missing construct timing must stay null.'
Assert-True ($null -eq $incompleteSample.PageMs) 'Missing page timing must stay null.'

# 多次出现时取最后一次：热启动会重建设置窗，需要衡量的正是最新一次。
$repeated = @'
2026-10-26T17:40:00.0000000+08:00 [Information] App: GestureClip v0.6.46 Beta starting.
2026-10-26T17:40:00.9000000+08:00 [Information] Lifecycle: SettingsWindow constructed in 900 ms
2026-10-26T17:40:01.0000000+08:00 [Information] Lifecycle: SettingsWindow constructed in 310 ms
2026-10-26T17:40:01.2000000+08:00 [Information] App: GestureClip v0.6.46 Beta started.
'@

$repeatedSample = Read-UiTimingSamples -Text $repeated
Assert-Equal 310 ([int]$repeatedSample.ConstructMs) 'The last construct line wins.'

# 缺少结束标记时不能凭空算出一个启动耗时。
$incomplete = '2026-10-26T17:40:00.0000000+08:00 [Information] App: GestureClip v0.6.46 Beta starting.'
$incompleteSample = Read-UiTimingSamples -Text $incomplete
Assert-True ($null -eq $incompleteSample.StartupMs) 'An unfinished startup must produce no startup sample.'
Assert-True ($null -eq $incompleteSample.ConstructMs) 'Missing construct timing must stay null.'

$empty = Read-UiTimingSamples -Text ''
Assert-True ($null -eq $empty.StartupMs) 'Empty log must not produce a sample.'

# 中位数：奇数取中间值，偶数取两者均值。
Assert-Equal 20 ([int](Get-MedianValue -Values @(10.0, 20.0, 30.0))) 'Odd median picks the middle value.'
Assert-Equal 25 ([int](Get-MedianValue -Values @(10.0, 30.0, 20.0, 40.0))) 'Even median averages the middle pair.'
Assert-True ($null -eq (Get-MedianValue -Values @())) 'Empty input has no median.'

# 预算判定
Assert-True ($null -eq (Get-UiBudgetFailure -Name 'startup' -Value 900 -Budget 3000)) 'Within budget must not fail.'
Assert-True ($null -ne (Get-UiBudgetFailure -Name 'startup' -Value 3100 -Budget 3000)) 'Over budget must fail.'
Assert-True ($null -ne (Get-UiBudgetFailure -Name 'startup' -Value $null -Budget 3000)) 'Missing sample must fail.'

# 护栏脚本本身必须存在且带阈值参数，避免被静默删掉。
$guardPath = Join-Path $PSScriptRoot '..\check-ui-performance.ps1'
if (-not (Test-Path -LiteralPath $guardPath)) { throw 'check-ui-performance.ps1 is missing.' }

$guardText = Get-Content -LiteralPath $guardPath -Raw
foreach ($parameter in @('MaxStartupMs', 'MaxSettingsConstructMs', 'MaxSettingsShowMs', 'MaxPageReadyMs')) {
    Assert-True ($guardText.Contains($parameter)) "Guard script must expose -$parameter."
}

Write-Host 'OK: UI performance parsing and budgets behave as expected.'
