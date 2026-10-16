# Local build: runs the tests, then publishes KeyboardLed.exe into the dist folder.
#
#   .\build.ps1                 framework-dependent single exe (needs .NET 8 Desktop Runtime)
#   .\build.ps1 -SelfContained  standalone exe, runs without any .NET install (~70 MB)
#   .\build.ps1 -SkipTests      skip the unit tests

param(
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $SkipTests) {
    Write-Host '==> Running tests' -ForegroundColor Cyan
    dotnet test KeyboardLed.Tests/KeyboardLed.Tests.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$out = Join-Path $PSScriptRoot 'dist'
if ($SelfContained) { $out = Join-Path $out 'self-contained' }
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

$sc = if ($SelfContained) { 'true' } else { 'false' }
Write-Host "==> Publishing (self-contained: $sc)" -ForegroundColor Cyan
dotnet publish KeyboardLed/KeyboardLed.csproj -c Release -r win-x64 --self-contained $sc -o $out --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out 'KeyboardLed.exe'
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "==> Done: $exe ($sizeMb MB)" -ForegroundColor Green
