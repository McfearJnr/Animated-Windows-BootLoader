param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $repoRoot "build-winui.ps1"
$winuiProjectDir = Join-Path $repoRoot "src-dotnet\HackBGRTAnimated.WinUI"

if (-not $NoBuild) {
  if (-not (Test-Path $buildScript)) {
    throw "Build script not found: $buildScript"
  }

  & $buildScript -Configuration $Configuration
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}

$candidates = @(
  (Join-Path $winuiProjectDir "bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\HackBGRTAnimated.WinUI.exe"),
  (Join-Path $winuiProjectDir "bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\HackBGRTAnimated.WinUI.exe")
)

$exePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $exePath) {
  $exePath = Get-ChildItem -Path (Join-Path $winuiProjectDir "bin") -Recurse -Filter "HackBGRTAnimated.WinUI.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*\$Configuration\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -ExpandProperty FullName -First 1
}

if (-not $exePath) {
  throw "Could not locate HackBGRTAnimated.WinUI.exe under $winuiProjectDir\bin"
}

Get-Process HackBGRTAnimated.WinUI -ErrorAction SilentlyContinue | Stop-Process -Force

$launched = Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath) -PassThru
Write-Host "Launched: $exePath"

# Detect the common WinAppSDK bootstrap failure case and emit a clear hint.
Start-Sleep -Seconds 2
$proc = Get-Process -Id $launched.Id -ErrorAction SilentlyContinue
if ($null -eq $proc) {
  Write-Warning "HackBGRTAnimated.WinUI exited immediately after launch."
  Write-Warning "If a startup dialog did not appear, check Windows Application Event Log for crash details."
  Write-Warning "If you see Windows App Runtime bootstrap errors, run from an elevated terminal:"
  Write-Warning "winget install --id Microsoft.WindowsAppRuntime.1.6 --exact --source winget --force"
} elseif ($proc.MainWindowTitle -like "*could not be started*") {
  Write-Warning "HackBGRTAnimated.WinUI started with a bootstrap error dialog."
  Write-Warning "Repair Windows App Runtime 1.6 from an elevated terminal:"
  Write-Warning "winget install --id Microsoft.WindowsAppRuntime.1.6 --exact --source winget --force"
}
