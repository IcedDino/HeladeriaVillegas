# Revisión de rendimiento

Se revisaron arranque, splash WebGL, catálogo, configurador, persistencia del ticket, órdenes en espera e historial de ventas. Se conservan estilos, imágenes, transiciones, animaciones y calidad del render.

## Hallazgos y cambios

| Operación | Trabajo detectado | Optimización |
| --- | --- | --- |
| Agregar, quitar o cancelar productos; cambiar descuento | CalculateTotal guarda el borrador y el llamador lo guardaba otra vez | Un solo guardado por acción |
| Empezar una orden | Los cambios de las propiedades de pago provocaban guardados de estados intermedios | Suspender persistencia durante el reinicio y guardar el estado final |
| Guardar borrador | JSON con espacios y valores calculados; escritura incluso si no cambió | JSON compacto sin propiedades calculadas; evitar reescribir estados idénticos; conservar escritura temporal y reemplazo del archivo |
| Arranque | Inicialización SQLite y copia/verificación del respaldo diario podían ejecutarse en el hilo de la interfaz | Ejecutar el trabajo en segundo plano y esperar su terminación; actualizar la interfaz al regresar |
| Splash | Sobrescribir seis recursos, unos 1.6 MB, en cada inicio | Comparar SHA-256 del recurso empaquetado y copiar únicamente si cambia o falta; también detecta cambios de scripts sin cambio de versión de la aplicación |
| Abrir configurador de snacks | Consultar sabores y construir botones que nunca se muestran | Omitir la consulta y los botones en esa categoría |
| Ventas: Hoy | Cambiar fecha disparaba la carga y el botón repetía la misma consulta | Una sola carga; conserva actualización al pulsar Hoy en la fecha actual |

## Medición reproducible

Prueba aislada: 500 guardados consecutivos de un borrador idéntico con un producto, modificador, sabor, descuento y pago mixto, dentro de una carpeta temporal. Misma máquina y configuración Debug. No incluye la interfaz ni equivale a una medición de ventas por segundo.

| Métrica | Antes | Después |
| --- | ---: | ---: |
| Tiempo de 500 guardados | 311.36 ms | 18.31 ms |
| Bytes asignados en el hilo durante la prueba | 1,609,368 | 973,160 |
| Tamaño del JSON | 778 bytes | 422 bytes |
| Reescrituras para 500 estados idénticos ya guardados | 500 | 0 |

El tiempo mejora aproximadamente 94%, las asignaciones disminuyen 40% y el archivo ocupa 46% menos en esta prueba. El coste de disco varía según el equipo, antivirus y almacenamiento.

Para repetir:

```powershell
& "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" build Verification/Verification.csproj -p:UseAppHost=false
& "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" Verification/bin/Debug/net8.0/Verification.dll --performance
```

Las comprobaciones cubren persistencia inmediata cuando cambia la cantidad, compatibilidad con borradores antiguos, ausencia de escrituras repetidas, recuperación después de poner en espera, importes, cancelaciones y respaldo/restauración.

## Límites y trabajo conservado

El splash ya cancela requestAnimationFrame y libera geometrías, texturas y materiales al salir. Se conserva su resolución y efectos. El historial ya usa consultas sin seguimiento e índice por fecha. Se conserva la consulta reciente de sabores en helados y especialidades para reflejar su disponibilidad.

Los borradores siguen guardándose inmediatamente cuando cambia la orden: no se introduce una espera que pueda perder la última venta ante un cierre. Un ticket muy grande todavía puede causar trabajo de serialización y disco en la interfaz, aunque se redujo el trabajo duplicado. La lista completa de ventas de un día y la reconstrucción del configurador son candidatos a medir con volúmenes reales antes de introducir paginación o reutilización de controles.

No se midieron FPS, uso de GPU ni picos de memoria del proceso MAUI; no se afirma una reducción de estas métricas. Las cifras corresponden únicamente a la prueba del borrador y las restantes mejoras se sustentan en operaciones eliminadas y revisión del código.
