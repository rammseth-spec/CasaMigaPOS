# Punto de venta en la web (Vender)

Decisión del usuario (08/10/2026): **POS web completo**, para vender y cobrar desde Mac, iPad o celular, no sólo consultar. Se construye por fases.

## Fase 1 (lista)

| Qué | Cómo |
|---|---|
| **Caja web** | El administrador la crea en **Vender → + Nueva caja web**. Cada una tiene su turno, fondo y folios; su número sigue al mayor de la sucursal (`CAJA-WEB-02` → folios `C02-000001…`), así no se confunde con la caja de escritorio (`C01`). Cada dispositivo elige su caja y la recuerda. |
| **Abrir turno** | Fondo inicial (permiso `cash.open`: cajero, encargado, administrador). Un turno abierto por caja. |
| **Vender** | Igual que en la app: buscador, categorías, cuadrícula con fotos, contador por pan, ticket con − / + y quitar, total. En celular y tableta vertical el ticket se abre desde la barra de abajo. |
| **Cobrar** | Efectivo con atajos (exacto, $50, $100, $200, $500) y cambio; tarjeta o transferencia por el total exacto con referencia opcional (máx. 40). |
| **Ticket** | En pantalla y **Imprimir ticket** (impresión del navegador, 72 mm): folio, fecha, cajero, caja, partidas, total, pago y cambio. |
| **Corte** | **Corte de caja** (permiso `cash.close`; ajena con `cash.close.other`): conteo ciego, luego esperado / contado / diferencia y **Cerrar turno**. Después del conteo ya no entran ventas. |
| **Tickets** | Botón **Tickets**: ventas del turno abierto; con un folio (`C02-000015`, o sólo `15` para esta caja) se busca cualquier venta de la sucursal. Cada ticket muestra sus devoluciones. |
| **Reimprimir** | Desde el ticket, con «*** REIMPRESIÓN ***» y «COPIA» (permiso `sales.receipt.reprint`: encargado y administrador, como en la app). |
| **Cancelar o devolver** | Permiso `sales.refund` (encargado y administrador). **Cancelar venta**: completa, del mismo turno y sin devoluciones previas. **Devolver productos**: piezas por partida hasta lo que queda. Motivo obligatorio (3 a 200). La venta original no cambia; se registra la compensación con su bitácora y la app dice qué hacer con el dinero: efectivo sale de la caja (si alcanza el esperado); tarjeta o transferencia se reembolsan fuera de la caja. Las ventas de la caja de escritorio se devuelven en esa caja. |
| **Gasto, entrada o retiro** | Permiso `cash.movement` (cajero, encargado, administrador); **retiro** sólo con `cash.withdrawal` (encargado y administrador). Gasto con categoría (Insumos, Gas, Limpieza, Transporte, Mantenimiento, Otro; «Otro» pide nota). No se saca más efectivo del esperado. Lista «En este turno». Inmutable: un error se corrige con el movimiento contrario. |
| **Quién entra** | Cajero: **Vender** y Catálogo (no ve Caja: muestra el esperado del turno y el conteo es ciego). Encargado y administrador: todo el panel más Vender. |

## Reglas (las mismas de la caja)

- Precio y nombre salen del **catálogo vigente en la nube**, nunca del navegador; sólo productos y categorías activos de la sucursal de la caja. Si un precio cambió después de armar el ticket, el cobro se rechaza con aviso y el ticket se actualiza (nunca se cobra un total distinto al que vio el cajero).
- Partidas únicas, de 1 a 999 piezas, hasta 100 panes distintos; total hasta $1,000,000.
- Efectivo: lo recibido debe alcanzar y no lleva referencia. Tarjeta y transferencia: se cobra exacto.
- **Cada cobro, devolución y movimiento tiene un identificador** (migración de la nube 005): si se cae la red y se vuelve a tocar el botón, devuelve lo ya registrado; nunca lo duplica. Mismo identificador con otro contenido se rechaza.
- Devoluciones y movimientos sólo con el turno abierto y **antes del conteo**, igual que en la app.
- **Una sola sentencia por operación** (`cloud/lib/pos.ts`): actualiza la fila de la caja (`web_till`: consecutivos y turno; sirve de candado y de condición) y escribe el registro inmutable (`sync_event`) y el estado (`entity_state`). Si el turno ya se contó o cerró, no se escribe nada. Dos dispositivos en la misma caja se forman en fila: los folios no se repiten.
- Esperado = fondo + ventas en efectivo (aplicado, sin cambio) − devoluciones de ventas en efectivo + entradas − gastos y retiros (como `CashClosingService`).
- Las ventas, turnos, conteos y la bitácora se guardan **con los mismos formatos que sube la caja de escritorio**, así Resumen, Ventas, Reportes, Ganancia y Caja las cuentan sin cambios. La copia nocturna de la nube las respalda (Respaldos no muestra las cajas web porque no tienen base propia).

Base: `cloud/db/004_web_pos.sql` (`terminal.kind`, `web_till`, `web_operation`). Rutas: `GET /api/web/pos`, `POST /api/web/pos-till | pos-open | pos-sale | pos-count | pos-close`. Pruebas: `cloud/test/pos.test.ts`.

## Diferencias con la caja de escritorio

- Necesita Internet en todo momento (no hay modo sin conexión).
- Imprime con el diálogo del navegador; no abre cajón ni manda ESC/POS.
- Las ventas web **no descuentan inventario** de la caja de escritorio ni aparecen en los reportes de la app de escritorio (sí en todos los de la web).

## Fases

- **Fase 1** (08/10/2026): caja web, turno, venta con fotos, cobro, ticket e impresión, corte.
- **Fase 2** (09/10/2026): tickets, reimpresión, cancelaciones y devoluciones, gastos/entradas/retiros. Pruebas `cloud/test/pos.test.ts` (5).
- **Inventario**: hoy el descuento automático de inventario al vender está **apagado** en la caja de escritorio (`inventory_sale_consumption_config` = DISABLED) y ningún producto lleva inventario, así que las ventas web y las de escritorio se comportan igual. Si se enciende, faltará mandar el consumo de las ventas web a la caja de escritorio (un cambio de la web más, como los de la ruta).
