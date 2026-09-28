# Casa Miga POS

**FASE 1K.2C-R1 IMPLEMENTADA; APROBACIÓN VISUAL PENDIENTE.** La corrección convierte los dos mosaicos autorizados en 26 assets técnicos PROVISIONAL de 512×512, compacta el POS para cajero y separa `Cobrar` de `Cobrar + imprimir ticket` sobre el mismo checkout post-commit. Catálogo administrativo muestra una sección evidente de imagen y crop visual arrastrable. Dataset aislado: 26 productos, 26 PROVISIONAL, 0 REAL y 0 sin foto; la instalación comercial no fue poblada. Ver [reporte correctivo](docs/PHASE_1K2C_R1_REPORT.md), [UI fotográfica](docs/product-photo-ui.md) y [mapa provisional](docs/phase1k2c-provisional-map.md). Automatización: 638/638; la aceptación humana final sigue pendiente y FASE 1K permanece abierta.

**FASE 1K.2B COMPLETADA — Product Photo Foundation.** Schema 16: foto opcional por Product, procedencia PROVISIONAL/REAL, originales exactos, derivado JPEG Q85 hasta 512, publicación recuperable, limpieza de retirados y backup v2 compatible con restore v1. Ver [contrato de fotos](docs/product-photos.md) y [reporte 1K.2B](docs/PHASE_1K2B_REPORT.md). 621/621 pruebas (535 regresión + 86 fotos), Release 0 errores/advertencias. Sin UI fotográfica final ni carga masiva; 1K.2C no iniciada. Los estados anteriores siguientes son históricos.

**FASE 1K.1 CERRADA Y APROBADA VISUALMENTE.** El nuevo shell WPF incorpora sidebar por permisos, header operativo, área de contenido común, estilos semánticos, contraste legible, logotipo HD, icono oficial y pantalla completa F11. La identidad usa el maestro oficial **VARIANTE 04 — SONRISA DELGADA** y la paleta documentada; no usa Opción 07 en Presentation. FASE 1K.2 está en diseño: [propuesta 1K.2A](docs/PHASE_1K2A_DESIGN.md), sin implementación ni migración 016. Ver [reporte 1K.1](docs/PHASE_1K1_REPORT.md) y [smoke visual](docs/PHASE_1K1_SMOKE.md).

**FASE 1J CLOSED; 1J.5 completada.** Recetas/perfiles versionados, producción multi-output, Planned/Actual, zero-output, validación de stock, recuperación, permisos y backup/restore verificados. UI funcional con refresco y selector de inventario. Schema 15; migraciones 001–015 intactas, sin 016. 533/533 pruebas; Release 0 errores/advertencias. Ver [reporte final](docs/PHASE_1J5_REPORT.md).

Aplicación Windows offline para una panadería mexicana. Versión de desarrollo, NO lista para producción.

Stack: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest.

## Continuar

1. Leer [estado](docs/CURRENT_STATE.md), [próximos pasos](docs/NEXT_STEPS.md) y [reporte 1J.4](docs/PHASE_1J4_REPORT.md).
2. Instalar SDK .NET 10.0.400 x64 o parche compatible y ejecutar `./scripts/verify.ps1`.
3. Ejecutar `dotnet run --project src/CasaMiga.Presentation`.
4. En una carpeta nueva, crear el primer administrador desde la pantalla inicial; después iniciar sesión y abrir caja con fondo cero o positivo.

El primer restore requiere Internet; la aplicación no. No hay servicios cloud ni telemetría de aplicación. Datos por defecto en `%LOCALAPPDATA%/CasaMigaPOS`; `CASAMIGA_DATA_DIR` acepta una carpeta local absoluta. Para pruebas aisladas, consultar [operación](docs/operations.md). Nunca usar una carpeta compartida o sincronizada.

Al arrancar se respalda, migra y valida la instalación. El paquete .cmbackup incluye base, terminal.json y manifiesto; restauración validada a directorio nuevo. Reiniciar exige login y recupera la caja abierta sin alterar su fondo ni timestamp. Salir del usuario o cerrar la ventana no cierra caja. El cierre requiere confirmar conteo y luego confirmar cierre; volver conserva el conteo. Los importes se ocultan hasta confirmar el conteo ciego.

- `src/`: Domain, Application, Infrastructure, Presentation y utilidad Maintenance.
- `tests/`: dinero, cantidades físicas, recetas/producción, UI/ViewModel, inventario, ledger, persistencia, identidad, autorización, concurrencia, carrito, venta, historial, tickets, recuperación, diagnóstico y backup. Resultados 1J.5: 533/533 pruebas, 0 fallidas/omitidas; Release 0 errores/advertencias de compilación.
- `migrations/`: SQL inmutable versionado, recursos del ensamblado.
- `docs/`: arquitectura, operación, decisiones y evidencia.
- `research/`: investigación de FASE 0; `scripts/`: verificación/publicación de desarrollo.

Venta y pago en efectivo se confirman juntos. El efecto de impresión siempre ocurre después del commit. El inventario dispone de casos de uso Application expuestos mediante la UI 1H.4 aprobada; el consumo al vender sólo ocurre con el flag ENABLED; existe flujo administrativo de activación, pausa y reanudación con preflight autoritativo. La Qian quedó validada para el baseline de impresión del Bloque D; otras capacidades físicas permanecen fuera de alcance o pendientes. La identidad visual de Presentation usa el manual oficial del 14 de septiembre de 2026. Hoja de Aguacate se referencia como `BEREL 405N` y usa la equivalencia digital aproximada `#4A5D4E` autorizada por el usuario. No se ha elegido licencia de distribución ni publicado un remoto.


POS: usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Cobrar exige además sales.complete. Categorías/productos activos, búsqueda nombre/SKU y páginas de 50. Cart sigue siendo transitorio y no es una venta. Conserva nombre/precio al agregar; sólo el commit comercial permite vaciarlo después de cobrar. Un resultado incierto exige verificación por UUID antes de volver a operar. Demo explícita sólo desarrollo.

Historial: `sales.view` para Administrator y Manager; Cashier no tiene acceso. Lista hasta 50 ventas por página, orden estable descendente, folio exacto y rango de días locales. El detalle conserva snapshots de producto/precio y muestra nombres actuales de usuario/terminal junto con sus IDs históricos. La consulta usa una conexión SQLite read-only y transacción deferred delimitada, sin cambiar la transacción inmediata de las operaciones comerciales.
