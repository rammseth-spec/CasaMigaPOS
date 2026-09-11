# Casa Miga POS

Aplicación Windows offline para una panadería mexicana. FASE 1D aprobada y registrada en Git; **FASE 1E cerrada** con venta persistida, cobro en efectivo y smoke WPF completo. Continúa siendo una versión de desarrollo y todavía no está lista para producción.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte FASE 1E](docs/PHASE_1E_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: dinero, persistencia, identidad, autorización, concurrencia, carrito, venta, recuperación, diagnóstico y backup. Conserva las 120 pruebas del baseline, con expectativas de migraciones/permisos actualizadas.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

Venta y pago en efectivo se confirman juntos. No hay inventario ni hardware. La identidad visual es temporal y está identificada como tal. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Cobrar exige además sales.complete. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart sigue siendo transitorio y no es una venta. Conserva nombre/precio al agregar; sólo el commit comercial permite vaciarlo después de cobrar. Un resultado incierto exige verificación por UUID antes de volver a operar. Demo explícita sólo desarrollo.
