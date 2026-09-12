# Casa Miga POS

Aplicación Windows offline para una panadería mexicana. **FASE 1I abierta con 1I.1–1I.2 completadas**: FASE 1H permanece cerrada y se añadió el modelo puro de política/plan para consumo futuro por venta. Checkout todavía no descuenta inventario; schema 11 prepara persistencia interna de `SALE_CONSUMPTION`, evaluaciones y snapshots; feature flag DISABLED. Verificación vigente: 325 pruebas aprobadas, ninguna fallida u omitida; Release sin errores ni advertencias. Versión de desarrollo, no lista para producción.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte 1I.2](docs/PHASE_1I2_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: dinero, cantidades físicas, servicios y consultas de inventario, UI/ViewModel, ledger, persistencia, identidad, autorización, concurrencia, carrito, venta, historial, tickets, recuperación, diagnóstico y backup. Suite vigente: 325 pruebas.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

Venta y pago en efectivo se confirman juntos. El efecto de impresión siempre ocurre después del commit. El inventario dispone de casos de uso Application expuestos mediante la UI 1H.4 aprobada; no se descuenta al vender. La Qian quedó validada para el baseline de impresión del Bloque D; otras capacidades físicas permanecen fuera de alcance o pendientes. La identidad visual es temporal y está identificada como tal. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Cobrar exige además sales.complete. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart sigue siendo transitorio y no es una venta. Conserva nombre/precio al agregar; sólo el commit comercial permite vaciarlo después de cobrar. Un resultado incierto exige verificación por UUID antes de volver a operar. Demo explícita sólo desarrollo.

Historial: `sales.view` para Administrator y Manager; Cashier no tiene acceso. Lista hasta 50 ventas por página, orden estable descendente, folio exacto y rango de días locales. El detalle conserva snapshots de producto/precio y muestra nombres actuales de usuario/terminal junto con sus IDs históricos. La consulta usa una conexión SQLite read-only y transacción deferred delimitada, sin cambiar la transacción inmediata de las operaciones comerciales.
