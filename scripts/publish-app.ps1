param(
    [ValidateSet('win-x64','win-arm64','linux-x64','osx-x64','osx-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\RouteAnalyzer.App\RouteAnalyzer.App.csproj'
$stagingDir = Join-Path $PSScriptRoot "..\artifacts\publish-staging\app\$Runtime"
$outDir = Join-Path $PSScriptRoot "..\artifacts\app\$Runtime"
$isWindowsRuntime = $Runtime.StartsWith('win-')
$publishedName = if ($isWindowsRuntime) { 'RouteAnalyzer.App.exe' } else { 'RouteAnalyzer.App' }
$finalName = if ($isWindowsRuntime) { 'RouteAnalyzer.exe' } else { 'RouteAnalyzer' }

Write-Host "Publishing RouteAnalyzer.App for $Runtime ..."

if (Test-Path -LiteralPath $stagingDir)
{
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

if (Test-Path -LiteralPath $outDir)
{
    Remove-Item -LiteralPath $outDir -Recurse -Force
}

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:DebugSymbols=false `
    /p:DebugType=None `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $stagingDir

if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Copy-Item -LiteralPath (Join-Path $stagingDir $publishedName) -Destination (Join-Path $outDir $finalName) -Force

Write-Host "Single-file app published to $(Join-Path $outDir $finalName)"
