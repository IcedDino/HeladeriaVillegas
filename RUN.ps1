$ErrorActionPreference = "Stop"
$LocalDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } elseif (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { throw ".NET SDK no encontrado." }

Write-Host "Restaurando paquetes..." -ForegroundColor Cyan
& $Dotnet restore .\HeladeriaPOS.Maui.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Compilando y ejecutando Heladería POS MAUI..." -ForegroundColor Cyan
& $Dotnet build .\HeladeriaPOS.Maui.csproj -t:Run -f net8.0-windows10.0.19041.0 -c Debug -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=None
exit $LASTEXITCODE
