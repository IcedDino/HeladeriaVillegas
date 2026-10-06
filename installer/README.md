# Instalador para Windows x64

`artifacts/installer/HeladeriaVillegas-Setup-x64.exe` instala la aplicación y crea accesos directos en el escritorio y menú Inicio. Incluye .NET y Windows App SDK en la carpeta de la app, además de los instaladores sin conexión de Visual C++ x64 y WebView2. Se instalan los componentes que faltan.

Requiere Windows 10 versión 1809 o superior, o Windows 11, de 64 bits. Se recomienda instalar para todos los usuarios con permisos de administrador. Incluye desinstalador; los datos y respaldos de la app permanecen en la carpeta de datos del usuario.

## Regenerar

Ejecutar `installer/build-installer.ps1 -Version 1.0.1`. Admite `-DotnetPath` y `-IsccPath` si las herramientas no están en PATH. La versión es obligatoria y debe usar `major.minor.patch`; se aplica tanto al ejecutable como al instalador. Las dependencias descargadas se guardan en `artifacts/prerequisites` y se valida su firma de Microsoft antes de empaquetarlas.

## Publicar actualizaciones

1. Generar un instalador con una versión superior a la instalada, por ejemplo `1.0.1`.
2. Crear un Release estable en `IcedDino/HeladeriaVillegas`, con tag `v1.0.1` y marcarlo como la última versión. El repositorio y el Release deben ser accesibles públicamente; no se incluye un token en la app.
3. Adjuntar `artifacts/installer/HeladeriaVillegas-Setup-x64.exe` sin cambiar su nombre. Los archivos de código fuente que GitHub agrega automáticamente no son instaladores.
4. Verificar que la API de GitHub informe `digest` con formato `sha256:...` para ese archivo. La app no ejecutará assets sin digest válido o cuyo tamaño o hash no coincidan.

La primera versión que contiene el actualizador debe instalarse manualmente en la máquina. A partir de ella, la app descargará las siguientes versiones mientras esté abierta y las aplicará automáticamente al siguiente inicio. No interrumpe una venta ni reinicia Windows. El arranque pide elevación de administrador mediante UAC; el auxiliar hereda esos permisos. Las actualizaciones conservan los datos del usuario y requieren respaldo verificado antes de reemplazar los binarios.

El auxiliar `apply-update.ps1` se incluye junto al ejecutable y se copia a `%ProgramData%\HeladeriaVillegasUpdates` antes de cerrar la app para poder sobrevivir al reemplazo. Esa carpeta permite acceso solo a Administradores elevados y SYSTEM; se rechazan carpetas existentes inseguras y enlaces de redirección. Si no puede protegerse, la caja abre y desactiva las actualizaciones dejando el error en el registro de arranque. La app espera una señal de que el auxiliar está listo antes de cerrarse. Su ejecución usa Windows PowerShell con una política limitada a ese proceso, sin modificar la configuración de Windows. Si una política de la organización bloquea PowerShell, la caja permanece abierta; consultar los registros sin desactivar políticas del equipo.

Para comprobar la lógica del auxiliar sin instalar nada: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/verify-updater.ps1`. El proyecto `Verification` comprueba consulta de Releases, versiones, descargas, digest, respaldo fallido y reintentos usando respuestas HTTP simuladas.

Fuentes oficiales:

- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist
- https://jrsoftware.org/isdl-old.php

Los paquetes descargados y el EXE generado quedan en `artifacts`, excluido de Git. El instalador de la aplicación no tiene firma digital propia.
