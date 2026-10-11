# Casa Miga POS 1.0 — notas de la versión

Candidata a 1.0 para el piloto en sucursal ([PILOTO.md](PILOTO.md)). Manual del personal: [MANUAL.md](MANUAL.md).

## Qué incluye

**Caja de escritorio (Windows)**
- Punto de venta con tarjetas y fotos, cobro en efectivo (con cambio), tarjeta y transferencia, ticket térmico y reimpresión.
- Caja: apertura, gastos, entradas y retiros, conteo ciego, cierre con nota de diferencia y cierres anteriores.
- Historial de ventas con folio, días, forma de pago y producto; cancelaciones y devoluciones con instrucción de reembolso.
- Reportes: Resumen, Comparativo, Ventas por hora, Control de caja, Producción y **Ganancia**, con CSV. Incluyen lo vendido en las cajas web de la sucursal.
- Charolas: ruta del día (entregar, recoger con fotos, cobrar, anular), tiendas, precios, reporte, **entregas y cobros**.
- Catálogo con fotos; inventario con consumo automático opcional y **costo de referencia**; recetas con versiones; producción en dos pasos.
- Usuarios con roles (cajero, encargado, administrador) y contraseñas temporales.
- Nube: sincronización cada 5 segundos, cambios hechos en la web, respaldos cifrados cada hora y restauración en una computadora nueva.

**Versión web (celular, tableta o computadora)**
- Las mismas pantallas con el diseño de la caja, incluida la venta y el corte en cajas web con folios propios.
- Cambios de catálogo, usuarios y tiendas visibles al momento, marcados «Por confirmar» hasta que la caja los recibe.
- Aviso para el encargado si una caja tiene la caja abierta y deja de conectarse.

## Versiones de datos

- Caja: migraciones hasta **030** (029 copia de ventas web, 030 índices de reportes).
- Nube: migraciones hasta **009** (008 cambios al momento, 009 índices de reportes).
- Las migraciones se aplican solas al abrir la caja o al arrancar la nube; ninguna borra datos.

## Pruebas

- Caja: 727 pruebas automáticas más 210 de pantallas, incluida la de punta a punta con la nube y el simulacro de respaldo y restauración.
- Web: 73 pruebas de la nube y 11 recorridos en navegador (escritorio y celular).
- Volumen: un año de ventas (109,500) con tiempos por pantalla en [VOLUME.md](VOLUME.md).
- CI en GitHub: la caja (Windows) y la web (Linux), con la prueba fallida anotada en el correo.

## Limitaciones conocidas

- La web necesita Internet; imprime con el navegador y no abre el cajón.
- Las ventas web no descuentan inventario en la caja (el consumo automático está apagado).
- Si una caja rechaza un producto nuevo que ya se vendió en la web, la venta queda registrada con un producto que la caja no tiene.
- El instalador no está firmado digitalmente (Windows puede advertir al instalar).
- En la web, el periodo máximo de reportes es de 92 días; en la caja, 366.
- `@vercel/blob` 1.x depende de versiones de `undici` con avisos de seguridad; se actualizará a 2.x en la siguiente revisión (cambio de versión mayor, requiere probar respaldos).

## Si algo sale mal

- Una caja dañada se restaura desde la nube en una carpeta nueva (`CasaMiga.Maintenance restore-cloud`).
- Mientras tanto se puede vender desde una caja web.
- Ninguna versión borra ventas ni movimientos: los errores se corrigen con cancelaciones, devoluciones o movimientos contrarios.
