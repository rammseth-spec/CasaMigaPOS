# Estado actual — FASE 1D

Fecha: 2026-09-07. FASE 1C aprobada. POS y carrito transitorio implementados; no iniciar 1E sin autorización.

- Baseline previo: clean/restore/build Release y 82 pruebas aprobadas. Sin regresión previa.
- Captura global implementada antes del POS: Dispatcher, AppDomain y tareas no observadas. Diagnóstico mínimo sin mensajes arbitrarios ni secretos; terminación según ADR-009. Fallo Dispatcher controlado probado en WPF publicada y carpeta aislada.
- Cart/CartLine inmutables en memoria, asociados a usuario/terminal/caja; cantidades enteras >=1, Money Int64 checked, snapshot al agregar. Repetidos aumentan una línea; desde 1 se elimina con botón explícito. Precio cero permitido.
- PosService revalida usuario activo, catalog.view, pos.use, caja OPEN y ausencia de conteo confirmado. No se pasa un carrito no vacío a otro usuario/caja. Cierre/conteo/logout exigen descarte también en Application.
- POS separado de administración: categorías activas con productos activos, orden SortOrder/nombre/ID; productos activos con categoría activa, nombre/ID, búsqueda nombre/SKU y páginas de 50. Prueba con 2,002 productos y comprobación de consultas sin N+1.
- Migración 005 sólo agrega pos.use a Administrator, Manager y Cashier. 001–004 intactas; sin nuevas dependencias ni tablas comerciales.
- Validación: 120 pruebas, 0 fallidas/omitidas; Release sin errores ni advertencias de compilación. Publicación autocontenida win-x64. Aviso del runner por reemplazo de TRX existente.
- Smoke real A–H: totales DEMO 38/53/58/53/38/0, rechazo sin caja, descarte al salir/cerrar y reinicio vacío. UI en ventana 1366×768 DIP y monitor 1920×1080 al 125%, maximizada; teclado y foco comprobados. Matices y evidencia en PHASE_1D_REPORT.md.

Cart is transient and not a sale. No se guarda en SQLite ni en .cmbackup. Catálogo y caja se conservan al reiniciar; el carrito se pierde. Cambios de catálogo no reemplazan snapshots agregados. Una venta futura deberá revalidar comerciabilidad bajo política explícita.

## Riesgos y continuidad

Git master sigue sin commits, identidad ni remoto. Snapshot de fuentes aprobadas 1C en work/phase1d-before; no sustituye historial Git. No se inventó autor.

Pantalla táctil física, otras escalas/monitores, recuperación administrativa, retención/segunda copia/cifrado y fallos físicos siguen pendientes. Diagnóstico best effort: no garantiza captura ante terminación del sistema, disco inaccesible o proceso corrupto. No usar para ventas reales. Reporte completo: PHASE_1D_REPORT.md.
