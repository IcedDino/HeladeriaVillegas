# Actualización ligera de la primera versión publicada

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/build-light-update.ps1 -Version 1.0.2 -BaselineDirectory artifacts/update-baseline/installed
```

El baseline debe ser una copia completa y sin modificar de la instalación creada por el asset original de `v1.0.0` (su binario anuncia `1.0.1`). El script publica en una carpeta nueva, firma la aplicación y el instalador con el certificado Nerqova existente y empaqueta solamente archivos diferentes. No incluye instaladores de dependencias ni runtimes: conserva los componentes presentes en el equipo. Si cambia una DLL de dependencia, la construcción se detiene y exige un paquete completo.

El instalador comprueba el SHA-256 de cada archivo que reutilizará antes de modificar la instalación; exige cerrar la caja y respalda los datos del usuario, verificando cada archivo copiado. Si falta un componente, cambió o el respaldo falla, aborta. El diagnóstico queda en `%TEMP%\HeladeriaVillegas-update-check.log`. No sirve para instalaciones nuevas.

Mantiene la carpeta de instalación y la identidad de datos `Nerqova/com.cesardonnet.heladeriapos`. Acepta tanto `/S /D=...` de NSIS como `/VERYSILENT /DIR="..."` del auxiliar Inno original.

El asset original publicado tiene un fallo al reconocer su instalación NSIS. Esas instalaciones requieren ejecutar el paquete correctivo una vez, conservando datos y sin desinstalar. Publicar un Release no corrige el código que ya está instalado. Las pruebas HTTP con el código del tag original comprueban el protocolo, pero no reemplazan la prueba real del binario publicado.

`Verification --preserve-db ruta/pos.db` prueba dos inicializaciones sobre una copia SQLite consistente de una base existente, verifica todos los valores originales de cada fila y ejecuta `PRAGMA integrity_check`. Nunca modifica la base de origen. La suite también realiza esta prueba con productos personalizados, una categoría, ventas pagadas y canceladas, detalles y modificadores.

`Verification --compare-db antes.db despues.db` compara todas las filas y columnas originales contra la base realmente instalada y comprueba su integridad, abriendo ambas en modo de solo lectura.

La prueba real está documentada en [update-verification-2026-10-28.md](../docs/update-verification-2026-10-28.md). `verify-installed-update.ps1` requiere elevación y una caja cerrada. Su opción `-RepairLegacyMarker` realiza una intervención local explícita después de comprobar el fallo original; no debe presentarse como una actualización automática sin intervención.
