param(
    [ValidateSet('win-x64','win-arm64','linux-x64','osx-x64','osx-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$project = Join-Path $PSScriptRoot '..\RouteAnalyzer.App\RouteAnalyzer.App.csproj'
$outDir = Join-Path $PSScriptRoot "..\artifacts\app\$Runtime"

Write-Host "Publishing RouteAnalyzer.App for $Runtime ..."

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:DebugSymbols=false `
    /p:DebugType=None `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

Get-ChildItem -LiteralPath $outDir -Filter '*.pdb' -ErrorAction SilentlyContinue |
    Remove-Item -Force
Remove-Item -LiteralPath (Join-Path $outDir 'appsettings.Development.json') -Force -ErrorAction SilentlyContinue

if ($Runtime.StartsWith('win-'))
{
    $launcherPath = Join-Path $outDir 'Start-RouteAnalyzer.cmd'
    @'
@echo off
cd /d "%~dp0"
start "Route Analyzer" "%~dp0RouteAnalyzer.App.exe"
'@ | Set-Content -LiteralPath $launcherPath -Encoding ASCII
}

$readmePath = Join-Path $outDir 'README-FIRST.txt'
@'
Route Analyzer
==============

Windows:
1. Double-click Start-RouteAnalyzer.cmd or RouteAnalyzer.App.exe.
2. The browser opens automatically.
3. Enter a target and run the diagnostic.
4. Full reports are saved under reports/app next to the app package.

Advanced:
- Use --no-open to start without opening a browser.
- Use --urls http://127.0.0.1:5015 to force a fixed URL.
'@ | Set-Content -LiteralPath $readmePath -Encoding UTF8

Write-Host "App published to $outDir"
