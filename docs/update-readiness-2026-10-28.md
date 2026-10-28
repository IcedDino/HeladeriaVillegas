# Estado de la actualización 1.0.2

La publicación está detenida por indicación del usuario. No adjuntar instaladores ni publicar el borrador de Release hasta recibir una nueva instrucción.

## Versión anterior comprobada

Se descargó e inspeccionó el instalador real adjunto al Release `v1.0.0`, sin ejecutarlo. Su SHA-256 es `8f1e0a335e675cab597370f4016d528de4ccf6e382b238553ad44baaa502aa4a`.

- El instalador es NSIS y anuncia `1.0.1`.
- La DLL extraída anuncia `1.0.1.0`, con editor `Nerqova`.
- El paquete incluye la corrección `WEBVIEW2_USER_DATA_FOLDER`.
- El actualizador incluido busca `unins000.exe`, pero ese instalador crea `uninstall.exe`. Por ello, una instalación hecha con ese paquete puede no activar las actualizaciones automáticas. Un Release nuevo no modifica el actualizador ya instalado; se requiere una instalación correctiva una vez.
- El código del tag `v1.0.0` y el binario publicado no corresponden exactamente a la misma revisión. Las pruebas con el código del tag no demuestran por sí solas que la instalación NSIS publicada esté buscando actualizaciones.

La próxima versión se establece en `1.0.2` para superar la versión real instalada.

## Conservación de datos y WebView2

Se mantiene `PublisherDisplayName=Nerqova` y `ApplicationId=com.cesardonnet.heladeriapos`. En Windows sin MSIX esto conserva la ubicación de datos del binario publicado: `%APPDATA%\Nerqova\com.cesardonnet.heladeriapos\Data`.

La configuración de WebView2 permanece antes de crear las ventanas: perfil en `Data\WebView2` y archivos de animación en `Data\Splash`, fuera de Program Files. No sustituir esta configuración al empaquetar.

Se encontraron también carpetas de versiones históricas con otro editor. No se movieron, borraron ni mezclaron sus bases. Sigue pendiente ampliar las pruebas de conservación sobre copias con ventas y productos registrados antes de declarar listo el paquete.

## Verificaciones y pendientes

Las pruebas de lógica, migraciones existentes, respaldo y auxiliar de instalación pasaron. Se agregó una verificación reproducible usando el código del tag original y los bytes del instalador real; simula HTTP y el lanzamiento del instalador, sin reemplazar la app instalada.

El usuario pidió que el próximo paquete sea ligero: solo actualización, sin incluir instaladores de .NET, Visual C++ o WebView2 ni redistribuir los runtimes ya presentes. La construcción de ese paquete sigue pendiente. El instalador 1.0.1 generado anteriormente contiene dependencias y no es el artefacto final que debe publicarse.

Antes de publicar: completar paquete ligero 1.0.2, comprobar conservación de datos y arranque de la animación, revisar la transición desde el instalador NSIS anterior y verificar el SHA-256 del asset. Mantener la publicación detenida mientras no se autorice.
