# Instalador para Windows x64

`artifacts/installer/HeladeriaVillegas-Setup-x64.exe` se genera con NSIS, un empaquetador gratuito. Instala la aplicación y crea accesos directos en el menú Inicio y, si se selecciona, en el escritorio. Incluye .NET y Windows App SDK en la carpeta de la app, además de los instaladores sin conexión de Visual C++ x64 y WebView2. Se instalan los componentes que faltan. El instalador y el ejecutable se firman como Nerqova; la ejecución inicial desde la pantalla final usa el token elevado del instalador y queda seleccionada de forma predeterminada.

Requiere Windows 10 versión 1809 o superior, o Windows 11, de 64 bits. Se recomienda instalar para todos los usuarios con permisos de administrador. Incluye desinstalador; los datos y respaldos de la app permanecen en la carpeta de datos del usuario.

## Regenerar

Ejecutar `installer/build-installer.ps1 -Version 1.0.0`. Admite `-DotnetPath`, `-NsisPath` y `-SigningCertThumbprint` si las herramientas o el certificado están en ubicaciones distintas. La versión es obligatoria y debe usar `major.minor.patch`; se aplica al ejecutable y al instalador. Las dependencias descargadas se guardan en `artifacts/prerequisites` y se valida su firma de Microsoft antes de empaquetarlas. Se requiere un certificado con clave privada y sujeto `CN=Nerqova` en el almacén Personal del usuario actual.

## Actualizar instalaciones 1.0.0

Para la primera actualizacion desde `1.0.0`, usar `installer/build-compatible-update.ps1 -Version 1.0.1`. Este genera un instalador Inno Setup con el mismo AppId, carpeta y nombre de archivo que la version original. El auxiliar de 1.0.0 solo entiende los argumentos de Inno Setup; publicar directamente un instalador NSIS no es compatible. Este proceso no firma los binarios; para distribuirlos firmados se necesita el certificado de firma correspondiente.

La version nueva reconoce instalaciones Inno y NSIS. La seleccion de argumentos se verifica en `verify-updater.ps1`. Mientras existan clientes 1.0.0, los instaladores publicados deben continuar usando Inno Setup. Una migracion futura a NSIS requiere un puente que acepte el protocolo anterior.

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
- https://nsis.sourceforge.io/Download
- https://nsis.sourceforge.io/License
- https://nsis.sourceforge.io/Reference/RequestExecutionLevel

Los paquetes descargados y el EXE generado quedan en `artifacts`, excluido de Git. El certificado actual es autofirmado para uso interno; los equipos externos no confiarán en la firma hasta que se use un certificado de una autoridad reconocida o se instale la confianza corporativa correspondiente. La firma tampoco garantiza que SmartScreen o Microsoft Defender nunca muestren avisos.


