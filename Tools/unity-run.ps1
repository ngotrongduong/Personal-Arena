<#
.SYNOPSIS
  Runs Unity headless and prints ONLY a short summary (saves agent tokens). The full log stays on disk.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools/unity-run.ps1 -Tests EditMode
  powershell -ExecutionPolicy Bypass -File Tools/unity-run.ps1 -Method PersonalArena.View.Editor.WatchBuild.BuildWindows -ExtraArgs "-watchBuildOutput ../Build/WatchNext"   # relative paths start at Unity/
  powershell -ExecutionPolicy Bypass -File Tools/unity-run.ps1 -Method PersonalArena.ML.Editor.TrainingBuild.BuildWindows -ExtraArgs "-buildPath Build/TrainingNext"
  powershell -ExecutionPolicy Bypass -File Tools/unity-run.ps1 -Compile        # compile check only (no tests, no build)

.NOTES
  Output: one RESULT line, counts, then at most -MaxErrors distinct error lines / failed tests.
  Full log: results/unity-<name>.log, test XML: results/<mode>.xml. Grep those only if the summary is not enough.
  Exit code: 0 = success, 1 = failure. Works with Windows PowerShell 5.1.
#>
param(
    [ValidateSet("EditMode", "PlayMode")] [string]$Tests,
    [string]$Method,
    [switch]$Compile,
    [string]$ExtraArgs = "",
    [string]$UnityExe = $(if ($env:UNITY_EXE) { $env:UNITY_EXE } else { "C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" }),
    [int]$MaxErrors = 15
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "Unity"
$results = Join-Path $repo "results"
New-Item -ItemType Directory -Force -Path $results | Out-Null

if (-not (Test-Path $UnityExe)) { Write-Output "RESULT: FAIL - Unity not found at '$UnityExe' (set -UnityExe or `$env:UNITY_EXE)"; exit 1 }
$modes = @($Tests, $Method, $(if ($Compile) { "compile" })) | Where-Object { $_ }
if ($modes.Count -ne 1) { Write-Output "RESULT: FAIL - pass exactly one of -Tests, -Method, -Compile"; exit 1 }

$name = if ($Tests) { $Tests.ToLower() } elseif ($Compile) { "compile" } else { ($Method -split '\.')[-2..-1] -join '-' }
$log = Join-Path $results "unity-$name.log"
$unityArgs = @("-batchmode", "-nographics", "-projectPath", "`"$project`"", "-logFile", "`"$log`"")
$xml = $null
if ($Tests) {
    $xml = Join-Path $results "$($Tests.ToLower()).xml"
    if (Test-Path $xml) { Remove-Item $xml -Force }
    $unityArgs += @("-runTests", "-testPlatform", $Tests, "-testResults", "`"$xml`"")   # no -quit with -runTests
} elseif ($Compile) {
    $unityArgs += @("-quit")
} else {
    $unityArgs += @("-quit", "-executeMethod", $Method)
}
if ($ExtraArgs) { $unityArgs += $ExtraArgs }

$watch = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
$watch.Stop()
$secs = [int]$watch.Elapsed.TotalSeconds
$ok = ($proc.ExitCode -eq 0)

# Distinct compiler errors / exceptions / build result lines from the log, never the whole log.
$errorLines = @()
$buildLine = $null
if (Test-Path $log) {
    $errorLines = @(Select-String -Path $log -Pattern 'error CS\d+', '^\S*Exception: ', 'Build Failed', 'Aborting batchmode', 'Scripts have compiler errors' |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
    $buildLine = Select-String -Path $log -Pattern 'Build Finished, Result: .*', 'Build completed with a result of' |
        Select-Object -Last 1 | ForEach-Object { $_.Line.Trim() }
}

if ($Tests) {
    if (-not (Test-Path $xml)) {
        Write-Output "RESULT: FAIL - no test results ($Tests, exit $($proc.ExitCode), ${secs}s). Log: $log"
        $errorLines | Select-Object -First $MaxErrors | ForEach-Object { Write-Output "  $_" }
        exit 1
    }
    [xml]$doc = Get-Content $xml -Raw
    $run = $doc.'test-run'
    $failed = [int]$run.failed
    $ok = ($failed -eq 0) -and ([int]$run.total -gt 0)
    Write-Output ("RESULT: {0} - {1} total, {2} passed, {3} failed, {4} skipped ({5}s). XML: {6}" -f `
        $(if ($ok) { "PASS" } else { "FAIL" }), $run.total, $run.passed, $failed, $run.skipped, $secs, $xml)
    if ($failed -gt 0) {
        $doc.SelectNodes("//test-case[@result='Failed']") | Select-Object -First $MaxErrors | ForEach-Object {
            $node = $_.SelectSingleNode('failure/message'); $msg = ($(if ($node) { $node.InnerText } else { '' }) -replace '\s+', ' ').Trim()
            if ($msg.Length -gt 300) { $msg = $msg.Substring(0, 300) + "..." }
            Write-Output "  FAILED $($_.fullname): $msg"
        }
    }
} else {
    if ($errorLines.Count -gt 0 -and -not $buildLine) { $ok = $false }
    if ($buildLine -and $buildLine -notmatch 'Succeeded|success') { $ok = $false }
    Write-Output ("RESULT: {0} - {1} (exit {2}, {3}s). Log: {4}" -f `
        $(if ($ok) { "PASS" } else { "FAIL" }), $name, $proc.ExitCode, $secs, $log)
    if ($buildLine) { Write-Output "  $buildLine" }
}
if ($errorLines.Count -gt 0) {
    Write-Output "  $($errorLines.Count) distinct error line(s), first $([Math]::Min($MaxErrors, $errorLines.Count)):"
    $errorLines | Select-Object -First $MaxErrors | ForEach-Object {
        $line = if ($_.Length -gt 300) { $_.Substring(0, 300) + "..." } else { $_ }
        Write-Output "  $line"
    }
}
if ($ok) { exit 0 } else { exit 1 }
