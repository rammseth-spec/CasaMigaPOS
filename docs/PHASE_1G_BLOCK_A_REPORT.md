# FASE 1G — Reporte Bloque A

Fecha: 2026-09-11. Estado: **1G.1–1G.4 implementadas y verificadas; FASE 1G abierta en checkpoint físico**.

Commit de implementación: `4f6463e` — receipt foundation, migración 008, trazabilidad, renderer, Winspool RAW, integración post-commit y pruebas.

Se añadió `ReceiptDocument` inmutable, independiente del hardware y construido desde una venta confirmada. Conserva folio, hora local/offset, IDs y nombres capturados, partidas históricas, Money, pago CASH y perfil comercial. Su JSON canónico se versiona, recibe SHA-256 y se persiste una vez por venta.

`TextReceiptRenderer` produce salida determinista de 48 columnas, envuelve nombres, alinea importes MXN y marca reintentos/reimpresiones. `EscPosReceiptRenderer` sólo codifica ese texto. Bloque A no contiene comandos de selección de página, corte ni cajón; una prueba rechaza la presencia de bytes ESC y GS.

La migración 008 crea receipt_snapshot, receipt_print_request, receipt_print_event y `sales.receipt.reprint`, asignado sólo a Administrator y Manager. Las tres entidades son inmutables/append-only. ORIGINAL es único por venta y los reintentos conservan su relación. Upgrade 007→008 preserva ventas; base nueva y backup/restore se prueban.

`WindowsRawPrintTransport` usa la secuencia Winspool documentada y devuelve JobId. Los trabajos se serializan. Fallas anteriores a StartDocPrinter son FAILED; después de iniciar el trabajo son UNKNOWN. El envío sucede fuera de transacción SQLite y fuera del hilo WPF. SUBMITTED no afirma impresión física.

COMPLETED queda reservado en el esquema para una observación futura de la cola; Bloque A termina en SUBMITTED y no monitorea activamente el trabajo después de aceptarlo.

Después del commit comercial, Checkout intenta el ticket. La falta o invalidez de `printing.json` registra el fallo y muestra que la venta permanece confirmada. FAILED, REQUESTED y UNKNOWN permiten un reintento. No se tocó la transacción de Checkout ni la fórmula de ExpectedCash.

`printing.json` es local, opcional y no forma parte de `.cmbackup`. El ejemplo permanece deshabilitado, con datos sintéticos identificados. La aplicación arranca, vende y consulta sin impresora.

Verificación focalizada ampliada: 59 pruebas relevantes aprobadas; conjunto específico Receipt: 12 aprobadas. Verificación completa mediante `scripts/verify.ps1`: 182 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Backup/restore con snapshot, request y dos eventos fue validado por integración automatizada.

Pendientes: hardware real, driver, nombre de cola, codepage efectiva, impresión desde el POS, estado físico, corte parcial, logo aprobado, datos reales del negocio, reimpresión desde Historial, LAN y cajón. Consultar `PHASE_1G_PHYSICAL_CHECKPOINT.md`.
