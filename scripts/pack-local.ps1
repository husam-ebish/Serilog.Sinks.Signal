[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'src\Serilog.Sinks.Signal\Serilog.Sinks.Signal.csproj'

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $versionPrefix = $project.Project.PropertyGroup.VersionPrefix | Select-Object -First 1
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
    $Version = "$versionPrefix-local.$timestamp"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\local-feed'
}
elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    & dotnet restore --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

    & dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    & dotnet test --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

    & dotnet pack $projectPath `
        --configuration Release `
        --no-build `
        --output $OutputDirectory `
        -p:PackageVersion=$Version
    if ($LASTEXITCODE -ne 0) { throw 'Package creation failed.' }
}
finally {
    Pop-Location
}

$packagePath = Join-Path $OutputDirectory "Serilog.Sinks.Signal.$Version.nupkg"
if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Expected package was not created: $packagePath"
}

Write-Output ''
Write-Output "Local package version: $Version"
Write-Output "Local package: $packagePath"
Write-Output ''
Write-Output 'Install it into a test project with:'
Write-Output "dotnet add <PROJECT.csproj> package Serilog.Sinks.Signal --version $Version --no-restore"
if ($OutputDirectory -eq (Join-Path $repositoryRoot 'artifacts\local-feed')) {
    Write-Output "dotnet restore <PROJECT.csproj> --configfile `"$(Join-Path $repositoryRoot 'NuGet.Local.Config')`""
}
else {
    Write-Output "dotnet restore <PROJECT.csproj> --source `"$OutputDirectory`""
    Write-Output 'For an uncached dependency restore, add both this directory and NuGet.org to a temporary NuGet.Config.'
}
