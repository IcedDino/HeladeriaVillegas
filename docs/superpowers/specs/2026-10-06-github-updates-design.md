# Actualizaciones automáticas desde GitHub

## Objetivo

La aplicación Windows de Heladería Villegas se utiliza en una máquina por un usuario. Debe detectar nuevas versiones publicadas en Releases de `IcedDino/HeladeriaVillegas` y descargarlas e instalarlas automáticamente cuando exista conexión. Sin conexión debe permitir vender normalmente. El usuario eligió ejecutar la app con permisos de administrador.

## Ejecución y distribución

Cambiar el manifiesto Windows a `requireAdministrator`. Windows solicitará elevación al abrir la aplicación. El instalador continuará usando su AppId y la ubicación instalada existente. No se desactiva UAC. Las actualizaciones se habilitan para instalaciones hechas con el instalador; las ejecuciones de desarrollo no deben iniciar instalaciones.

Mantener una única versión numérica `major.minor.patch` para el ejecutable, el instalador y el tag de Release (`vmajor.minor.patch`). Cada Release debe incluir `HeladeriaVillegas-Setup-x64.exe`. El proceso de empaquetado debe recibir la versión explícitamente para evitar publicar instaladores que todavía anuncien 1.0.0.

## Detección y descarga

Consultar la API HTTPS de GitHub al iniciar, cada hora y al recuperar conexión, sin bloquear la interfaz. Usar un HttpClient independiente del cliente Openverse, con User-Agent, timeout y cancelación. Consultar releases estables; omitir borradores y prereleases. Comparar versiones numéricas, evitando reinstalaciones y downgrades. Evitar comprobaciones concurrentes y limitar reintentos ante errores o límites de API.

Descargar exclusivamente el instalador esperado del repositorio configurado. Escribir primero un archivo temporal y validar tamaño y SHA-256 contra el digest publicado por GitHub antes de marcarlo como pendiente. Si no hay digest válido, no ejecutar el instalador. Una descarga incompleta nunca se ejecuta. Guardar los ejecutables, metadatos y auxiliar en `%ProgramData%\HeladeriaVillegasUpdates`, fuera del directorio de instalación, con propietario y permisos exclusivos de Administradores elevados y SYSTEM. Rechazar carpetas existentes inseguras y puntos de redirección; si no puede protegerse el almacenamiento, desactivar las actualizaciones y abrir la caja normalmente.

## Instalación

Una actualización descargada durante el uso queda pendiente hasta el siguiente inicio. Al abrir, antes de cargar la caja, verificar otra vez el archivo y ejecutar el instalador con permisos heredados, sin asistente y sin reiniciar Windows. No forzar el cierre de otras instancias; impedir la actualización si hay otra caja abierta.

Usar un proceso auxiliar para esperar a que la app termine, ejecutar el instalador, registrar el resultado y reabrir el ejecutable desde su ubicación original. La app solo termina después de recibir una señal de que el auxiliar validó su configuración y puede esperar el cierre; si no llega en 15 segundos, conservar la caja abierta. No iniciar otra instancia de la caja mientras se reemplazan archivos: usar un mutex durante la vida de la instalación en la sesión de Windows. Aplicar cualquier restauración pendiente únicamente después de adquirir ese mutex. Si el instalador falla, conservar registro y reabrir la versión disponible; evitar ciclos de instalación repetidos registrando el intento fallido. El siguiente reintento debe respetar un intervalo definido de una hora.

Las ventas, los precios, los tickets guardados y los respaldos permanecen en la carpeta de datos. El actualizador no elimina datos. Antes de instalar, generar un respaldo verificado; si no es posible, aplazar la instalación y permitir abrir la caja.

## Componentes

- Servicio independiente para consultar Releases, comparar versiones y descargar/verificar el instalador.
- Coordinador Windows para conexión, periodicidad, estado pendiente y lanzamiento del proceso auxiliar.
- Integración con el arranque y el respaldo existente.
- Ajustes de versión en proyecto, script de empaquetado e instalador, y documentación para publicar Releases compatibles.

## Verificación

Probar versiones nuevas, iguales, antiguas y tags inválidos; ausencia de instalador; pérdida de conexión; respuestas de error y límites de API; descargas parciales y digest incorrecto; recuperación de estado pendiente; exclusión mutua de comprobaciones y fallos del instalador. Compilar el proyecto Windows y ejecutar las verificaciones existentes. Verificar elevación, reemplazo y reapertura con el instalador en Windows; cualquier comprobación que no pueda realizarse debe quedar indicada al entregar el cambio.
