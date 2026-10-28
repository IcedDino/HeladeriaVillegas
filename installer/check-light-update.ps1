param([Parameter(Mandatory=$true)][string]$InstallDirectory,
      [Parameter(Mandatory=$true)][string]$ManifestPath,
      [Parameter(Mandatory=$true)][string]$LogPath)
$ErrorActionPreference = 'Stop'
try {
    $directory = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
    if (!(Test-Path -LiteralPath (Join-Path $directory 'HeladeriaPOS.Maui.exe'))) { throw 'No existe una instalacion anterior. Este paquete solo actualiza.' }
    foreach ($process in @(Get-Process -Name HeladeriaPOS.Maui -ErrorAction SilentlyContinue)) {
        if (!$process.Path -or $process.Path -eq (Join-Path $directory 'HeladeriaPOS.Maui.exe')) { throw 'Cierra la caja antes de actualizar. No se modifico la instalacion.' }
    }
    $entries = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    foreach ($entry in $entries) {
        $path = [IO.Path]::GetFullPath((Join-Path $directory $entry.Path))
        if (!$path.StartsWith($directory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta de dependencia invalida.' }
        if (!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw ('Componente faltante o diferente: ' + $entry.Path + '. Se requiere el instalador completo; no se modifico la instalacion.')
        }
    }
    # Installer runs only with the POS closed. Preserve the database, WAL and all user assets together.
    $data = Join-Path $env:APPDATA 'Nerqova/com.cesardonnet.heladeriapos/Data'
    if (Test-Path -LiteralPath $data) {
        $backup = Join-Path $data ('Backups/BeforeUpdate-' + (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $data -Force) {
            if ($item.Name -in @('Backups','WebView2','Splash')) { continue }
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se puede respaldar un enlace de datos.' }
            Copy-Item -LiteralPath $item.FullName -Destination $backup -Recurse -Force
            foreach ($source in @(if ($item.PSIsContainer) { Get-ChildItem -LiteralPath $item.FullName -Recurse -File } else { $item })) {
                $relative = $source.FullName.Substring($data.Length + 1)
                if ((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $backup $relative)).Hash) { throw 'El respaldo no coincide con los datos originales.' }
            }
        }
    }
    'OK: componentes instalados verificados; respaldo realizado; datos originales sin modificar.' | Set-Content -LiteralPath $LogPath
    exit 0
} catch {
    $_.Exception.Message | Set-Content -LiteralPath $LogPath
    exit 1
}
