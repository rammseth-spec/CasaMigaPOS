# FASE 1G — BLOQUE F — REIMPRESIÓN DESDE HISTORIAL

Fecha: 2026-09-11. Estado: implementación y verificación técnica completas; pendiente único smoke físico y foto. FASE 1G permanece abierta. Sistema de desarrollo, no listo para producción.

## Decisión E.1

E.1 queda cerrado. Se acepta el margen superior actual; su causa es predominantemente la separación mecánica entre cabezal y cortador. El logo maestro Opción 07 no se recorta y conserva sus ocho filas blancas superiores. No se implementa backfeed, reverse feed ni comandos equivalentes sin documentación específica de Qian.

## Implementación

El detalle de Historial incorpora `REIMPRIMIR TICKET` sólo cuando el usuario actual está activo y conserva `sales.receipt.reprint`. Administrator y Manager lo reciben desde migración 008; Cashier no. Presentation deshabilita la vista durante el trabajo y muestra el resultado de cola, fallo o resultado incierto. Application vuelve a autorizar cada acción.

La reimpresión usa el `receipt_snapshot` persistido. Si una venta anterior a 1G carece de snapshot, se crea desde su venta persistida y se fija una vez. Un snapshot existente no se reconstruye desde catálogo, precios o configuración comercial actual. Cada solicitud válida crea un `receipt_print_request` con `kind=REPRINT`, SaleId, actor, UTC, cola, SHA-256 y, en reintentos, ParentRequestId. Los eventos y auditoría siguen append-only.

El renderer ya distinguía la copia con `*** REIMPRESIÓN ***` y `COPIA · NO ABRE CAJÓN`. Conserva el logo Opción 07 oficial 560 × 262, CP850/`ESC t 2`, lienzo 576, 48 columnas, TOTAL doble altura, cuatro LF y corte parcial `GS V 1`. No emite `ESC p`.

Un guard concurrente por SaleId rechaza una segunda reimpresión simultánea antes de crear otro request. La UI también deshabilita el botón mientras espera. No hay reintentos automáticos. FAILED/UNKNOWN permiten un nuevo intento explícito relacionado; UNKNOWN pide revisar físicamente antes de repetir.

Sale, SaleItem, Payment, folio, caja y ExpectedCash no cambian. La impresión original conserva el flujo post-commit y su idempotencia.

## Verificación técnica

- Pruebas focalizadas de ReceiptTests: 22 aprobadas.
- Suite completa: 195 aprobadas, 0 fallidas, 0 omitidas.
- Build Release: 0 errores, 0 advertencias.
- Backup/restore conserva snapshot, request REPRINT y sus eventos.
- Pruebas nuevas: Administrator/Manager permitidos; Cashier/inactivo/revocado rechazados; snapshot/hash inmutables; marca REIMPRESIÓN; logo, CP850, corte y cero cajón; doble ejecución bloqueada; FAILED/UNKNOWN/retry sin mutación financiera.
- Migraciones 001–008 intactas.

HEAD inicial: `faa73fa3e87b4f4e689f7e17479833bced43ce42`. El commit de implementación se registrará antes del smoke. No hay push ni remoto configurado.

## Checkpoint físico pendiente

La publicación win-x64 está preparada con la instalación sintética existente de 55 ventas y configuración POS-80 aprobada. Falta iniciar sesión manualmente, abrir Historial, elegir una venta existente y pulsar una sola vez REIMPRIMIR TICKET. No se creará una venta para la prueba.

Después se documentarán PrintRequestId, JobId, estado Winspool, conteos financieros intactos y la validación humana pendiente mediante foto completa. No se inicia cajón, LAN, inventario, producción, facturación, rediseño, F11, instalador, FASE 1H ni otra fase.
