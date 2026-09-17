# Heladería POS — .NET MAUI 8

Punto de venta táctil para Windows, rehecho con **.NET MAUI**, **.NET 8**, **CommunityToolkit.Mvvm**, **EF Core** y **SQLite**.

## Incluye

- Catálogo táctil por categorías.
- Imágenes e iconos propios para productos y botones.
- Icono de aplicación con cono de helado.
- Ticket activo con botones grandes `+` y `−`.
- Cobro rápido: Exacto, +20, +50, +100, +200, +500 y Limpiar.
- Motor de precios para Snacks, Helados y Especialidades.
- Base SQLite 100% local/offline.
- Persistencia de tickets.
- Diseño pensado para pantallas táctiles de caja.
- Ventana maximizada al iniciar en Windows.

## Requisito importante: workload de MAUI

Tu SDK de .NET 8 no instala MAUI automáticamente. Ejecuta una vez:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\INSTALL_MAUI.ps1
```

O manualmente:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" workload install maui-windows
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" workload restore .\HeladeriaPOS.Maui.csproj
```

## Ejecutar

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\RUN.ps1
```

O manualmente:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" restore .\HeladeriaPOS.Maui.csproj
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build .\HeladeriaPOS.Maui.csproj -t:Run -f net8.0-windows10.0.19041.0 -c Debug -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=None
```

## Ejecutar en macOS

Requiere el SDK de .NET 8, el workload `maui-maccatalyst` y Xcode completo.
Desde la carpeta del proyecto:

```bash
bash RUN.sh
```

El script selecciona Xcode instalado, compila para la arquitectura del Mac y abre la aplicación.
Puedes indicar otra instalación de Xcode mediante `DEVELOPER_DIR`.

## Visual Studio (Windows)

Abre `HeladeriaPOS.Maui.sln`. Visual Studio debe tener instalado el workload **.NET Multi-platform App UI development**.

## Base de datos

MAUI guarda `pos.db` dentro de `FileSystem.AppDataDirectory`. Se crea automáticamente en el primer inicio.

## Release

```powershell
.\BUILD_RELEASE.ps1
```

La publicación es Windows x64, unpackaged y self-contained.
