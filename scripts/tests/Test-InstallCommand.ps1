#Requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installerPath = Join-Path $PSScriptRoot '..\install\install.ps1'
$parseErrors = $null
$tokens = $null
$installerAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $installerPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "Installer parse failed: $parseErrors" }

$functionNames = @('Get-UninstallCommand', 'Get-UninstallRegistryPath', 'Unregister-UninstallEntry')
foreach ($functionName in $functionNames) {
    $definition = $installerAst.Find({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName
    }, $false)
    if ($null -eq $definition) { throw "Missing installer function: $functionName" }
    . ([scriptblock]::Create($definition.Extent.Text))
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected: [$Expected]; actual: [$Actual]" }
}

function Get-CommandTokens([string]$Command) {
    $commandErrors = $null
    $commandTokens = $null
    $commandAst = [System.Management.Automation.Language.Parser]::ParseInput(
        $Command, [ref]$commandTokens, [ref]$commandErrors)
    if ($commandErrors.Count -gt 0) { throw "Uninstall command parse failed: $commandErrors" }
    foreach ($element in $commandAst.EndBlock.Statements[0].PipelineElements[0].CommandElements) {
        if ($element -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
            $element.Value
        } else {
            $element.Extent.Text
        }
    }
}

function Test-Path([string]$LiteralPath) { return $true }

function Remove-Item([string]$LiteralPath, [switch]$Recurse, [switch]$Force) {
    return $LiteralPath
}

$AppId = 'GestureClip.App'
$customRoot = Join-Path ([IO.Path]::GetTempPath()) 'GestureClip install test'
foreach ($AllUsers in @($false, $true)) {
    $command = Get-UninstallCommand -InstallRoot $customRoot -ForAllUsers:$AllUsers
    $arguments = @(Get-CommandTokens $command)
    $expectedCount = if ($AllUsers) { 10 } else { 9 }
    Assert-Equal $expectedCount $arguments.Count 'Unexpected uninstall argument count.'
    Assert-Equal 'powershell.exe' $arguments[0] 'Unexpected command executable.'
    Assert-Equal '-File' $arguments[4] 'Missing helper flag.'
    Assert-Equal (Join-Path $customRoot 'uninstall.ps1') $arguments[5] 'Helper path was split or changed.'
    Assert-Equal '-Uninstall' $arguments[6] 'Missing uninstall flag.'
    Assert-Equal '-InstallDir' $arguments[7] 'Missing custom install root flag.'
    Assert-Equal $customRoot $arguments[8] 'Custom root was split or changed.'
    if ($AllUsers) { Assert-Equal '-AllUsers' $arguments[9] 'Missing all-users scope.' }

    $removedPaths = @(Unregister-UninstallEntry)
    $expectedHive = if ($AllUsers) { 'HKLM:' } else { 'HKCU:' }
    Assert-Equal 1 $removedPaths.Count 'Unregister must remove exactly one registry key.'
    Assert-Equal "$expectedHive\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId" $removedPaths[0] 'Wrong uninstall registry hive.'
}

Write-Output 'PASS: custom install paths with spaces and both uninstall scopes.'
