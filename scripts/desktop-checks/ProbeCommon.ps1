$ProbeTimeoutMilliseconds = 5000
$ProbePollMilliseconds = 5

function Wait-ProbeCondition {
    param([scriptblock]$Condition, [int]$TimeoutMilliseconds = $ProbeTimeoutMilliseconds)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds $ProbePollMilliseconds
    }
    throw "Desktop probe condition timed out after $TimeoutMilliseconds milliseconds."
}

function Get-ProbeOverlay {
    param([int]$GestureClipProcessId)
    $title = -join @([char]0x526A, [char]0x8D34, [char]0x677F, [char]0x5386, [char]0x53F2)
    return [DesktopProbeNative]::FindVisible($GestureClipProcessId, $title)
}

function Reset-ProbeTarget {
    param($Target)
    if ($Target.Kind -eq 'Office') { Reset-OfficeProbeTarget $Target }
    else { Reset-BrowserProbeTarget $Target }
}

function Test-ProbeTargetText {
    param($Target, [string]$Token)
    if ($Target.Kind -eq 'Office') { return Test-OfficeProbeText $Target $Token }
    return Test-BrowserProbeText $Target $Token
}

function Select-ProbeClipboardItem {
    param([int]$GestureClipProcessId, [string]$Token)
    $window = Get-ProbeOverlay $GestureClipProcessId
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($window)
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'SearchBox')
    $search = $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $search) { throw 'Actual overlay search control was not accessible.' }
    $search.SetFocus()
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Token)
    $match = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Token)
    Wait-ProbeCondition { $null -ne $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $match) } | Out-Null
}

function Add-ProbeMeasurement {
    param($Options)
    try {
        Wait-ProbeCondition $Options.Condition | Out-Null
        $Options.Watch.Stop()
        $row = [pscustomobject]@{ app=$Options.App; operation=$Options.Operation; milliseconds=[Math]::Round($Options.Watch.Elapsed.TotalMilliseconds, 2); success=$true }
        return @{ Row=$row; Error=$null }
    }
    catch {
        $Options.Watch.Stop()
        $row = [pscustomobject]@{ app=$Options.App; operation=$Options.Operation; milliseconds=[Math]::Round($Options.Watch.Elapsed.TotalMilliseconds, 2); success=$false }
        return @{ Row=$row; Error=$_.Exception }
    }
}

function Invoke-ProbeSample {
    param($Options)
    $target = $Options.Target
    Reset-ProbeTarget $target
    $watch = [DesktopProbeNative]::Chord(0x11, 0xC0)
    $open = Add-ProbeMeasurement @{ App=$target.Name; Operation=$Options.Phase + '.hotkey_to_overlay_visible'; Watch=$watch; Condition={ (Get-ProbeOverlay $Options.ProcessId) -ne [IntPtr]::Zero } }
    $open.Row
    if ($null -ne $open.Error) { throw $open.Error }
    Select-ProbeClipboardItem $Options.ProcessId $Options.Token
    $watch = [DesktopProbeNative]::Chord(0, 0x0D)
    $paste = Add-ProbeMeasurement @{ App=$target.Name; Operation=$Options.Phase + '.confirm_to_target_text'; Watch=$watch; Condition={ Test-ProbeTargetText $target $Options.Token } }
    $paste.Row
    if ($null -ne $paste.Error) { throw $paste.Error }
    Reset-ProbeTarget $target
    $watch = [DesktopProbeNative]::DownGesture($target.Window)
    $gesture = Add-ProbeMeasurement @{ App=$target.Name; Operation=$Options.Phase + '.gesture_release_to_target_text'; Watch=$watch; Condition={ Test-ProbeTargetText $target $Options.Token } }
    $gesture.Row
    if ($null -ne $gesture.Error) { throw $gesture.Error }
}
