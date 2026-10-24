#Requires -Version 5.1
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "GestureClip.sln"
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$output = Join-Path $releaseRoot "GestureClip"
$staging = Join-Path $releaseRoot "GestureClip.staging"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
$packageVersion = $version.ToLowerInvariant()
$fullZipPath = Join-Path $releaseRoot "GestureClip-v$packageVersion-win-x64.zip"
$hashPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$dotnet = Get-DotNetSdk
Assert-BuildPath $staging $repoRoot
Assert-BuildPath $output $repoRoot
Write-Host "Using .NET SDK host: $dotnet"
Assert-PublishOutputAvailable $output

Invoke-Checked $dotnet @("restore", $solution) "dotnet restore"
Invoke-DotNetTests $dotnet @("test", $solution, "--no-restore")

Remove-BuildOutput $staging
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Invoke-Checked $dotnet @(
    "publish", $project, "-c", "Release", "-r", "win-x64",
    "--self-contained", "true", "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true", "-o", $staging
) "dotnet publish"

$signScript = Join-Path $PSScriptRoot "sign-release.ps1"
Invoke-Checked "powershell.exe" @(
    "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $signScript, "-Path", $staging
) "sign-release.ps1"

Remove-BuildOutput $output
Move-Item -LiteralPath $staging -Destination $output
foreach ($document in @("README.md", "UPDATE.md", "HELP.md", "BETA_TEST.md", "KNOWN_ISSUES.md", "CHANGELOG.md", "PRIVACY.md", "ROADMAP.md", "LICENSE")) {
    $source = Join-Path $repoRoot $document
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $output $document) -Force
    }
}
Copy-Item -LiteralPath (Join-Path $repoRoot "docs") -Destination (Join-Path $output "docs") -Recurse -Force

# Piped and wildcard removals bind poorly with deletion guards; loop over explicit paths.
foreach ($pdb in (Get-ChildItem -LiteralPath $output -Filter "*.pdb" -File)) {
    Remove-BuildOutput $pdb.FullName
}
foreach ($name in @("gestureclip.db", "gestureclip.db-shm", "gestureclip.db-wal", "logs")) {
    Remove-BuildOutput (Join-Path $output $name)
}
foreach ($zip in (Get-ChildItem -LiteralPath $releaseRoot -Filter "GestureClip-v*-win-x64.zip" -File)) {
    Remove-BuildOutput $zip.FullName
}
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $fullZipPath -Force
$hashValue = (Get-FileHash -LiteralPath $fullZipPath -Algorithm SHA256).Hash
"$hashValue  $([IO.Path]::GetFileName($fullZipPath))" | Set-Content -LiteralPath $hashPath -Encoding UTF8

Write-Host "Release package created: $output"
Write-Host "Release zip created: $fullZipPath"
Write-Host "SHA256 file created: $hashPath"
