param(
    [string]$Version = '1.0.2',
    [string]$InstallerPath,
    [string]$DotnetPath = (Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe'),
    [switch]$Published
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$InstallerPath) { $InstallerPath = Join-Path $root 'artifacts/installer/HeladeriaVillegas-Setup-x64.exe' }
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Name -cne 'HeladeriaVillegas-Setup-x64.exe') { throw 'Installer filename is incompatible with 1.0.0.' }
if ($installer.VersionInfo.ProductVersion.Trim() -ne $Version) { throw 'Installer version does not match the intended Release.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$' -or [version]$Version -le [version]'1.0.0') { throw 'Version must be newer than 1.0.0.' }

$workspace = Join-Path $root 'artifacts/legacy-update-check'
$sources = Join-Path $workspace 'Sources'
New-Item -ItemType Directory -Force -Path $sources | Out-Null
foreach ($name in @('GitHubUpdateService.cs', 'WindowsUpdateCoordinator.cs', 'UpdateStateStore.cs')) {
    $original = & git -C $root show "v1.0.0:Services/$name"
    if ($LASTEXITCODE -ne 0) { throw "Could not read original 1.0.0 source: $name" }
    $original | Set-Content -LiteralPath (Join-Path $sources $name) -Encoding UTF8
}
$legacyInstaller = (& git -C $root show 'v1.0.0:installer/HeladeriaPOS.iss') -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Could not read original installer definition.' }
$currentInstaller = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'HeladeriaPOS.iss') -Raw
foreach ($setting in @('AppId', 'DefaultDirName', 'OutputBaseFilename')) {
    $pattern = '(?m)^' + $setting + '=(.+)$'
    $before = [regex]::Match($legacyInstaller, $pattern).Groups[1].Value.Trim()
    $after = [regex]::Match($currentInstaller, $pattern).Groups[1].Value.Trim()
    if (!$before -or $before -cne $after) { throw "Incompatible Inno Setup setting: $setting" }
}
# The asset actually published under v1.0.0 was rebuilt as 1.0.1 with publisher Nerqova.
# Its metadata differs from the source tag; retain the published asset's data directory.
$newManifest = Get-Content -LiteralPath (Join-Path $root 'Platforms/Windows/Package.appxmanifest') -Raw -Encoding UTF8
if ($newManifest -notmatch '<PublisherDisplayName>Nerqova</PublisherDisplayName>' -or
    $newManifest -notmatch '<Identity Name="com.cesardonnet.heladeriapos"') {
    throw 'Published application data identity changed: preserve Nerqova/com.cesardonnet.heladeriapos.'
}
if ([version]$Version -le [version]'1.0.1') { throw 'The published previous binary is already 1.0.1; the next version must be newer.' }
if ($currentInstaller -match '\[(InstallDelete|UninstallDelete)\]') { throw 'Installer deletion rules require review before distributing an update.' }
$testDirectory = Join-Path $workspace ('Run-' + [Guid]::NewGuid().ToString('N'))
$arguments = @('run', '--project', (Join-Path $root 'Verification/LegacyUpdateCompatibility/LegacyUpdateCompatibility.csproj'), '--', $installer.FullName, $Version, $testDirectory)
if ($Published) { $arguments += '--published' }
& $DotnetPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Original updater compatibility check failed.' }
$originalHelper = & git -C $root show 'v1.0.0:installer/apply-update.ps1'
if ($LASTEXITCODE -ne 0) { throw 'Could not read original installation helper.' }
$helperPath = Join-Path $workspace 'apply-update-1.0.0.ps1'
$originalHelper | Set-Content -LiteralPath $helperPath -Encoding UTF8
& (Join-Path $PSScriptRoot 'verify-updater.ps1') -UpdaterScriptPath $helperPath -LegacyInno
Write-Output 'OK: original installer identity, folder and asset filename preserved'
