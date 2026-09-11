# Casa Miga POS

Aplicación Windows offline para una panadería mexicana. **FASE 1G abierta; Bloque D cerrado físicamente y Bloque E detenido antes de implementar**: transporte RAW, jerarquía visual, PC850/tabla ESC-POS 2 y corte parcial fueron validados en papel. El repositorio no contiene un logotipo oficial aprobado; se requiere ese asset para continuar. Cajón, LAN y reimpresión UI siguen pendientes. Continúa siendo una versión de desarrollo y todavía no está lista para producción.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte del Bloque A de 1G](docs/PHASE_1G_BLOCK_A_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: dinero, persistencia, identidad, autorización, concurrencia, carrito, venta, historial, tickets, recuperación, diagnóstico y backup. Suite vigente: 188 pruebas.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

Venta y pago en efectivo se confirman juntos. El efecto de impresión siempre ocurre después del commit. No hay inventario. La Qian quedó validada para el baseline de impresión del Bloque D; otras capacidades físicas permanecen fuera de alcance o pendientes. La identidad visual es temporal y está identificada como tal. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Cobrar exige además sales.complete. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart sigue siendo transitorio y no es una venta. Conserva nombre/precio al agregar; sólo el commit comercial permite vaciarlo después de cobrar. Un resultado incierto exige verificación por UUID antes de volver a operar. Demo explícita sólo desarrollo.

Historial: `sales.view` para Administrator y Manager; Cashier no tiene acceso. Lista hasta 50 ventas por página, orden estable descendente, folio exacto y rango de días locales. El detalle conserva snapshots de producto/precio y muestra nombres actuales de usuario/terminal junto con sus IDs históricos. La consulta usa una conexión SQLite read-only y transacción deferred delimitada, sin cambiar la transacción inmediata de las operaciones comerciales.
