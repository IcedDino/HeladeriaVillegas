# Verificación del actualizador GitHub

## Resultado

Implementado el flujo de consulta, descarga verificada y aplicación de una actualización pendiente al siguiente inicio. La app Windows solicita elevación mediante su manifiesto. Las ejecuciones de desarrollo no instalan actualizaciones.

## Evidencia

- Proyecto `Verification`: pasan los escenarios de Releases, comparación numérica, versiones iguales/antiguas, tags inválidos, URLs ajenas, digest ausente/incorrecto, archivos parciales, cancelación, estado corrupto, comprobaciones concurrentes, desconexión, timeout, fallo del respaldo, fallo del lanzamiento y reintentos.
- `StartupPreparationChecks`: una segunda instancia rechazada no aplica restauraciones; la restauración ocurre después de adquirir el bloqueo.
- `UpdateStorageChecks`: se rechaza una carpeta existente con permisos inseguros. La creación de una carpeta protegida se omite en este entorno porque el proceso de verificación no está elevado.
- `installer/verify-updater.ps1`: pasan las simulaciones con archivos reales de instalación exitosa, salida fallida del instalador, otra instancia abierta, archivo alterado, configuración corrupta y señal de preparación. Solo se sustituyen operaciones de procesos externos; no se instala en la máquina durante esas pruebas.
- La compilación Windows inicial terminó con cero errores y cero advertencias. La publicación versionada posterior compiló y publicó; NuGet informó `NU1900` porque no pudo consultar las advertencias de vulnerabilidades en `api.nuget.org`. Los paquetes utilizados estaban disponibles localmente.
- Se verificó que el EXE publicado incluye `requireAdministrator`, que la DLL tiene versión `1.0.1.0` y que `apply-update.ps1` acompaña la publicación.
- Inno Setup generó correctamente `artifacts/installer/HeladeriaVillegas-Setup-x64.exe` (versión 1.0.1). SHA-256: `4B388B0BBFF4A96CAFED83F60C0F91A50A4FA552127C16FD412E86A47AFBB845`.
- Se descargó WebView2 completo desde el enlace oficial de Microsoft y se verificó su firma; el archivo incompleto previo se conservó con otro nombre.
- El repositorio es públicamente accesible. La API `releases/latest` devuelve 404: actualmente no hay una última versión estable publicada para consumir.

## Revisión independiente

Se corrigieron los tres hallazgos de la revisión: restauración antes del mutex, cierre de la app sin confirmación del auxiliar y entradas ejecutables en almacenamiento modificable sin elevar. Las correcciones tienen verificaciones de regresión; la prueba de creación de permisos elevados queda pendiente para una ejecución elevada.

## Decisiones durante la ejecución

- Trabajar en la carpeta actual y conservar cambios previos, según elección del usuario. Se dejan los cambios del actualizador sin commit para no incluir cambios ajenos ni guardar una parte que dependa de archivos del usuario sin guardar.
- Usar operaciones PowerShell para el registro de avance, porque los auxiliares del skill asumen Bash. No cambia el producto.
- Bloquear segundos lanzamientos instalados en la misma sesión de Windows para evitar abrir caja mientras se sustituyen los archivos. El costo es que una segunda ventana de caja no puede abrirse.
- Usar almacenamiento protegido en ProgramData para ejecutables, scripts y metadatos; los datos de ventas siguen en AppData. Los registros de actualización requieren acceso elevado.
- Excluir `artifacts` y `.superpowers` de los archivos automáticos del SDK: la compilación estaba incluyendo ejemplos C# de herramientas del instalador y copias de respaldo. El código de la app debe permanecer fuera de esas carpetas generadas.

## Límites y pendiente menor

No se probó una instalación real con UAC ni el reemplazo de dos versiones instaladas en esta máquina. Tampoco se verificaron carreras entre sesiones distintas de Windows ni la recuperación de un instalador que dañe parcialmente el ejecutable instalado. Esas comprobaciones requieren una instalación de prueba y ejecución elevada; las simulaciones no las reemplazan.

El reintento de API usa una hora fija y no interpreta `Retry-After` ni cabeceras de reinicio del límite; podría comprobar antes de lo solicitado por el servidor y recibirá otro error, sin impedir vender.

La primera versión con este actualizador debe instalarse manualmente. Las siguientes deben publicarse como Releases estables con tag `vmajor.minor.patch` y asset `HeladeriaVillegas-Setup-x64.exe` que incluya digest SHA-256 en la API.
