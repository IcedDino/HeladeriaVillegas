$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'apply-update.ps1'
if (!(Test-Path -LiteralPath $scriptPath)) { throw 'FAIL: updater script does not exist' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('updater-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
try {
    # Load real orchestration; replace only external process operations, never file verification.
    . $scriptPath
    $script:FixtureRoot = $fixture
    $script:InstallerCalls = 0
    $script:RestartCalls = 0
    $script:InstallerResult = 0
    function Invoke-Installer([string]$Installer, [string]$InstallDirectory, [string]$LogPath) {
        if (!(Test-Path -LiteralPath $Installer)) { throw 'Missing verified installer' }
        if ($InstallDirectory -ne (Join-Path $script:FixtureRoot 'App')) { throw 'Wrong install directory' }
        $script:InstallerCalls++
        return $script:InstallerResult
    }
    function Restart-POS([string]$ExecutablePath) { $script:RestartCalls++ }
    function Get-POSInstances([string]$ExecutablePath) { return @() }
    $app = Join-Path $fixture 'App'
    $updates = Join-Path $fixture 'Updates'
    New-Item -ItemType Directory -Force -Path $app,$updates | Out-Null
    $executable = Join-Path $app 'HeladeriaPOS.Maui.exe'
    Set-Content -LiteralPath $executable -Value 'fake app'
    Set-Content -LiteralPath (Join-Path $app 'unins000.exe') -Value 'installer marker'
    $installer = Join-Path $updates 'HeladeriaVillegas-Setup-x64.exe'
    [IO.File]::WriteAllBytes($installer, [Text.Encoding]::UTF8.GetBytes('installer fixture'))
    $package = @{ Version='1.1.0'; DownloadUrl='https://github.com/IcedDino/HeladeriaVillegas/releases/download/v1.1.0/HeladeriaVillegas-Setup-x64.exe'; Size=(Get-Item -LiteralPath $installer).Length; Sha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash }
    $config = @{ OriginalPid=999999; OriginalStartUtc='2026-10-05T12:00:00Z'; ExecutablePath=$executable; Package=$package }
    $readyName = 'Local\HeladeriaVillegasUpdateReady-' + [Guid]::NewGuid().ToString('N')
    $ready = New-Object Threading.EventWaitHandle($false, [Threading.EventResetMode]::ManualReset, $readyName)
    $config.ReadyEventName = $readyName
    $configPath = Join-Path $updates 'apply-update.json'
    $pendingPath = Join-Path $updates 'pending.json'
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $configPath
    @{ Package=$package; LastAttemptUtc='2026-10-05T12:00:00Z' } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $pendingPath
    $result = Invoke-PendingUpdate $configPath
    if (!$ready.WaitOne(0)) { throw 'FAIL: helper must signal readiness before app shuts down' }
    if ($result -ne 0 -or $script:InstallerCalls -ne 1 -or $script:RestartCalls -ne 1 -or (Test-Path -LiteralPath $pendingPath)) { throw 'FAIL: successful installation clears pending and reopens' }

    $script:InstallerResult = 1
    @{ Package=$package; LastAttemptUtc='2026-10-05T12:00:00Z' } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $pendingPath
    $result = Invoke-PendingUpdate $configPath
    if ($result -ne 1 -or $script:RestartCalls -ne 2 -or !(Test-Path -LiteralPath $pendingPath)) { throw 'FAIL: failed installer retains pending and reopens old app' }

    $script:InstallerResult = 0
    function Get-POSInstances([string]$ExecutablePath) { return @(12345) }
    $result = Invoke-PendingUpdate $configPath
    if ($result -eq 0 -or $script:InstallerCalls -ne 2 -or $script:RestartCalls -ne 2) { throw 'FAIL: another open caja must defer without reopening a duplicate' }
    function Get-POSInstances([string]$ExecutablePath) { return @() }
    [IO.File]::WriteAllBytes($installer, [Text.Encoding]::UTF8.GetBytes('tampered installer'))
    $result = Invoke-PendingUpdate $configPath
    if ($result -eq 0 -or $script:InstallerCalls -ne 2 -or $script:RestartCalls -ne 3) { throw 'FAIL: corrupt installer must not execute and old app reopens' }
    $ready.Reset() | Out-Null
    Set-Content -LiteralPath $configPath -Value 'bad json'
    $result = Invoke-PendingUpdate $configPath
    if ($result -eq 0 -or $script:InstallerCalls -ne 2) { throw 'FAIL: corrupt config must not execute installer' }
    if ($ready.WaitOne(0)) { throw 'FAIL: failed startup must not tell the app to close' }
    $ready.Dispose()
    Write-Output 'OK: auxiliar verifica archivos, preserva instalación fallida, excluye otra instancia y reabre caja'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $allowedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolvedFixture.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedFixture) -notlike 'updater-fixture-*') { throw 'Unexpected fixture cleanup path' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
