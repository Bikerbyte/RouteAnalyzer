param(
    [ValidateSet('win-x64','win-arm64','linux-x64','osx-x64','osx-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$project = Join-Path $PSScriptRoot '..\RouteAnalyzer.Cli\RouteAnalyzer.Cli.csproj'
$outDir = Join-Path $PSScriptRoot "..\artifacts\cli\$Runtime"

Write-Host "Publishing RouteAnalyzer.Cli for $Runtime ..."

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

Write-Host "CLI published to $outDir"
