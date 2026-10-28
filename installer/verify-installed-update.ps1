#requires -RunAsAdministrator
param(
    [string]$InstallDirectory = 'C:\Program Files\Heladeria Villegas POS',
    [string]$ExpectedVersion = '1.0.2',
    [switch]$RepairLegacyMarker
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $root 'artifacts/installed-update-test.json'
$transcript = Join-Path $root 'artifacts/installed-update-test.log'
$executable = Join-Path $InstallDirectory 'HeladeriaPOS.Maui.exe'
$dll = Join-Path $InstallDirectory 'HeladeriaPOS.Maui.dll'
$updates = Join-Path $env:ProgramData 'HeladeriaVillegasUpdates'
$pendingPath = Join-Path $updates 'pending.json'
$legacyMarker = Join-Path $InstallDirectory 'unins000.exe'
$createdMarker = $false
$report = [ordered]@{ StartedUtc = [DateTime]::UtcNow.ToString('O'); OriginalVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion; OriginalDetected = $false; LegacyMarkerRepaired = $false; DownloadVerified = $false; InstalledVersion = $null; Result = 'running' }
function Save-Report { $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8 }
function Open-TestPOS {
    $process = Start-Process -FilePath $executable -WorkingDirectory $InstallDirectory -WindowStyle Hidden -PassThru
    Write-Output ('Started original POS PID=' + $process.Id) | Out-Host
    return $process
}
function Stop-TestPOS($process) {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(10000) | Out-Null }
}
function Wait-ForPending([int]$Seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        if (Test-Path -LiteralPath $pendingPath) {
            $pending = Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
            if ($pending.Package.Version -eq $ExpectedVersion) { return $pending }
        }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
Start-Transcript -Path $transcript -Force
try {
    if (@(Get-Process HeladeriaPOS.Maui -ErrorAction SilentlyContinue).Count -ne 0) { throw 'Close the POS before starting this controlled test.' }
    if (Test-Path -LiteralPath $pendingPath) { throw 'Existing pending update requires review before this test.' }
    if (Test-Path -LiteralPath $legacyMarker) { throw 'Legacy marker already exists; this is not an unmodified NSIS baseline.' }
    if ($report.OriginalVersion -ne '1.0.1.0') { throw 'Expected original published binary 1.0.1.0.' }
    Save-Report
    $original = Open-TestPOS
    $pending = Wait-ForPending 35
    $report.OriginalDetected = $null -ne $pending
    Save-Report
    if (!$pending) {
        'Original unmodified NSIS installation did NOT detect the public Release in 35 seconds.'
        if (!$RepairLegacyMarker) { $report.Result = 'original-updater-disabled'; Save-Report; return }
        Stop-TestPOS $original
        # Reversible local repair: supply the actual NSIS uninstaller under the marker required by the legacy binary.
        # This is an explicit intervention, never evidence of an automatic update on an unmodified first installation.
        Copy-Item -LiteralPath (Join-Path $InstallDirectory 'uninstall.exe') -Destination $legacyMarker
        $createdMarker = $true
        $report.LegacyMarkerRepaired = $true
        Save-Report
        $original = Open-TestPOS
        $pending = Wait-ForPending 90
        if (!$pending) { throw 'Original updater did not download the Release after local marker repair.' }
    }
    $download = Join-Path $updates 'HeladeriaVillegas-Setup-x64.exe'
    if ((Get-Item -LiteralPath $download).Length -ne $pending.Package.Size -or
        (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $pending.Package.Sha256) { throw 'Actual downloaded update hash/size mismatch.' }
    $report.DownloadVerified = $true
    $report.Package = $pending.Package
    Copy-Item -LiteralPath $pendingPath -Destination (Join-Path $root 'artifacts/installed-update-pending.json')
    Save-Report
    'Original updater downloaded and verified PUBLIC GitHub Release ' + $ExpectedVersion
    Stop-TestPOS $original
    $restarted = Open-TestPOS
    $deadline = [DateTime]::UtcNow.AddSeconds(150)
    do {
        Start-Sleep -Seconds 2
        $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
        if ($installedVersion -eq ($ExpectedVersion + '.0') -and !(Test-Path -LiteralPath $pendingPath)) {
            Start-Sleep -Seconds 15
            if (@(Get-Process HeladeriaPOS.Maui -ErrorAction SilentlyContinue).Count -eq 0) { throw 'Updated POS was not reopened.' }
            $report.InstalledVersion = $installedVersion
            $report.Result = 'passed-after-explicit-local-marker-repair'
            Save-Report
            'PASS: ORIGINAL updater and ORIGINAL helper installed public package and reopened updated POS.'
            if (Test-Path -LiteralPath (Join-Path $updates 'update.log')) { Copy-Item -LiteralPath (Join-Path $updates 'update.log') -Destination (Join-Path $root 'artifacts/installed-update-helper.log') }
            return
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Timed out waiting for original updater to install the public package.'
} catch {
    $report.Result = 'failed'; $report.Error = $_.Exception.ToString(); Save-Report
    $_ | Out-String | Write-Output
} finally {
    if ($createdMarker -and (Test-Path -LiteralPath $legacyMarker)) { Remove-Item -LiteralPath $legacyMarker -Force }
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O'); Save-Report
    Stop-Transcript
}
