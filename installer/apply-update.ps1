param([string]$ConfigurationPath)
$ErrorActionPreference = 'Stop'

function Write-UpdateLog([string]$Directory, [string]$Message) {
    try { Add-Content -LiteralPath (Join-Path $Directory 'update.log') -Value ((Get-Date -Format o) + ' ' + $Message) } catch { }
}

function Get-POSInstances([string]$ExecutablePath) {
    $matches = @()
    $name = [IO.Path]::GetFileNameWithoutExtension($ExecutablePath)
    foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
        try {
            # Missing process path is treated conservatively: never replace files of an uninspectable caja.
            if (!$process.Path -or [string]::Equals($process.Path, $ExecutablePath, [StringComparison]::OrdinalIgnoreCase)) { $matches += $process.Id }
        } catch { if (!$process.HasExited) { $matches += $process.Id } }
    }
    return $matches
}

function Invoke-Installer([string]$Installer, [string]$InstallDirectory, [string]$LogPath) {
    # Windows paths cannot contain double quotes. ArgumentList needs explicit quoting in Windows PowerShell 5.1.
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOCLOSEAPPLICATIONS', '/NORESTARTAPPLICATIONS',
        ('/DIR="' + $InstallDirectory + '"'), ('/LOG="' + $LogPath + '"'))
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    return $process.ExitCode
}

function Restart-POS([string]$ExecutablePath) {
    Start-Process -FilePath $ExecutablePath -WorkingDirectory ([IO.Path]::GetDirectoryName($ExecutablePath)) -WindowStyle Hidden | Out-Null
}

function Invoke-PendingUpdate([string]$ConfigPath) {
    $updates = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ConfigPath))
    $executable = $null
    $mutex = $null
    $ownsMutex = $false
    $mayReopen = $false
    $result = 1
    try {
        if ([IO.Path]::GetFileName($ConfigPath) -ne 'apply-update.json' -or (Get-Item -LiteralPath $ConfigPath).Length -gt 16384) { throw 'Invalid updater configuration.' }
        $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
        $executable = [IO.Path]::GetFullPath([string]$config.ExecutablePath)
        if ($executable -ne $config.ExecutablePath -or [IO.Path]::GetFileName($executable) -ne 'HeladeriaPOS.Maui.exe') { throw 'Invalid executable path.' }
        $appDirectory = [IO.Path]::GetDirectoryName($executable)
        if (!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath (Join-Path $appDirectory 'unins000.exe'))) { throw 'Installed POS not found.' }
        $mayReopen = $true

        # A PID can be reused; only wait for the original process with its exact creation time and executable.
        if ([int]$config.OriginalPid -le 0) { throw 'Invalid original process ID.' }
        $original = Get-Process -Id ([int]$config.OriginalPid) -ErrorAction SilentlyContinue
        if ($original) {
            if ($original.Path -ne $executable -or $original.StartTime.ToUniversalTime().ToString('O') -ne $config.OriginalStartUtc) { throw 'Original process identity changed.' }
        }
        if ([string]$config.ReadyEventName -notmatch '^Local\\HeladeriaVillegasUpdateReady-[a-f0-9]{32}$') { throw 'Invalid readiness event.' }
        # Caller only closes after the helper validates configuration and can wait for the original process.
        $ready = [Threading.EventWaitHandle]::OpenExisting([string]$config.ReadyEventName)
        try { $ready.Set() | Out-Null } finally { $ready.Dispose() }
        if ($original) {
            if (!$original.WaitForExit(60000)) { $mayReopen = $false; throw 'POS did not close within 60 seconds.' }
        }

        $sha = [Security.Cryptography.SHA256]::Create()
        try { $directoryHash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($appDirectory.ToUpperInvariant()))).Replace('-', '') } finally { $sha.Dispose() }
        $mutex = New-Object Threading.Mutex($false, ('Local\HeladeriaVillegas-' + $directoryHash))
        try { $ownsMutex = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
        if (!$ownsMutex -or @(Get-POSInstances $executable).Count -gt 0) { $mayReopen = $false; throw 'Another POS instance is running.' }

        $package = $config.Package
        if ([string]$package.Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -or [string]$package.Sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid release metadata.' }
        $expectedBase = 'https://github.com/IcedDino/HeladeriaVillegas/releases/download/'
        $asset = '/HeladeriaVillegas-Setup-x64.exe'
        if ($package.DownloadUrl -cne ($expectedBase + 'v' + $package.Version + $asset) -and $package.DownloadUrl -cne ($expectedBase + $package.Version + $asset)) { throw 'Invalid release URL.' }
        $installer = Join-Path $updates 'HeladeriaVillegas-Setup-x64.exe'
        $pendingPath = Join-Path $updates 'pending.json'
        if ((Get-Item -LiteralPath $pendingPath).Length -gt 16384) { throw 'Invalid pending state.' }
        $pending = Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
        if ($pending.Package.Version -cne $package.Version -or $pending.Package.Sha256 -ne $package.Sha256 -or $pending.Package.Size -ne $package.Size -or $pending.Package.DownloadUrl -cne $package.DownloadUrl) { throw 'Pending release changed.' }
        $length = (Get-Item -LiteralPath $installer).Length
        if ([long]$package.Size -le 0 -or [long]$package.Size -gt 2147483648 -or $length -ne [long]$package.Size) { throw 'Installer size mismatch.' }
        if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $package.Sha256) { throw 'Installer SHA256 mismatch.' }

        Write-UpdateLog $updates ('Installing ' + $package.Version)
        $result = Invoke-Installer $installer $appDirectory (Join-Path $updates 'installer.log')
        Write-UpdateLog $updates ('Installer exit code: ' + $result)
        if ($result -eq 0) { Remove-Item -LiteralPath $pendingPath -Force }
    } catch {
        Write-UpdateLog $updates $_.Exception.Message
        $result = 1
    } finally {
        # Release before reopening: the new application takes the same installation mutex.
        if ($ownsMutex) { $mutex.ReleaseMutex() }
        if ($mutex) { $mutex.Dispose() }
        if ($mayReopen -and $executable -and (Test-Path -LiteralPath $executable)) {
            try {
                if (@(Get-POSInstances $executable).Count -eq 0) { Restart-POS $executable }
            } catch { Write-UpdateLog $updates ('Could not reopen POS: ' + $_.Exception.Message) }
        }
    }
    return $result
}

# Dot-sourcing exposes orchestration for verification without starting real processes.
if ($MyInvocation.InvocationName -ne '.') {
    if (!$ConfigurationPath) { exit 1 }
    exit (Invoke-PendingUpdate $ConfigurationPath)
}
