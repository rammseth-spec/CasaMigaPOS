# Casa Miga POS

Aplicación Windows offline para una panadería mexicana. **FASE 1I CERRADA; FASE 1J ABIERTA únicamente con Domain 1J.1**: Checkout integra venta e inventario en un único commit; schema 12 distingue ventas legacy y ventas con evaluación obligatoria. Flag inicial DISABLED; activación segura y preflight 1I.4 implementados, con smoke humano completado. Versión de desarrollo, no lista para producción. Ver [reporte 1J.1](docs/PHASE_1J1_REPORT.md) para continuidad y evidencia. Fórmulas y planificación puras, sin producción persistente ni UI; 1J.2 no iniciada.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte 1I.5](docs/PHASE_1I5_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: dinero, cantidades físicas, servicios y consultas de inventario, UI/ViewModel, ledger, persistencia, identidad, autorización, concurrencia, carrito, venta, historial, tickets, recuperación, diagnóstico y backup. Resultados vigentes: 435 pruebas aprobadas; ver reporte 1J.1.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

Venta y pago en efectivo se confirman juntos. El efecto de impresión siempre ocurre después del commit. El inventario dispone de casos de uso Application expuestos mediante la UI 1H.4 aprobada; el consumo al vender sólo ocurre con el flag ENABLED; existe flujo administrativo de activación, pausa y reanudación con preflight autoritativo. La Qian quedó validada para el baseline de impresión del Bloque D; otras capacidades físicas permanecen fuera de alcance o pendientes. La identidad visual es temporal y está identificada como tal. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Cobrar exige además sales.complete. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart sigue siendo transitorio y no es una venta. Conserva nombre/precio al agregar; sólo el commit comercial permite vaciarlo después de cobrar. Un resultado incierto exige verificación por UUID antes de volver a operar. Demo explícita sólo desarrollo.

Historial: `sales.view` para Administrator y Manager; Cashier no tiene acceso. Lista hasta 50 ventas por página, orden estable descendente, folio exacto y rango de días locales. El detalle conserva snapshots de producto/precio y muestra nombres actuales de usuario/terminal junto con sus IDs históricos. La consulta usa una conexión SQLite read-only y transacción deferred delimitada, sin cambiar la transacción inmediata de las operaciones comerciales.
