$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnetLocal = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $dotnetLocal) { $dotnetLocal } else { "dotnet" }
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet finalizo con codigo ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

$appProject = Join-Path $root "LinherKeyboardWedge.App\LinherKeyboardWedge.App.csproj"
$launcherProject = Join-Path $root "LinherKeyboardWedge.Launcher\LinherKeyboardWedge.Launcher.csproj"
$setupProject = Join-Path $root "LinherKeyboardWedge.Setup\LinherKeyboardWedge.Setup.csproj"

$appPublishDir = Join-Path $root "dist\LinherKeyboardWedge"
$appZipPath = Join-Path $root "dist\linher_keyboard_wedge_windows.zip"
$launcherPublishDir = Join-Path $root "dist\LinherKeyboardWedgeLauncher"
$launcherPortableZipPath = Join-Path $root "dist\linher_keyboard_wedge_launcher_portable.zip"
$launcherBuildDir = Join-Path $root "dist\launcher-build"
$setupPublishDir = Join-Path $root "dist\setup-build"
$setupPayloadDir = Join-Path $root "LinherKeyboardWedge.Setup\Payload"
$setupPath = Join-Path $root "dist\linher_keyboard_wedge_setup.exe"
$legacySetupPath = Join-Path $root "dist\LinherKeyboardWedgeSetup.exe"
$legacyWedgerAppPublishDir = Join-Path $root "dist\LinherKeyboardWedger"
$legacyWedgerLauncherPublishDir = Join-Path $root "dist\LinherKeyboardWedgerLauncher"
$legacyWedgerAppZipPath = Join-Path $root "dist\linher_keyboard_wedger_windows.zip"
$legacyWedgerLauncherPortableZipPath = Join-Path $root "dist\linher_keyboard_wedger_launcher_portable.zip"
$legacyWedgerSetupPath = Join-Path $root "dist\linher_keyboard_wedger_setup.exe"

function New-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationZip
    )

    if (Test-Path -LiteralPath $DestinationZip) {
        Remove-Item -LiteralPath $DestinationZip -Force
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            [System.IO.Compression.ZipFile]::CreateFromDirectory($SourceDirectory, $DestinationZip)
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds (400 * $attempt)
        }
    }

    throw $lastError
}

[xml]$projectXml = Get-Content -Path $appProject
$version = $projectXml.Project.PropertyGroup.Version
$tag = "v$version"

Remove-Item -LiteralPath $appPublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $launcherPublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $launcherBuildDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $setupPublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $appZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $launcherPortableZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $setupPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacySetupPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyWedgerAppPublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyWedgerLauncherPublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyWedgerAppZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyWedgerLauncherPortableZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $legacyWedgerSetupPath -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $appPublishDir, $launcherPublishDir, $launcherBuildDir, $setupPublishDir, $setupPayloadDir | Out-Null

Get-ChildItem -Path $setupPayloadDir -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne ".gitkeep" } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Invoke-Dotnet @(
    "publish", $appProject,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "/p:PublishSingleFile=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $appPublishDir
)

Get-ChildItem -Path $appPublishDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath (Join-Path $root "config.example.json") -Destination $appPublishDir -Force
New-ZipFromDirectory -SourceDirectory $appPublishDir -DestinationZip $appZipPath

Invoke-Dotnet @(
    "publish", $launcherProject,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "/p:PublishSingleFile=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $launcherBuildDir
)

Get-ChildItem -Path $launcherBuildDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $launcherBuildDir "*") -Destination $launcherPublishDir -Recurse -Force

$bundledAssetsDir = Join-Path $launcherPublishDir "bundled_assets"
New-Item -ItemType Directory -Force -Path $bundledAssetsDir | Out-Null
Copy-Item -LiteralPath $appZipPath -Destination (Join-Path $bundledAssetsDir "linher_keyboard_wedge_windows.zip") -Force

$metadata = @{
    repo = "linhermx/keyboard_wedge"
    tag = $tag
    asset_name = "linher_keyboard_wedge_windows.zip"
} | ConvertTo-Json -Depth 3
[System.IO.File]::WriteAllText(
    (Join-Path $bundledAssetsDir "linher_keyboard_wedge_release.json"),
    $metadata,
    (New-Object System.Text.UTF8Encoding($false)))

New-ZipFromDirectory -SourceDirectory $launcherPublishDir -DestinationZip $launcherPortableZipPath

Copy-Item -Path (Join-Path $launcherPublishDir "*") -Destination $setupPayloadDir -Recurse -Force

Invoke-Dotnet @(
    "publish", $setupProject,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "/p:PublishSingleFile=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $setupPublishDir
)

Copy-Item -LiteralPath (Join-Path $setupPublishDir "LinherKeyboardWedgeSetup.exe") -Destination $setupPath -Force

Get-ChildItem -Path $setupPayloadDir -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne ".gitkeep" } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Remove-Item -LiteralPath $launcherBuildDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $setupPublishDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Aplicacion: $appPublishDir"
Write-Host "ZIP actualizacion: $appZipPath"
Write-Host "Launcher portable: $launcherPortableZipPath"
Write-Host "Instalador: $setupPath"
