$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\LINHER Keyboard Wedge"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\LINHER Keyboard Wedge"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

if (Test-Path $runKey) {
    Remove-ItemProperty -Path $runKey -Name "LINHERKeyboardWedge" -ErrorAction SilentlyContinue
}

Remove-Item -LiteralPath $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "LINHER Keyboard Wedge se desinstaló. La configuración y los logs de %LOCALAPPDATA%\LINHER\KeyboardWedge se conservaron."
