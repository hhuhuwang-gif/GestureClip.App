#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][int]$GestureClipProcessId,
    [ValidateSet('Word', 'Excel', 'Edge', 'Chrome')][string[]]$Applications = @('Word', 'Excel', 'Edge'),
    [ValidateRange(1, 100)][int]$Samples = 5,
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..\artifacts\desktop-response' }
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Run with powershell.exe -STA -File scripts\measure-office.ps1.' }
Add-Type -AssemblyName PresentationCore, UIAutomationClient, UIAutomationTypes
Add-Type -Path (Join-Path $PSScriptRoot 'desktop-checks\DesktopProbeNative.cs')
. (Join-Path $PSScriptRoot 'desktop-checks\ProbeCommon.ps1')
. (Join-Path $PSScriptRoot 'desktop-checks\OfficeTargets.ps1')
. (Join-Path $PSScriptRoot 'desktop-checks\BrowserTargets.ps1')
$gestureProcess = Get-Process -Id $GestureClipProcessId
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')) + '\'
if (!$gestureProcess.Path.StartsWith($workspace, [StringComparison]::OrdinalIgnoreCase)) { throw 'Selected GestureClip process is not a workspace build.' }
if ((Get-ProbeOverlay $GestureClipProcessId) -ne [IntPtr]::Zero) { throw 'Close the existing clipboard overlay before the probe.' }
$originalClipboard = [Windows.Clipboard]::GetDataObject()
$originalPointer = New-Object DesktopProbeNative+Point
if (![DesktopProbeNative]::GetCursorPos([ref]$originalPointer)) { throw 'Cannot save pointer position.' }
$originalForeground = [DesktopProbeNative]::GetForegroundWindow()
$rows = New-Object 'Collections.Generic.List[object]'
$runDirectory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$target = $null
$probeErrors = New-Object 'Collections.Generic.List[Exception]'
try {
    foreach ($applicationName in $Applications) {
        $token = 'GestureClip-probe-' + [Guid]::NewGuid().ToString('N')
        [Windows.Clipboard]::SetText($token)
        $target = if ($applicationName -in @('Word', 'Excel')) { New-OfficeProbeTarget $applicationName } else { New-BrowserProbeTarget $applicationName $runDirectory }
        Start-Sleep -Milliseconds 600
        for ($sample = 0; $sample -lt $Samples; $sample++) {
            $phase = if ($sample -eq 0) { 'first' } else { 'repeat' }
            Invoke-ProbeSample @{ Target=$target; Token=$token; ProcessId=$GestureClipProcessId; Phase=$phase } | ForEach-Object { $rows.Add($_) }
        }
        if ($target.Kind -eq 'Office') { Close-OfficeProbeTarget $target } else { Close-BrowserProbeTarget $target }
        $target = $null
    }
}
catch {
    $probeErrors.Add($_.Exception)
    Write-Warning 'Desktop probe failed. Results contain only completed attempts; unrecorded apps are untested.'
}
finally {
    $cleanupActions = @(
        { if ($null -ne $target) { if ($target.Kind -eq 'Office') { Close-OfficeProbeTarget $target } else { Close-BrowserProbeTarget $target } } },
        { if ($null -eq $originalClipboard) { [Windows.Clipboard]::Clear() } else { [Windows.Clipboard]::SetDataObject($originalClipboard, $true) } },
        { if (![DesktopProbeNative]::SetCursorPos($originalPointer.X, $originalPointer.Y)) { throw 'Could not restore pointer position.' } },
        { if ([DesktopProbeNative]::IsWindowVisible($originalForeground)) { [DesktopProbeNative]::Activate($originalForeground) } },
        { $rows | Export-Csv -LiteralPath (Join-Path $runDirectory 'measurements.csv') -NoTypeInformation -Encoding UTF8 },
        { ConvertTo-Json -InputObject @($rows.ToArray()) -Depth 3 | Set-Content -LiteralPath (Join-Path $runDirectory 'measurements.json') -Encoding UTF8 }
    )
    foreach ($action in $cleanupActions) {
        try { & $action }
        catch { $probeErrors.Add($_.Exception) }
    }
}
if ($probeErrors.Count -gt 0) {
    foreach ($probeError in $probeErrors) { Write-Warning ($probeError.GetType().Name + ': ' + $probeError.Message) }
    throw (New-Object AggregateException('Desktop probe failed or cleanup was incomplete.', $probeErrors))
}
Write-Output "Desktop response results: $runDirectory"
