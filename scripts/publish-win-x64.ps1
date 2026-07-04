$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "GestureClip.sln"
$project = Join-Path $repoRoot "src\GestureClip.App\GestureClip.App.csproj"
$output = Join-Path $repoRoot "artifacts\release\GestureClip"
$staging = Join-Path $repoRoot "artifacts\release\GestureClip.staging"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = $projectXml.Project.PropertyGroup.Version
$zipPath = Join-Path $repoRoot "artifacts\release\GestureClip-v$version-win-x64.zip"
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

Copy-Item (Join-Path $repoRoot "README.md") (Join-Path $output "README.md") -Force
Remove-Item (Join-Path $output "*.pdb") -Force -ErrorAction SilentlyContinue

Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -Force

$publishedExe = Join-Path $output "GestureClip.exe"
if (Test-Path -LiteralPath $publishedExe) {
    Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
    Copy-Item -LiteralPath $publishedExe -Destination $latestExe -Force
}

Write-Host "Release package created:" $output
Write-Host "Release zip created:" $zipPath
Write-Host "Root executable updated:" $rootExe
