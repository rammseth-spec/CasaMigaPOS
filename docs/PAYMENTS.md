# Formas de pago

Estado: **implementado (migración 020); smoke manual pendiente**. Fecha: 06/10/2026.

## Política vigente (valores por defecto, pendientes de confirmación del dueño)

| Regla | Valor |
|---|---|
| Métodos | Efectivo (`CASH`), Tarjeta (`CARD`, terminal bancaria externa sin integración) y Transferencia (`TRANSFER`). |
| Pagos mixtos | No. Un método por venta. |
| Tarjeta / transferencia | Por el total exacto, sin cambio. Referencia opcional de hasta 40 caracteres (últimos 4 dígitos, autorización, folio). |
| Efectivo | Sin cambios: recibido ≥ total, cambio = recibido − total, sin referencia. |
| Caja | El esperado sólo cuenta efectivo. Tarjeta y transferencia no entran al cajón. |
| Devoluciones | Por el mismo medio de la venta. Sólo las de ventas en efectivo salen del cajón y requieren efectivo suficiente. |
| Ticket | Efectivo conserva el formato validado en impresora física. Tarjeta/transferencia imprimen «Pago TARJETA/TRANSFERENCIA» y «Pagado». |
| Reportes | Desglose por forma de pago (ventas, vendido, devuelto, neto) en pantalla y CSV. |

## Garantías

- `payment` se reconstruyó (mismo procedimiento que la migración 014) conservando todas las filas, la inmutabilidad y el disparador `sale_complete`.
- La base rechaza métodos desconocidos, cambio o sobrepago en tarjeta/transferencia y referencia en efectivo.
- El código lee bases anteriores a la 020 como efectivo sin referencia.

## Pendiente

- Confirmar con el dueño si se requieren pagos mixtos o comisiones de terminal.
- Smoke del ticket de tarjeta/transferencia en la impresora Qian.
