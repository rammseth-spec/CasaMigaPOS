# Cancelaciones y devoluciones

Estado: **implementado (migración 019); smoke manual pendiente**. Fecha: 06/10/2026.

## Política vigente (valores por defecto, pendientes de confirmación del dueño)

| Regla | Valor |
|---|---|
| Cancelación | Devuelve la venta **completa**. Sólo si no tiene devoluciones previas y su caja sigue abierta y sin conteo (mismo turno). |
| Devolución | Total o parcial, de cualquier venta anterior. Acumulable hasta la cantidad vendida por partida. |
| Quién | Administrator y Manager (`sales.refund`). Cashier no. |
| Motivo | Obligatorio, 3–200 caracteres. |
| Efectivo | Sale de la caja **abierta** de la terminal que registra la devolución. Nunca más del efectivo esperado en esa caja. |
| Caja | Esperado = fondo + ventas en efectivo − devoluciones en efectivo. El cierre muestra las devoluciones. Cajas cerradas no cambian. |
| Inventario | **No se modifica.** El producto devuelto no regresa a existencias (equivale a merma). |
| Ticket | Sin comprobante impreso de devolución todavía. |

## Garantías

- La venta original nunca se modifica ni se borra; la devolución es un registro compensatorio inmutable (`sale_refund`, `sale_refund_item`).
- La base valida por trigger: caja abierta y sin conteo de la misma terminal, partidas de la misma venta con precio original, cantidades acumuladas ≤ vendidas, reglas de cancelación y efectivo suficiente.
- Todo ocurre en una transacción: partidas, encabezado, auditoría (`sale.cancelled` / `sale.returned`) y entrada en `sync_outbox`. Si algo falla, no queda nada.
- Resultado incierto: cada devolución tiene `operation_id` único; si el commit ocurrió, el servicio lo devuelve en vez de fallar.

## Dónde

Historial de ventas → Ver detalle → sección «Cancelaciones y devoluciones». La lista muestra la columna «Devuelto».

## Pendiente

- Confirmar la política con el dueño (plazo máximo, reingreso a inventario, autorización de supervisor para cajeros).
- Comprobante impreso de devolución.
- Devoluciones con otros métodos de pago cuando existan.
