<#
.SYNOPSIS
    Runs every automated test: world logic (pytest), plugin logic (xUnit), and the in-game tests (Surity, the game is
    started in batch mode, which also checks that every hook still finds its target in the installed game build).

.PARAMETER SkipGame
    Skip the in-game tests (they need the game closed and take about a minute).

.PARAMETER ApRoot
    The Archipelago 0.6.x source folder (default: the ARCHIPELAGO_DIR environment variable) with this repository's
    apworld\homura_hime linked into its worlds folder:
        New-Item -ItemType Junction -Path <ApRoot>\worlds\homura_hime -Target <repo>\apworld\homura_hime

.PARAMETER Python
    The Python of the virtual environment Archipelago runs in (default: the ARCHIPELAGO_PYTHON environment variable).

.PARAMETER GameDir
    The Homura Hime install folder, with BepInEx 5 installed (default: the HOMURA_HIME_DIR environment variable, else the
    usual Steam location).

.NOTES
    The in-game tests install the Surity runner and the test plugin into BepInEx\plugins for the run and remove them
    afterwards. See docs/TESTING.md.
#>
param(
    [switch]$SkipGame,
    [string]$ApRoot = $env:ARCHIPELAGO_DIR,
    [string]$Python = $env:ARCHIPELAGO_PYTHON,
    [string]$GameDir = $(if ($env:HOMURA_HIME_DIR) { $env:HOMURA_HIME_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Homura Hime" })
)

if (-not $ApRoot -or -not $Python) {
    throw "Set -ApRoot and -Python (or the ARCHIPELAGO_DIR and ARCHIPELAGO_PYTHON environment variables), see docs/TESTING.md."
}

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = @()

function Step($name, [scriptblock]$body) {
    Write-Host "`n=== $name" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) { $script:failed += $name }
}

# The in-game run: Surity starts the game headless, the tests report back over a local socket.
function Invoke-GameTests {
    if (Get-Process HomuraHime -ErrorAction SilentlyContinue) {
        Write-Host "The game is running: close it first (a headless instance left by an earlier run counts too)." -ForegroundColor Yellow
        $global:LASTEXITCODE = 1
        return
    }

    # a CLI left over from an earlier run still holds Surity's port (45750) and breaks the next run
    Get-Process surity -ErrorAction SilentlyContinue | Stop-Process -Force

    # the plugin under test, then the tests + Surity runner
    dotnet build (Join-Path $root "plugin") -c Release -p:DeployToGame=true -p:GameDir="$GameDir" -nowarn:CS0649 -v q
    dotnet build (Join-Path $root "plugin.gametests") -c Release -p:DeployToGame=true -p:GameDir="$GameDir" -v q

    # an Archipelago server for the tests to connect to (an already running one is reused); serve_test_seed.py generates
    # a fresh seed first, so every run starts from a clean slate (no checked locations remembered)
    $server = $null
    if (-not (Get-NetTCPConnection -LocalPort 38281 -State Listen -ErrorAction SilentlyContinue)) {
        $serveScript = Join-Path $root "tools\serve_test_seed.py"
        $server = Start-Process $Python -ArgumentList "`"$serveScript`"", "`"$ApRoot`"" -WorkingDirectory $ApRoot -WindowStyle Hidden -PassThru
        Start-Sleep 15
    }

    $report = Join-Path $env:TEMP "surity-report.txt"
    $failureFile = Join-Path $env:TEMP "homura-hime-gametests-failures.txt"   # written by Harness.Guard
    $progressFile = Join-Path $env:TEMP "homura-hime-gametests-progress.txt"   # written by Harness.Note
    Remove-Item $report, $failureFile, $progressFile -ErrorAction SilentlyContinue
    $expected = (Select-String -Path (Join-Path $root "plugin.gametests\*.cs") -Pattern '^\s*\[Test\]').Count

    try {
        $env:DOTNET_ROLL_FORWARD = "LatestMajor"   # Surity.CLI targets .NET Core 3.1
        if (-not (Get-Command surity -ErrorAction SilentlyContinue)) {
            dotnet tool install Surity.CLI --global | Out-Null
        }

        # a hanging run must not hang the script: give the whole run three minutes
        $run = Start-Process surity -ArgumentList ("`"" + (Join-Path $GameDir "HomuraHime.exe") + "`"", "-s") `
            -RedirectStandardOutput $report -WindowStyle Hidden -PassThru
        $finished = $run.WaitForExit(180000)
        if (Test-Path $report) { Get-Content $report }

        $problems = @()
        if (-not $finished) { $problems += "the run timed out" }
        # Surity only ever sees passing tests (see Harness.Guard); real failures are in the failure file
        if (Test-Path $failureFile) { $problems += (Get-Content $failureFile) }
        # the CLI does not report its exit code reliably; check that every test actually ran
        $summary = (Get-Content $report -ErrorAction SilentlyContinue | Select-String "^Tests:").Line
        if (-not ($summary -match "(\d+) total" -and [int]$Matches[1] -eq $expected)) {
            $problems += "expected $expected tests, the report says: $summary"
        }

        if ($problems.Count -gt 0 -and (Test-Path $progressFile)) { Write-Host "  progress notes:"; Get-Content $progressFile | Select-Object -Last 15 | ForEach-Object { Write-Host "    $_" } }
        foreach ($problem in $problems) { Write-Host "  FAIL: $problem" -ForegroundColor Red }
        $global:LASTEXITCODE = if ($problems.Count -eq 0) { 0 } else { 1 }
    }
    finally {
        Get-Process surity -ErrorAction SilentlyContinue | Stop-Process -Force
        # Surity leaves the headless game it started running after the tests; only that instance is stopped
        Get-CimInstance Win32_Process -Filter "Name='HomuraHime.exe'" | Where-Object { $_.CommandLine -match "-runSurityTests" } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
        if ($server) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
        Remove-Item (Join-Path $GameDir "BepInEx\plugins\HomuraHimeGameTests") -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $GameDir "BepInEx\plugins\Surity") -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Step "World logic (pytest)" {
    Push-Location $ApRoot
    $env:PYTHONUTF8 = 1
    & $Python -m pytest worlds/homura_hime -q
    Pop-Location
}

Step "Plugin logic (xUnit)" {
    dotnet test (Join-Path $root "plugin.tests") --nologo -v q
}

if (-not $SkipGame) {
    Step "In-game tests (Surity)" { Invoke-GameTests }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Host ("FAILED: " + ($failed -join ", ")) -ForegroundColor Red
    exit 1
}
Write-Host "All tests passed." -ForegroundColor Green

