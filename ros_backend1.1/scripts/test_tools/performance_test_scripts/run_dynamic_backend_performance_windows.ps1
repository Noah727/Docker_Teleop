param(
    [double]$Duration = 60,
    [double]$Interval = 1,
    [double]$Warmup = 8,
    [string]$FakeArm = "both",
    [string]$FakePattern = "line_y",
    [double]$FakePeriod = 8,
    [double]$FakeAmplitudeX = 0.0,
    [double]$FakeAmplitudeY = 0.55,
    [double]$FakeAmplitudeZ = 0.0,
    [string]$Distro = ""
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
    throw "wsl.exe was not found. Install WSL2 Ubuntu before running this script."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendPath = Resolve-Path (Join-Path $scriptDir "..\..\..")

$wslArgs = @()
if ($Distro -ne "") {
    $wslArgs += @("-d", $Distro)
}

$wslBackendPath = (& wsl.exe @wslArgs wslpath -a "$backendPath").Trim()
if ($LASTEXITCODE -ne 0 -or $wslBackendPath -eq "") {
    throw "Could not convert backend path to a WSL path: $backendPath"
}

$bashCommand = @"
set -euo pipefail
cd '$wslBackendPath'
python3 scripts/test_tools/eval_scripts/13_dynamic_novnc_headed_performance_test.py \
  --duration '$Duration' \
  --interval '$Interval' \
  --warmup '$Warmup' \
  --fake-arm '$FakeArm' \
  --fake-pattern '$FakePattern' \
  --fake-period '$FakePeriod' \
  --fake-amplitude-x '$FakeAmplitudeX' \
  --fake-amplitude-y '$FakeAmplitudeY' \
  --fake-amplitude-z '$FakeAmplitudeZ'
"@

Write-Host "Running dynamic backend performance test inside WSL at $wslBackendPath"
& wsl.exe @wslArgs bash -lc $bashCommand
exit $LASTEXITCODE

