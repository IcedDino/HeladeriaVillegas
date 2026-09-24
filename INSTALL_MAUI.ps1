$ErrorActionPreference = "Stop"
$LocalDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$Dotnet = if (Test-Path $LocalDotnet) { $LocalDotnet } elseif (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { throw ".NET SDK no encontrado." }

Write-Host "Usando: $Dotnet" -ForegroundColor Cyan
& $Dotnet --version
& $Dotnet workload install maui-windows
& $Dotnet workload restore .\HeladeriaPOS.Maui.csproj
Write-Host "MAUI para Windows instalado/restaurado." -ForegroundColor Green
