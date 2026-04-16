<#
.SYNOPSIS
    Builds and packs the SDK and Sample projects into NuGet packages.

.DESCRIPTION
    Builds Zakira.Imprint.Sdk and Zakira.Imprint.Sample in Release configuration,
    packs them into .nupkg files under the out/ directory, and optionally pushes
    them to nuget.org.

    The SDK is packed first and copied into the local-packages feed so that
    Zakira.Imprint.Sample (which references the SDK as a NuGet package) can
    resolve the dependency during its own pack.

.PARAMETER Push
    If specified, pushes the generated packages to nuget.org.

.PARAMETER ApiKey
    The NuGet API key for pushing packages. If not provided, falls back to
    the NUGET_API_KEY environment variable.

.EXAMPLE
    .\pack.ps1
    Builds and packs both projects.

.EXAMPLE
    .\pack.ps1 -Push
    Builds, packs, and pushes to nuget.org using NUGET_API_KEY env variable.

.EXAMPLE
    .\pack.ps1 -Push -ApiKey "my-api-key"
    Builds, packs, and pushes to nuget.org using the provided API key.
#>

param(
    [switch]$Push,
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$outDir = Join-Path $root "out"
$localFeed = Join-Path $root "local-packages"

$sdkProject = Join-Path $root "src\Zakira.Imprint.Sdk\Zakira.Imprint.Sdk.csproj"
$sampleProject = Join-Path $root "samples\Zakira.Imprint.Sample\Zakira.Imprint.Sample.csproj"

# Clean output directory
if (Test-Path $outDir) {
    Remove-Item "$outDir\*" -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# --- Pack SDK ---
Write-Host "Packing Zakira.Imprint.Sdk..." -ForegroundColor Cyan
dotnet pack $sdkProject -c Release -o $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pack Zakira.Imprint.Sdk"
    exit 1
}
Write-Host "Packed Zakira.Imprint.Sdk successfully." -ForegroundColor Green

# Copy the SDK package into the local feed so the Sample can resolve it
Write-Host "Updating local-packages feed with the new SDK package..." -ForegroundColor Cyan
Copy-Item (Join-Path $outDir "Zakira.Imprint.Sdk.*.nupkg") -Destination $localFeed -Force

# --- Pack Sample ---
Write-Host "Packing Zakira.Imprint.Sample..." -ForegroundColor Cyan
dotnet pack $sampleProject -c Release -o $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pack Zakira.Imprint.Sample"
    exit 1
}
Write-Host "Packed Zakira.Imprint.Sample successfully." -ForegroundColor Green

# --- Push ---
if ($Push) {
    if (-not $ApiKey) {
        $ApiKey = $env:NUGET_API_KEY
    }

    if (-not $ApiKey) {
        Write-Error "No API key provided. Use -ApiKey or set the NUGET_API_KEY environment variable."
        exit 1
    }

    $packages = Get-ChildItem -Path $outDir -Filter "*.nupkg"
    foreach ($pkg in $packages) {
        Write-Host "Pushing $($pkg.Name)..." -ForegroundColor Cyan
        dotnet nuget push $pkg.FullName --api-key $ApiKey --source https://api.nuget.org/v3/index.json --skip-duplicate
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to push $($pkg.Name)"
            exit 1
        }
        Write-Host "Pushed $($pkg.Name) successfully." -ForegroundColor Green
    }
}

Write-Host "Done." -ForegroundColor Green
