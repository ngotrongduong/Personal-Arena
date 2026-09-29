# Watch the AI while it trains: keeps exporting the newest checkpoint to latest.brain
# and opens the viewer, which hot-loads each new brain. Closing the viewer stops the exporter.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root '.venv-ml\Scripts\python.exe'
$exporterScript = Join-Path $root 'Trainer\export_brain.py'
$viewer = Join-Path $root 'Build\Watch\PersonalArenaWatch.exe'

if (-not (Test-Path $viewer)) {
    Write-Host "Viewer not built yet: run Personal Arena > Build Watch AI Viewer (Windows)." -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

$exporter = $null
if (Test-Path $python) {
    $exporter = Start-Process -FilePath $python -ArgumentList @("`"$exporterScript`"", '--watch', '--behavior', 'all') `
        -WorkingDirectory $root -WindowStyle Hidden -PassThru
}

try {
    Start-Process -FilePath $viewer -ArgumentList @('-runs', "`"$(Join-Path $root 'Trainer\runs')`"") -Wait
}
finally {
    if ($exporter -and -not $exporter.HasExited) {
        Stop-Process -Id $exporter.Id -Force
    }
}
