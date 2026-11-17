#Requires -Version 5.1
<#
    纯函数工具：供 check-ui-performance.ps1 与 scripts/tests/Test-UiPerformanceParsing.ps1 共用。
    这里只做解析与统计，不启动进程，便于在测试里用固定日志文本验证。
#>

function Get-GestureClipExePath {
    param(
        [string]$Root,
        [string]$Explicit
    )

    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (-not (Test-Path -LiteralPath $Explicit)) {
            throw "Executable not found: $Explicit"
        }

        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $candidates = @(
        (Join-Path $Root 'artifacts\release\GestureClip.exe'),
        (Join-Path $Root 'src\GestureClip.App\bin\Release\net8.0-windows10.0.19041.0\GestureClip.exe'),
        (Join-Path $Root 'src\GestureClip.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\GestureClip.exe'),
        (Join-Path $Root 'src\GestureClip.App\bin\Debug\net8.0-windows10.0.19041.0\GestureClip.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'GestureClip.exe not found. Build the project or pass -ExePath.'
}

function Get-MedianValue {
    param([double[]]$Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return $null
    }

    $sorted = $Values | Sort-Object
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return [double]$sorted[$middle]
    }

    return [double](($sorted[$middle - 1] + $sorted[$middle]) / 2)
}

<#
    从一段日志里取出三类样本（毫秒）：
      startup   starting → started 的时间差
      construct 最后一次 "SettingsWindow constructed in N ms"
      show      最后一次 "Settings window shown in N ms"
    日志行格式：2026-10-26T17:40:00.1234567+08:00 [Information] Category: message
#>
function Read-UiTimingSamples {
    param([string]$Text)

    $result = [pscustomobject]@{
        StartupMs   = $null
        ConstructMs = $null
        ShowMs      = $null
        PageMs      = $null
    }

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $result
    }

    # 版本号里带空格（"0.6.46 Beta"），所以不能用 \S+ 匹配。
    $startMatch = [regex]::Match($Text, '(?m)^(\S+) \[Information\].*GestureClip v.+ starting\.')
    $endMatch = [regex]::Match($Text, '(?m)^(\S+) \[Information\].*GestureClip v.+ started\.')
    if ($startMatch.Success -and $endMatch.Success) {
        $result.StartupMs = ([DateTimeOffset]::Parse($endMatch.Groups[1].Value) -
            [DateTimeOffset]::Parse($startMatch.Groups[1].Value)).TotalMilliseconds
    }

    $construct = [regex]::Matches($Text, 'SettingsWindow constructed in (\d+) ms')
    if ($construct.Count -gt 0) {
        $result.ConstructMs = [double]$construct[$construct.Count - 1].Groups[1].Value
    }

    # AppLifecycleService 输出的是 "Settings window ready in N ms (cold|warm)"；
    # 旧版本曾叫 "shown in"，两种都认，避免日志文案一变护栏就失效。
    $show = [regex]::Matches($Text, 'Settings window (?:shown|ready) in (\d+) ms')
    if ($show.Count -gt 0) {
        $result.ShowMs = [double]$show[$show.Count - 1].Groups[1].Value
    }

    # 切页开销：SettingsWindow.Navigation 每页输出 "Page 'x' ready in N ms"，取最慢的一页。
    $pages = [regex]::Matches($Text, "Page '[^']+' ready in (\d+) ms")
    if ($pages.Count -gt 0) {
        $slowest = 0d
        foreach ($match in $pages) {
            $value = [double]$match.Groups[1].Value
            if ($value -gt $slowest) { $slowest = $value }
        }

        $result.PageMs = $slowest
    }

    return $result
}

function Get-UiBudgetFailure {
    param(
        [string]$Name,
        $Value,
        [int]$Budget
    )

    if ($null -eq $Value) {
        return "$Name : no sample collected"
    }

    if ($Value -gt $Budget) {
        return "$Name : $([int]$Value) ms > $Budget ms"
    }

    return $null
}
