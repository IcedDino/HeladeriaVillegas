@echo off
setlocal
set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
if exist "%DOTNET%" goto run
where dotnet >nul 2>&1
if errorlevel 1 (
  echo No se encontro .NET SDK.
  pause
  exit /b 1
)
set "DOTNET=dotnet"
:run
"%DOTNET%" restore HeladeriaPOS.Maui.csproj
if errorlevel 1 goto error
"%DOTNET%" build HeladeriaPOS.Maui.csproj -t:Run -f net8.0-windows10.0.19041.0 -c Debug -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=None
if errorlevel 1 goto error
exit /b 0
:error
pause
exit /b 1
