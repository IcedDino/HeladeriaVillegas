param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')]
    [string]$Version,
    [string]$DotnetPath,
    [string]$NsisPath,
    [string]$SigningCertThumbprint
)

$ErrorActionPreference = 'Stop'
$parsedReleaseVersion = $null
if (![System.Version]::TryParse($Version, [ref]$parsedReleaseVersion)) {
    throw 'Version is outside the supported range.'
}

$root = Split-Path $PSScriptRoot -Parent
$dependencies = Join-Path $root 'artifacts/prerequisites'
$installerDirectory = Join-Path $root 'artifacts/installer'
New-Item -ItemType Directory -Force -Path $dependencies, $installerDirectory | Out-Null

if (!$DotnetPath) {
    $localDotnet = Join-Path $env:LOCALAPPDATA 'NerqovaBuild/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) {
        $DotnetPath = $localDotnet
    } else {
        $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
    }
}
if (!$NsisPath) {
    $localNsis = Join-Path $env:LOCALAPPDATA 'NerqovaBuild/NSIS/makensis.exe'
    if (Test-Path -LiteralPath $localNsis) {
        $NsisPath = $localNsis
    } else {
        $NsisPath = (Get-Command makensis.exe -ErrorAction Stop).Source
    }
}
if (!(Test-Path -LiteralPath $DotnetPath)) { throw "dotnet.exe not found: $DotnetPath" }
if (!(Test-Path -LiteralPath $NsisPath)) { throw "makensis.exe not found: $NsisPath" }

$downloads = @{
    'vc_redist.x64.exe' = 'https://aka.ms/vs/17/release/vc_redist.x64.exe'
    # Official Evergreen Standalone Installer x64; linkid 2124703 is the online bootstrapper.
    'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' = 'https://go.microsoft.com/fwlink/?linkid=2124701'
}

foreach ($name in $downloads.Keys) {
    $target = Join-Path $dependencies $name
    if (!(Test-Path -LiteralPath $target)) {
        & curl.exe -fSL --retry 3 $downloads[$name] -o $target
        if ($LASTEXITCODE -ne 0) { throw "Could not download $name." }
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $target
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "Microsoft signature validation failed: $name ($($signature.Status))."
    }
    if ($name -eq 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' -and (Get-Item -LiteralPath $target).Length -lt 50MB) {
        throw 'WebView2 must be the offline Evergreen Standalone Installer, not its bootstrapper.'
    }
}

$signingCertificate = $null
if ($SigningCertThumbprint) {
    $certPath = "Cert:\CurrentUser\My\$($SigningCertThumbprint.Replace(' ', ''))"
    $signingCertificate = Get-Item $certPath -ErrorAction SilentlyContinue
} else {
    $signingCertificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq 'CN=Nerqova' -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}
if (!$signingCertificate -or !$signingCertificate.HasPrivateKey -or $signingCertificate.Subject -ne 'CN=Nerqova') {
    throw 'A certificate with a private key and subject CN=Nerqova is required in CurrentUser\My.'
}

$signToolRoot = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10\bin'
$signTool = Get-ChildItem $signToolRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (!$signTool) { throw 'Windows SDK signtool.exe was not found.' }

Push-Location $root
try {
    $publishArguments = @(
        'publish',
        './HeladeriaPOS.Maui.csproj',
        '-f', 'net8.0-windows10.0.19041.0',
        '-c', 'Release',
        '-r', 'win-x64',
        '-p:Platform=x64',
        "-p:ReleaseVersion=$Version",
        '-p:WindowsPackageType=None',
        '-p:WindowsAppSDKSelfContained=true',
        '--self-contained', 'true'
    )
    & $DotnetPath @publishArguments
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

    $publishDirectory = Join-Path $root 'bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish'
    $application = Join-Path $publishDirectory 'HeladeriaPOS.Maui.exe'
    if (!(Test-Path -LiteralPath $application)) { throw 'Published application executable was not produced.' }

    & $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /sha1 $signingCertificate.Thumbprint /d 'Heladeria Villegas POS' $application
    if ($LASTEXITCODE -ne 0) { throw 'Signing the application executable failed.' }

    & $NsisPath "/DAPP_VERSION=$Version" (Join-Path $PSScriptRoot 'HeladeriaVillegas.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'NSIS installer compilation failed.' }

    $installer = Join-Path $installerDirectory 'HeladeriaVillegas-Setup-x64.exe'
    & $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /sha1 $signingCertificate.Thumbprint /d 'Heladeria Villegas POS Setup' $installer
    if ($LASTEXITCODE -ne 0) { throw 'Signing the installer failed.' }

    foreach ($file in @($application, $installer)) {
        $signature = Get-AuthenticodeSignature -LiteralPath $file
        if ($signature.SignerCertificate.Thumbprint -ne $signingCertificate.Thumbprint) {
            throw "Signed output does not have the Nerqova signer: $file"
        }
        if (!$signature.TimeStamperCertificate) { throw "The timestamp is missing from signed output: $file" }
    }

    Get-FileHash -LiteralPath $installer -Algorithm SHA256 | Format-List
    Get-Item -LiteralPath $installer | Select-Object FullName, Length
} finally {
    Pop-Location
}
