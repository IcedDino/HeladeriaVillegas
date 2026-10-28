# Verificación final de la actualización 1.0.2

El usuario volvió a autorizar la publicación y la prueba real el 28 de octubre de 2026. Este resultado sustituye el estado pendiente del documento `update-readiness-2026-10-28.md`.

Release público: https://github.com/IcedDino/HeladeriaVillegas/releases/tag/v1.0.2

Asset: `HeladeriaVillegas-Setup-x64.exe`, 950976 bytes, SHA-256 `df3575cee500f7cd434a8d6ba7cfe7205e66b279894eff9d4545e4563d8a72c6`. El digest y tamaño publicados por GitHub coinciden con el archivo local firmado como Nerqova. El paquete no incluye instaladores de dependencias ni redistribuye los runtimes existentes.

## Prueba real

Se descargó e instaló el asset original de `v1.0.0`, cuyo binario anuncia `1.0.1.0`. Su SHA-256 es `8f1e0a335e675cab597370f4016d528de4ccf6e382b238553ad44baaa502aa4a`.

La inspección del IL confirmó que `WindowsUpdatePlatform.IsInstalled` exige `unins000.exe`, mientras que el instalador NSIS crea `uninstall.exe`. Con la instalación original sin modificar y el Release nuevo público, no hubo descarga pendiente en 35 segundos. La condición del binario desactiva el actualizador antes de consultar GitHub.

Para una segunda prueba se hizo una intervención local explícita: con la caja cerrada se copió el desinstalador real al nombre `unins000.exe`. No se modificaron los binarios ni el auxiliar del actualizador original.

Después de esa reparación, el actualizador original descargó el EXE público, verificó tamaño y SHA-256, guardó el estado pendiente y realizó su respaldo. Al siguiente inicio, el auxiliar original ejecutó el instalador con `/VERYSILENT /DIR=...`; terminó con código 0 y reabrió la caja en 1.0.2.0. Los registros confirmaron carga de datos, `model-ready` y apertura de la caja. Se retiró el identificador temporal; la versión nueva reconoce el `uninstall.exe` real.

**No fue una actualización automática de una instalación original sin intervenir.** Los usuarios de ese paquete original deben ejecutar una vez el instalador correctivo, sin desinstalar. El instalador actualiza la aplicación existente, conserva su carpeta y la misma clave de registro de desinstalación; no crea otra aplicación. Publicar un Release no puede corregir un actualizador instalado que no consulta GitHub.

## Datos y compatibilidad

- Se mantiene la identidad `Nerqova/com.cesardonnet.heladeriapos` y su directorio de datos.
- La comparación de la base realmente instalada contra el respaldo anterior conservó todas las filas y valores originales; `PRAGMA integrity_check` devolvió `ok`.
- La suite también conservó una base de ejemplo con productos personalizados, una categoría, ventas pagadas y canceladas, detalles, modificadores, precios y sabores después de dos inicializaciones.
- Los 592 archivos reutilizados permanecen idénticos. El payload más la instalación original reconstruye los 597 archivos del publish completo, byte por byte.
- El instalador real pasó sobre copias de la instalación original usando argumentos NSIS y los argumentos Inno del auxiliar antiguo.
- El preflight comprobó componentes válidos y respaldo verificado; rechazó una caja abierta y un runtime alterado antes de reemplazar archivos.
- Pasaron las verificaciones de lógica, migraciones, ventas, respaldos y auxiliar. El protocolo original también se comprobó contra el Release público y sus bytes reales.

El paquete ligero aborta si faltan componentes o sus hashes difieren del baseline. No sirve para instalaciones nuevas. Estas pruebas corresponden al entorno comprobado y no garantizan todos los dispositivos, políticas de Windows ni condiciones de red.

Los informes locales están en `artifacts/installed-update-test.json`, `artifacts/installed-update-test.log`, `artifacts/installed-update-helper.log` y `artifacts/installer-smoke.log`. Los datos y los registros del usuario no se publicaron.
