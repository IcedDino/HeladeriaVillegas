param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')]
    [string]$Version,
    [string]$DotnetPath = 'dotnet',
    [string]$IsccPath = 'ISCC.exe'
)
$ErrorActionPreference = 'Stop'
$parsedReleaseVersion = $null
if (![System.Version]::TryParse($Version, [ref]$parsedReleaseVersion)) { throw 'Versión fuera del rango admitido' }
$root = Split-Path $PSScriptRoot -Parent
$dependencies = Join-Path $root 'artifacts/prerequisites'
New-Item -ItemType Directory -Force -Path $dependencies | Out-Null

$downloads = @{
    'vc_redist.x64.exe' = 'https://aka.ms/vs/17/release/vc_redist.x64.exe'
    'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' = 'https://go.microsoft.com/fwlink/?linkid=2124701'
}
foreach ($name in $downloads.Keys) {
    $target = Join-Path $dependencies $name
    if (!(Test-Path -LiteralPath $target)) {
        & curl.exe -fsSL --retry 3 $downloads[$name] -o $target
        if ($LASTEXITCODE -ne 0) { throw "No se pudo descargar $name" }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $target
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "Firma de Microsoft inválida: $name ($($signature.Status))"
    }
    if ($name -eq 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' -and (Get-Item -LiteralPath $target).Length -lt 50MB) {
        throw 'WebView2 debe ser el instalador completo sin conexión, no el bootstrapper'
    }
}

Push-Location $root
try {
    & $DotnetPath publish ./HeladeriaPOS.Maui.csproj -f net8.0-windows10.0.19041.0 -c Release -r win-x64 -p:Platform=x64 "-p:ReleaseVersion=$Version" -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --self-contained true
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación Release' }
    & $IsccPath "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'HeladeriaPOS.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador' }
    $installer = Join-Path $root 'artifacts/installer/HeladeriaVillegas-Setup-x64.exe'
    Get-FileHash -LiteralPath $installer -Algorithm SHA256 | Format-List
} finally {
    Pop-Location
}
