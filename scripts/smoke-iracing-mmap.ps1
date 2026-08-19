# Live iRacing mmap smoke (KI-002). Requires the sim in a session (or replay) with irsdkEnableMem=1.
# Usage (from repo root):
#   pwsh -File scripts/smoke-iracing-mmap.ps1
# Optional: -TimeoutSeconds 120

param(
    [int]$TimeoutSeconds = 90,
    [int]$BridgeSeconds = 25,
    [string]$LogDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$started = Get-Date
$repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($LogDirectory)) {
    $LogDirectory = Join-Path $env:TEMP ("simpulse-ki002-" + $started.ToString("yyyyMMdd-HHmmss"))
}
New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null

Write-Host "KI-002 live mmap smoke starting. TimeoutSeconds=$TimeoutSeconds BridgeSeconds=$BridgeSeconds LogDirectory=$LogDirectory"

function Test-IracingMmap {
    try {
        $map = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting(
            "Local\IRSDKMemMapFileName",
            [System.IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
        $map.Dispose()
        return $true
    }
    catch {
        return $false
    }
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$mmapOpen = $false
$probeCount = 0
while ((Get-Date) -lt $deadline) {
    $probeCount++
    $sim = Get-Process -Name "iRacingSim64DX11" -ErrorAction SilentlyContinue
    $mmapOpen = Test-IracingMmap
    $elapsed = [int]((Get-Date) - $started).TotalSeconds
    $remain = [Math]::Max(0, $TimeoutSeconds - $elapsed)
    Write-Host ("probe {0} elapsed={1}s remain={2}s sim={3} mmap={4}" -f $probeCount, $elapsed, $remain, [bool]$sim, $mmapOpen)
    if ($mmapOpen) { break }
    Start-Sleep -Seconds 5
}

$probePath = Join-Path $LogDirectory "probe.txt"
@(
    "startedUtc=$($started.ToUniversalTime().ToString('o'))"
    "simProcess=$((Get-Process -Name 'iRacingSim64DX11' -ErrorAction SilentlyContinue) -ne $null)"
    "mmapOpen=$mmapOpen"
    "elapsedSec=$([int]((Get-Date) - $started).TotalSeconds)"
) | Set-Content -Encoding utf8 $probePath

if (-not $mmapOpen) {
    Write-Host "FAIL: mmap Local\IRSDKMemMapFileName not present. Open a session or replay, then re-run."
    Write-Host "Probe written to $probePath"
    Write-Host ("totalElapsedSec={0}" -f [int]((Get-Date) - $started).TotalSeconds)
    exit 2
}

Write-Host "mmap present; building Bridge Release host"
$project = Join-Path $repo "apps\windows-bridge\SimPulse.Bridge\SimPulse.Bridge.csproj"
dotnet build $project --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: Bridge build failed"
    exit 1
}

$exe = Join-Path $repo "apps\windows-bridge\SimPulse.Bridge\bin\Release\net8.0-windows\SimPulse.Bridge.exe"
if (-not (Test-Path $exe)) {
    Write-Host "FAIL: Bridge exe missing at $exe"
    exit 1
}

Write-Host "starting Bridge for ${BridgeSeconds}s. Exe=$exe"
$env:SIMPULSE_FIXTURE_PATH = $null
Remove-Item Env:SIMPULSE_FIXTURE_PATH -ErrorAction SilentlyContinue
$env:SIMPULSE_BRIDGE_TRAY = "0"
$env:SIMPULSE_LOG_LEVEL = "Information"
$env:SIMPULSE_LOG_FILE = "1"
$env:SIMPULSE_LOG_DIR = $LogDirectory
$env:SIMPULSE_BRIDGE_CONSOLE = "1"

$run = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $BridgeSeconds
if (-not $run.HasExited) {
    Stop-Process -Id $run.Id -Force -ErrorAction SilentlyContinue
    Get-Process -Name "SimPulse.Bridge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

$logFiles = Get-ChildItem $LogDirectory -Filter "*.log" -ErrorAction SilentlyContinue
$haystack = @()
foreach ($f in $logFiles) { $haystack += Get-Content $f.FullName }

$hits = $haystack | Where-Object {
    $_ -match "mmap open succeeded|iRacing session started|Race event.*SessionStart|iRacing lap event" -and
    $_ -notmatch "Pin="
}
$hits | Set-Content -Encoding utf8 (Join-Path $LogDirectory "hits.txt")

Write-Host "hits:"
$hits | ForEach-Object { Write-Host $_ }

if (-not ($haystack | Where-Object { $_ -match "mmap open succeeded" })) {
    Write-Host "FAIL: mmap was open but Bridge did not log mmap open. See $LogDirectory"
    Write-Host ("totalElapsedSec={0}" -f [int]((Get-Date) - $started).TotalSeconds)
    exit 3
}

if (-not ($haystack | Where-Object { $_ -match "iRacing session started" })) {
    Write-Host "FAIL: mmap opened but Bridge did not log session start. See $LogDirectory"
    Write-Host ("totalElapsedSec={0}" -f [int]((Get-Date) - $started).TotalSeconds)
    exit 4
}

Write-Host "PASS: live mmap + Bridge session evidence in $LogDirectory"
Write-Host ("totalElapsedSec={0}" -f [int]((Get-Date) - $started).TotalSeconds)
exit 0
