$ErrorActionPreference = "Stop"
$LocalDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } elseif (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { throw ".NET SDK no encontrado." }

& $Dotnet publish .\HeladeriaPOS.Maui.csproj -f net8.0-windows10.0.19041.0 -c Release -r win-x64 -p:WindowsPackageType=None --self-contained true
exit $LASTEXITCODE
