# ADR-012 — Snapshot y trazabilidad de tickets

Fecha: 2026-09-11. Estado: aceptada para FASE 1G Bloque A.

## Contexto

`Sale`, `SaleItem` y `Payment` son inmutables y contienen los importes y snapshots comerciales. Los nombres actuales de usuario y terminal pueden cambiar, igual que el encabezado y mensaje del negocio. Reconstruir una reimpresión desde esos datos actuales produciría contenido distinto. Imprimir es además un efecto físico posterior al commit y no puede compartir atomicidad con SQLite.

## Decisión

`ReceiptDocument` vive en Application y no conoce Windows, impresoras ni ESC/POS. Se crea desde `SalesHistoryDetail`: usa exclusivamente `SaleItem` para producto, nombre, precio, cantidad y total; captura una vez los nombres de cajero y terminal; incorpora un perfil comercial configurado; conserva hora local con offset. Money continúa en centavos `Int64` y todas las validaciones se realizan con las operaciones checked existentes.

La representación JSON canónica se guarda una sola vez en `receipt_snapshot` con versión y SHA-256. Snapshot y hash se revalidan al leer. Las ventas anteriores a 1G crean el snapshot durante su primer intento de impresión. El snapshot no puede actualizarse ni borrarse.

Cada efecto físico se representa mediante una solicitud inmutable y eventos append-only. `ORIGINAL` es único por venta; `RETRY` referencia el intento anterior; `REPRINT` exige permiso separado. Los estados son REQUESTED, SUBMITTED, COMPLETED, FAILED y UNKNOWN. SUBMITTED sólo significa que Windows aceptó el trabajo. UNKNOWN obliga a revisar físicamente antes de reintentar.

La solicitud y su evento REQUESTED se confirman antes de llamar al transporte. El envío ocurre sin transacción SQLite abierta. El resultado se agrega después en otra transacción. Una falla de persistencia posterior a un envío aceptado se reporta como UNKNOWN y jamás altera venta, pago, carrito o ExpectedCash.

## Consecuencias

La misma venta no genera dos solicitudes ORIGINAL. Los reintentos y reimpresiones no crean otra venta. El backup incluye snapshots, solicitudes y eventos porque viven en SQLite; `printing.json` queda fuera por ser configuración física de una PC. Los datos comerciales históricos se conservan, pero los nombres de cajero/terminal de ventas anteriores a 1G sólo pueden capturarse con el valor disponible al crear por primera vez el snapshot.
