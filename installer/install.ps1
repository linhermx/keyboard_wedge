$ErrorActionPreference = "Stop"

$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = Join-Path $env:LOCALAPPDATA "Programs\LINHER Keyboard Wedge"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\LINHER Keyboard Wedge"
$shortcutPath = Join-Path $startMenuDir "LINHER Keyboard Wedge.lnk"

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

Copy-Item -LiteralPath (Join-Path $sourceDir "LinherKeyboardWedgeLauncher.exe") -Destination $installDir -Force

if (Test-Path -LiteralPath (Join-Path $sourceDir "bundled_assets")) {
    $bundledAssetsDir = Join-Path $installDir "bundled_assets"
    New-Item -ItemType Directory -Force -Path $bundledAssetsDir | Out-Null
    Copy-Item -Path (Join-Path $sourceDir "bundled_assets\*") -Destination $bundledAssetsDir -Recurse -Force
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir "LinherKeyboardWedgeLauncher.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = Join-Path $installDir "LinherKeyboardWedgeLauncher.exe"
$shortcut.Save()

Start-Process -FilePath (Join-Path $installDir "LinherKeyboardWedgeLauncher.exe")
