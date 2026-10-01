# Watch the AI while it trains: keeps exporting the newest checkpoint to latest.brain
# and opens the viewer, which hot-loads each new brain. Closing the viewer stops the exporter.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root '.venv-ml\Scripts\python.exe'
$exporterScript = Join-Path $root 'Trainer\export_brain.py'
$viewer = Join-Path $root 'Build\Watch\PersonalArenaWatch.exe'

# A newer viewer built while the old one was open waits in Build\WatchNext; install it now.
$current = Join-Path $root 'Build\Watch'
$next = Join-Path $root 'Build\WatchNext'
$old = Join-Path $root 'Build\Watch.old'
if (Test-Path (Join-Path $next 'PersonalArenaWatch.exe')) {
    $open = Get-Process -Name PersonalArenaWatch -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($current + '\', [StringComparison]::OrdinalIgnoreCase) }
    if (-not $open) {
        try {
            if (Test-Path $old) { Remove-Item -LiteralPath $old -Recurse -Force }
            if (Test-Path $current) { Rename-Item -LiteralPath $current -NewName 'Watch.old' }
            Rename-Item -LiteralPath $next -NewName 'Watch'
            Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host 'Installed the new viewer build.' -ForegroundColor Green
        }
        catch {
            Write-Host "Could not install the new viewer build: $_" -ForegroundColor Yellow
            if (-not (Test-Path $current) -and (Test-Path $old)) { Rename-Item -LiteralPath $old -NewName 'Watch' }
        }
    }
}

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

$exitCode = 0
$startedAt = Get-Date
try {
    $viewerProcess = Start-Process -FilePath $viewer -ArgumentList @('-runs', "`"$(Join-Path $root 'Trainer\runs')`"") -PassThru
    $null = $viewerProcess.Handle  # keeps the handle so ExitCode is readable after the viewer closes
    $viewerProcess.WaitForExit()
    $exitCode = $viewerProcess.ExitCode
}
finally {
    if ($exporter -and -not $exporter.HasExited) {
        Stop-Process -Id $exporter.Id -Force
    }
}

# A viewer that fails while starting closes by itself within seconds: say so (in Vietnamese) and point to its log.
# The text is kept ASCII here (\u escapes) so Windows PowerShell reads this file correctly without a BOM.
$secondsOpen = ((Get-Date) - $startedAt).TotalSeconds
if ($exitCode -ne 0 -and $exitCode -ne $null -and $secondsOpen -lt 15) {
    $log = Join-Path $env:USERPROFILE 'AppData\LocalLow\ngotrongduong\Personal Arena\Player.log'
    Write-Host ''
    Write-Host ([regex]::Unescape("Tr\u00ecnh xem AI b\u1ecb l\u1ed7i khi m\u1edf (m\u00e3 l\u1ed7i $exitCode).")) -ForegroundColor Red
    Write-Host ([regex]::Unescape("Nh\u1eadt k\u00fd l\u1ed7i n\u1eb1m \u1edf:")) -ForegroundColor Yellow
    Write-Host "  $log"
    Write-Host ([regex]::Unescape("H\u00e3y g\u1eedi file n\u00e0y cho Claude \u0111\u1ec3 s\u1eeda."))
    Read-Host ([regex]::Unescape("Nh\u1ea5n Enter \u0111\u1ec3 \u0111\u00f3ng"))
    exit $exitCode
}
