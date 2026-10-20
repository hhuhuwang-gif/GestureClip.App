function New-OfficeProbeTarget {
    param([string]$Name)
    $processName = if ($Name -eq 'Word') { 'WINWORD' } else { 'EXCEL' }
    $existing = @(Get-Process -Name $processName -ErrorAction SilentlyContinue | ForEach-Object Id)
    $application = New-Object -ComObject "$Name.Application"
    $document = $null
    $ownsProcess = $false
    try {
        $document = if ($Name -eq 'Word') { $application.Documents.Add() } else { $application.Workbooks.Add() }
        $window = if ($Name -eq 'Word') { [IntPtr]$document.ActiveWindow.Hwnd } else { [IntPtr]$application.Hwnd }
        $window = [DesktopProbeNative]::GetAncestor($window, 2)
        $processId = [DesktopProbeNative]::ProcessId($window)
        if ($processId -eq 0 -or $existing -contains $processId) {
            throw 'Office COM reused an existing user instance; probe refuses to operate it.'
        }
        $ownsProcess = $true
        $application.Visible = $true
        return [pscustomobject]@{ Name=$Name; App=$application; Document=$document; Window=$window; Kind='Office' }
    }
    catch {
        if ($null -ne $document) {
            if ($Name -eq 'Word') { $discard = 0; $document.Close([ref]$discard) } else { $document.Close($false) }
        }
        if ($ownsProcess) { $application.Quit() }
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($application) | Out-Null
        throw
    }
}

function Reset-OfficeProbeTarget {
    param($Target)
    if ($Target.Name -eq 'Word') {
        $Target.Document.Content.Text = ''
        $Target.Document.Activate()
        $Target.App.Selection.HomeKey(6) | Out-Null
    }
    else {
        $Target.Document.Worksheets.Item(1).Range('A1').Clear() | Out-Null
        $Target.Document.Activate()
        $Target.Document.Worksheets.Item(1).Range('A1').Select() | Out-Null
    }
    [DesktopProbeNative]::Activate($Target.Window)
}

function Test-OfficeProbeText {
    param($Target, [string]$Token)
    if ($Target.Name -eq 'Word') { return $Target.Document.Content.Text.Contains($Token) }
    return ([string]$Target.Document.Worksheets.Item(1).Range('A1').Value2).Contains($Token)
}

function Close-OfficeProbeTarget {
    param($Target)
    try {
        if ($Target.Name -eq 'Word') { $discard = 0; $Target.Document.Close([ref]$discard) }
        else { $Target.Document.Close($false) }
    }
    finally {
        if ($Target.Name -eq 'Word') { $discard = 0; $Target.App.Quit([ref]$discard) } else { $Target.App.Quit() }
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($Target.Document) | Out-Null
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($Target.App) | Out-Null
    }
}
