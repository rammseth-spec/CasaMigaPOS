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
| **Quién entra** | Cajero: **Vender** y Catálogo (no ve Caja: muestra el esperado del turno y el conteo es ciego). Encargado y administrador: todo el panel más Vender. |

## Reglas (las mismas de la caja)

- Precio y nombre salen del **catálogo vigente en la nube**, nunca del navegador; sólo productos y categorías activos de la sucursal de la caja. Si un precio cambió después de armar el ticket, el cobro se rechaza con aviso y el ticket se actualiza (nunca se cobra un total distinto al que vio el cajero).
- Partidas únicas, de 1 a 999 piezas, hasta 100 panes distintos; total hasta $1,000,000.
- Efectivo: lo recibido debe alcanzar y no lleva referencia. Tarjeta y transferencia: se cobra exacto.
- **Cada cobro tiene un identificador**: si se cae la red y se toca Cobrar otra vez, devuelve la misma venta; nunca cobra dos veces. Mismo identificador con otro contenido se rechaza.
- **Una sola sentencia por operación** (`cloud/lib/pos.ts`): actualiza la fila de la caja (`web_till`: consecutivos y turno; sirve de candado y de condición) y escribe el registro inmutable (`sync_event`) y el estado (`entity_state`). Si el turno ya se contó o cerró, no se escribe nada. Dos dispositivos en la misma caja se forman en fila: los folios no se repiten.
- Esperado = fondo + ventas en efectivo (aplicado, sin cambio) − devoluciones de ventas en efectivo + entradas − gastos y retiros (como `CashClosingService`).
- Las ventas, turnos, conteos y la bitácora se guardan **con los mismos formatos que sube la caja de escritorio**, así Resumen, Ventas, Reportes, Ganancia y Caja las cuentan sin cambios. La copia nocturna de la nube las respalda (Respaldos no muestra las cajas web porque no tienen base propia).

Base: `cloud/db/004_web_pos.sql` (`terminal.kind`, `web_till`, `web_operation`). Rutas: `GET /api/web/pos`, `POST /api/web/pos-till | pos-open | pos-sale | pos-count | pos-close`. Pruebas: `cloud/test/pos.test.ts`.

## Diferencias con la caja de escritorio

- Necesita Internet en todo momento (no hay modo sin conexión).
- Imprime con el diálogo del navegador; no abre cajón ni manda ESC/POS.
- Las ventas web **no descuentan inventario** de la caja de escritorio ni aparecen en los reportes de la app de escritorio (sí en todos los de la web).

## Siguientes fases

1. Cancelaciones y devoluciones de ventas web.
2. Gastos, retiros y entradas en la caja web.
3. Reimprimir tickets anteriores.
4. Descontar inventario (mandar el consumo a la caja de escritorio).
