param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "src-dotnet\HackBGRTAnimated.WinUI\HackBGRTAnimated.WinUI.csproj"
$targetExe = Join-Path $repoRoot "src-dotnet\HackBGRTAnimated.WinUI\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\HackBGRTAnimated.WinUI.exe"

if (-not (Test-Path $projectPath)) {
  throw "WinUI project not found: $projectPath"
}

Get-Process HackBGRTAnimated.WinUI -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Restoring WinUI project..."
dotnet restore $projectPath -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host "Building WinUI project ($Configuration|x64)..."
dotnet build $projectPath -c $Configuration -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

if (Test-Path $targetExe) {
  Write-Host "Build output: $targetExe"
} else {
  Write-Warning "Build succeeded but expected exe path was not found: $targetExe"
}
