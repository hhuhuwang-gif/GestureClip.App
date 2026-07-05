$ErrorActionPreference = "Stop"

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
$updateZipPath = Join-Path $releaseRoot "GestureClip-v$packageVersion-update-win-x64.zip"
$hashPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$rootExe = Join-Path $repoRoot "GestureClip.exe"
$latestExe = Join-Path $repoRoot "GestureClip-latest.exe"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Copy-ReleaseDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $source = Join-Path $repoRoot $Name
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $output $Name) -Force
    }
}

Invoke-Checked { dotnet restore $solution } "dotnet restore"
Invoke-Checked { dotnet test $solution --no-restore } "dotnet test"

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Invoke-Checked {
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $staging
} "dotnet publish"

Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item (Join-Path $staging "*") $output -Recurse -Force
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

Copy-ReleaseDocument "README.md"
Copy-ReleaseDocument "UPDATE.md"
Copy-ReleaseDocument "CHANGELOG.md"
Copy-ReleaseDocument "LICENSE"

Remove-Item (Join-Path $output "*.pdb") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $output "gestureclip.db") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $output "gestureclip.db-shm") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $output "gestureclip.db-wal") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $output "logs") -Recurse -Force -ErrorAction SilentlyContinue

Remove-Item $fullZipPath -Force -ErrorAction SilentlyContinue
Remove-Item $updateZipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $fullZipPath -Force
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $updateZipPath -Force

$publishedExe = Join-Path $output "GestureClip.exe"
if (Test-Path -LiteralPath $publishedExe) {
    Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
    Copy-Item -LiteralPath $publishedExe -Destination $latestExe -Force
}

$hashes = foreach ($path in @($fullZipPath, $updateZipPath)) {
    $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
    "$($hash.Hash)  $([System.IO.Path]::GetFileName($path))"
}
$hashes | Set-Content -LiteralPath $hashPath -Encoding UTF8
Copy-Item -LiteralPath $hashPath -Destination (Join-Path $output "SHA256SUMS.txt") -Force

Write-Host "Release package created:" $output
Write-Host "Full release zip created:" $fullZipPath
Write-Host "Update release zip created:" $updateZipPath
Write-Host "SHA256 file created:" $hashPath
Write-Host "Root executable updated:" $rootExe
