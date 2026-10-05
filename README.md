# Heladería Villegas POS

Punto de venta táctil para Windows con .NET MAUI 8, SQLite y funcionamiento local.

## Uso en caja

- Selecciona categoría y producto. El configurador muestra el precio final antes de agregarlo.
- En helados y especialidades, los sabores son opcionales. Puedes agregar y cobrar sin elegir sabor; si lo eliges, se guarda en el ticket.
- Ajusta cantidades con `+` y `−`. El ticket en curso se guarda automáticamente y se recupera al abrir la aplicación.
- `Poner orden en espera` guarda el ticket actual y deja una nueva orden vacía para atender otra. `En espera` abre la lista para recuperar las órdenes guardadas.
- `Cancelar` pide confirmación; `Deshacer cancelación` restaura la última orden vaciada mientras la aplicación siga abierta.
- Elige efectivo, tarjeta, transferencia o pago mixto. En efectivo se calcula el cambio. Introduce el importe del descuento en el campo `Descuento` o con su botón `Editar`, y escribe el motivo. El total se actualiza al cambiar el importe. Los descuentos no pueden cubrir todo el subtotal.
- En el cobro, toca la imagen de cada billete ($20, $50, $100, $200, $500 o $1,000) para sumarlo al efectivo recibido. Puedes tocar varias veces, introducir un importe con el teclado o limpiar el efectivo. `Efectivo exacto` cubre el saldo pendiente, descontando tarjeta y transferencia en un pago mixto.
- `Ventas` muestra historial diario, corte por método, cancelación con motivo y reimpresión.
- `Precios` permite cambiar precios y marcar productos o sabores como no disponibles. Los cambios persisten después de reiniciar.
- Los respaldos SQLite se crean y verifican automáticamente cada 12 horas mientras la app está abierta. Al iniciar se hace uno si no hay copia o ya venció el intervalo; los fallos se reintentan cada minuto. Solo las copias que pasan la verificación cuentan como respaldo completado.

## Datos y respaldos

La base `pos.db`, las órdenes en espera, las imágenes y los respaldos se guardan en `FileSystem.AppDataDirectory` de MAUI. Las copias automáticas quedan en `Backups`. Copia periódicamente esa carpeta a una USB u otro equipo; las copias en el mismo disco no protegen de una falla física.

Al restaurar, cierra y abre de nuevo la aplicación. Revisa el historial antes de volver a vender.

Los comprobantes de texto se guardan en `Receipts`. El botón `Imprimir` usa el controlador de impresión predeterminado de Windows para archivos `.txt`; configura una impresora predeterminada que admita ese formato antes de usarlo en caja.

## Preparación del equipo Windows

1. Instala el SDK .NET 8.0.425 y el workload `maui-windows` para compilar.
2. Ejecuta `RUN.bat` o `RUN.ps1` para desarrollo.
3. Ejecuta `BUILD_RELEASE.ps1` para publicar en `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish`.
4. En el equipo donde se use la publicación, instala **Windows App Runtime 1.4 x64**. La publicación incluye .NET, pero usa el runtime de Windows App SDK instalado en el equipo.

La ventana inicia maximizada y admite 900 × 680 como mínimo. Verifica el diseño en la resolución y escala de Windows del equipo táctil definitivo.

## Verificación

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project .\Verification\Verification.csproj
```

La comprobación cubre creación y actualización de la base, precios persistentes, disponibilidad, cálculo de venta, cancelación, borradores y respaldo/restauración.
