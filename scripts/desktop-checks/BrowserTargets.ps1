function Invoke-ProbeCdp {
    param($Connection, $Request)
    $message = @{ id=1; method=$Request.Method; params=$Request.Parameters } | ConvertTo-Json -Depth 8 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($message)
    $segment = New-Object 'System.ArraySegment[byte]' -ArgumentList @(,$bytes)
    $cancellation = New-Object Threading.CancellationTokenSource([TimeSpan]::FromSeconds(5))
    try {
        $Connection.SendAsync($segment, [Net.WebSockets.WebSocketMessageType]::Text, $true, $cancellation.Token).GetAwaiter().GetResult()
        do {
            $response = Receive-ProbeCdpMessage $Connection $cancellation.Token
        } until ($response.PSObject.Properties.Name -contains 'id' -and $response.id -eq 1)
        if ($response.PSObject.Properties.Name -contains 'error') { throw "CDP failed: $($response.error.message)" }
        return $response.result
    }
    finally { $cancellation.Dispose() }
}

function Receive-ProbeCdpMessage {
    param($Connection, $CancellationToken)
    $buffer = New-Object byte[] 65536
    $received = New-Object IO.MemoryStream
    try {
        do {
            $part = $Connection.ReceiveAsync((New-Object 'System.ArraySegment[byte]' -ArgumentList @(,$buffer)), $CancellationToken).GetAwaiter().GetResult()
            if ($part.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) { throw 'Probe browser CDP connection closed.' }
            $received.Write($buffer, 0, $part.Count)
        } until ($part.EndOfMessage)
        return [Text.Encoding]::UTF8.GetString($received.ToArray()) | ConvertFrom-Json
    }
    finally { $received.Dispose() }
}

function Invoke-ProbePage {
    param($Target, [string]$Expression)
    $result = Invoke-ProbeCdp $Target.Connection @{ Method='Runtime.evaluate'; Parameters=@{ expression=$Expression; returnByValue=$true } }
    if ($result.PSObject.Properties.Name -contains 'exceptionDetails') { throw 'Probe page evaluation failed.' }
    return $result.result.value
}

function Find-ProbeBrowserExecutable {
    param([string]$Name)
    $relative = if ($Name -eq 'Edge') { 'Microsoft\Edge\Application\msedge.exe' } else { 'Google\Chrome\Application\chrome.exe' }
    $candidates = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)
    foreach ($directory in $candidates) {
        $candidate = Join-Path $directory $relative
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "$Name executable was not found."
}

function New-BrowserProbeTarget {
    param([string]$Name, [string]$AssetsDirectory)
    $executable = Find-ProbeBrowserExecutable $Name
    $identity = [Guid]::NewGuid().ToString('N')
    $directory = Join-Path $AssetsDirectory $identity
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $page = Join-Path $directory 'probe.html'
    $title = "GestureClip Desktop Probe $identity"
    Set-Content -LiteralPath $page -Encoding UTF8 -Value "<!doctype html><title>$title</title><h1>GestureClip desktop response probe</h1><p>Local test page. No messages are sent.</p><textarea id='office-probe' autofocus style='width:90vw;height:65vh;font-size:24px'></textarea>"
    $profile = Join-Path $directory 'profile'
    $arguments = @('--new-window', '--no-first-run', '--no-default-browser-check', '--disable-sync', '--remote-debugging-port=0', "--user-data-dir=`"$profile`"", "`"$([Uri]::new($page).AbsoluteUri)`"")
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Normal -PassThru
    $target = [pscustomobject]@{ Name=$Name; Kind='Browser'; Process=$process; Directory=$directory; Connection=$null; Window=[IntPtr]::Zero }
    try { return Connect-BrowserProbeTarget $target @{ Profile=$profile; Title=$title; Page=$page } }
    catch {
        if (!$process.HasExited) { $process.CloseMainWindow() | Out-Null }
        throw
    }
}

function Connect-BrowserProbeTarget {
    param($Target, $Options)
    $portFile = Join-Path $Options.Profile 'DevToolsActivePort'
    Wait-ProbeCondition { Test-Path -LiteralPath $portFile } -TimeoutMilliseconds 15000 | Out-Null
    $port = [int]([IO.File]::ReadAllLines($portFile)[0])
    $tabs = Invoke-RestMethod "http://127.0.0.1:$port/json/list"
    $pages = @($tabs | Where-Object { $_.type -eq 'page' -and $_.url -eq [Uri]::new($Options.Page).AbsoluteUri })
    if ($pages.Count -ne 1) { throw 'Dedicated browser did not expose exactly one probe page.' }
    $connection = New-Object Net.WebSockets.ClientWebSocket
    $connection.ConnectAsync([Uri]$pages[0].webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Wait-ProbeCondition { [DesktopProbeNative]::FindVisible(0, $Options.Title) -ne [IntPtr]::Zero } | Out-Null
    return [pscustomobject]@{ Name=$Target.Name; Kind=$Target.Kind; Process=$Target.Process; Directory=$Target.Directory; Connection=$connection; Window=[DesktopProbeNative]::FindVisible(0, $Options.Title) }
}

function Reset-BrowserProbeTarget {
    param($Target)
    Invoke-ProbePage $Target "document.getElementById('office-probe').value='';document.getElementById('office-probe').focus();true" | Out-Null
    [DesktopProbeNative]::Activate($Target.Window)
}

function Test-BrowserProbeText {
    param($Target, [string]$Token)
    $value = Invoke-ProbePage $Target "document.getElementById('office-probe').value"
    return ([string]$value).Contains($Token)
}

function Close-BrowserProbeTarget {
    param($Target)
    $process = Get-Process -Id ([DesktopProbeNative]::ProcessId($Target.Window)) -ErrorAction Stop
    if (!$process.CloseMainWindow()) { throw 'Dedicated probe browser refused normal window close.' }
    if (!$process.WaitForExit(5000)) { throw 'Dedicated probe browser did not close in 5 seconds; it was not terminated.' }
    $Target.Connection.Dispose()
}
