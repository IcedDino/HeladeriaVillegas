param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.2',
    [Parameter(Mandatory = $true)][string]$BaselineDirectory,
    [string]$DotnetPath = (Join-Path $env:LOCALAPPDATA 'NerqovaBuild/dotnet/dotnet.exe'),
    [string]$NsisPath = (Join-Path $env:LOCALAPPDATA 'NerqovaBuild/NSIS/makensis.exe'),
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$baseline = (Resolve-Path -LiteralPath $BaselineDirectory).Path
$baselineDll = Join-Path $baseline 'HeladeriaPOS.Maui.dll'
if (!(Test-Path -LiteralPath $baselineDll)) { throw 'Original installation baseline is required.' }
$baselineVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($baselineDll).FileVersion
if ([version]$Version -le [version]$baselineVersion) { throw 'Update version must exceed the installed version.' }
$publish = if ($SkipPublish) { Join-Path $root 'bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish' } else { Join-Path $root ('artifacts/light-publish/' + [guid]::NewGuid().ToString('N')) }
$stage = Join-Path $root ('artifacts/light-update/' + $Version + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage,(Join-Path $root 'artifacts/installer') | Out-Null
if (!$SkipPublish) {
    & $DotnetPath publish (Join-Path $root 'HeladeriaPOS.Maui.csproj') -o $publish -f net8.0-windows10.0.19041.0 -c Release -r win-x64 -p:Platform=x64 "-p:ReleaseVersion=$Version" -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --self-contained true
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
}
if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publish 'HeladeriaPOS.Maui.dll')).FileVersion -ne "$Version.0") {
    throw 'Published DLL version does not match.'
}
$certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=Nerqova' -and $_.HasPrivateKey } | Sort-Object NotAfter -Descending | Select-Object -First 1
if (!$certificate) { throw 'Nerqova signing certificate is required.' }
$signTool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') -Recurse -Filter signtool.exe | Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (!$signTool) { throw 'Windows SDK signtool is required.' }
& $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /sha1 $certificate.Thumbprint (Join-Path $publish 'HeladeriaPOS.Maui.exe')
if ($LASTEXITCODE -ne 0) { throw 'Application signing failed.' }
$retained = @()
$included = @()
foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
    $relative = $file.FullName.Substring($publish.Length + 1)
    if ($relative -match '(?i)(^|\\)(pos\.db.*|.*\.db|vc_redist.*|MicrosoftEdgeWebView2RuntimeInstaller.*)$') { throw "Unexpected data/prerequisite in publish: $relative" }
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $old = Join-Path $baseline $relative
    if ((Test-Path -LiteralPath $old) -and (Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash -eq $hash) {
        $retained += [ordered]@{ Path = $relative; Sha256 = $hash }
    } else {
        # Runtime/framework changes need a full installer; never silently omit or bundle them.
        if ($file.Extension -eq '.dll' -and $file.Name -ne 'HeladeriaPOS.Maui.dll') { throw "Dependency differs from original installation: $relative. Full package required." }
        if ($file.Name -match '^(coreclr|clrjit|hostfxr|hostpolicy|createdump|mscordaccore|mscordbi)\.') { throw "Runtime changed: $relative" }
        $target = Join-Path $stage $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
        $included += [ordered]@{ Path = $relative; Sha256 = $hash; Bytes = $file.Length }
    }
}
if (!$retained.Count -or !($included | Where-Object Path -eq 'HeladeriaPOS.Maui.dll')) { throw 'Invalid update payload.' }
$manifest = Join-Path $stage 'retained-files.json'
ConvertTo-Json -InputObject @($retained) -Depth 5 | Set-Content -LiteralPath $manifest -Encoding UTF8
& $NsisPath "/DAPP_VERSION=$Version" '/DUPDATE_ONLY' "/DPUBLISH_DIR=$stage" (Join-Path $PSScriptRoot 'HeladeriaVillegas.nsi')
if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed.' }
$installer = Join-Path $root 'artifacts/installer/HeladeriaVillegas-Setup-x64.exe'
& $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /sha1 $certificate.Thumbprint $installer
if ($LASTEXITCODE -ne 0) { throw 'Installer signing failed.' }
foreach ($signedFile in @($installer,(Join-Path $publish 'HeladeriaPOS.Maui.exe'))) {
    $signature = Get-AuthenticodeSignature -LiteralPath $signedFile
    if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint -or !$signature.TimeStamperCertificate) { throw 'Output signature or timestamp is missing.' }
}
[ordered]@{
    Version = $Version; BaselineVersion = $baselineVersion; BaselineDirectory = $baseline
    File = $installer; Bytes = (Get-Item -LiteralPath $installer).Length
    Sha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    IncludedFiles = $included; RetainedFiles = $retained.Count
    PrerequisitesBundled = $false; Published = $false
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path (Split-Path $installer) 'release-readiness.json') -Encoding UTF8
Get-Item -LiteralPath $installer | Select-Object FullName,Length
Get-FileHash -LiteralPath $installer -Algorithm SHA256
