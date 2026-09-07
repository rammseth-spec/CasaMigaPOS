# Casa Miga POS

Aplicación Windows offline para una panadería mexicana. FASE 1C aprobada; FASE 1D implementa POS y carrito temporal, además de identidad, caja, cierre auditado, catálogo y respaldo completo. Todavía no está lista para producción.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte FASE 1D](docs/PHASE_1D_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: 120 casos de dinero, persistencia, identidad, autorización, concurrencia, carrito, diagnóstico y backup.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

No hay ventas, pagos, inventario ni hardware. La identidad visual es temporal y está identificada como tal. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart is transient and not a sale. Conserva nombre/precio al agregar; salir del usuario, cerrar caja o cerrar normalmente la aplicación exige confirmar descarte si hay partidas. Un reinicio comienza vacío. Sin cobro ni persistencia de ventas. Demo explícita sólo desarrollo. Esperar autorización antes de 1E.
