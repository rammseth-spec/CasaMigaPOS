# Movimientos de efectivo: gastos, entradas y retiros

Pantalla **Caja**, con la caja abierta: tarjeta «Movimientos de efectivo» a la derecha del estado (debajo si la ventana es angosta).

## Tipos

| Tipo | Qué es | Efecto en el efectivo esperado | Quién |
|------|--------|--------------------------------|-------|
| Gasto | Efectivo que sale para pagar algo del negocio | Resta | Cajero, encargado, administrador |
| Entrada | Efectivo que entra y no es venta: cambio que se trae, fondo extra | Suma | Cajero, encargado, administrador |
| Retiro | Efectivo que sale a resguardo: depósito al banco, entrega al dueño. No es gasto | Resta | Encargado y administrador |

Permisos (migración 022): `cash.movement` para los tres roles y `cash.withdrawal` sólo para Manager y Administrator. El cajero no ve la opción «Retiro».

## Categorías de gasto

Lista fija: Insumos, Gas, Limpieza, Transporte, Mantenimiento y Otro. «Otro» exige explicar en la nota (3 caracteres o más). Entradas y retiros no llevan categoría. La nota es opcional en lo demás (hasta 200 caracteres, sin saltos de línea).

## Reglas

- Sólo con la caja abierta de esta terminal y **antes del conteo ciego**: después del conteo el cajón queda congelado.
- Nunca se saca más efectivo del que debe haber: fondo + ventas en efectivo − devoluciones en efectivo ± movimientos. Las devoluciones en efectivo también respetan los movimientos (no se devuelve dinero que ya se retiró).
- Registros **inmutables**: un error se corrige con el movimiento contrario (por ejemplo, una entrada por lo que se registró de más como gasto).
- Cada registro lleva un identificador de operación: reintentar tras un error incierto no duplica.
- La base de datos repite las reglas (trigger `cash_movement_valid`, `CHECK` de categoría y nota, sin `UPDATE`/`DELETE`) y la auditoría (`cash.expense`, `cash.withdrawal`, `cash.deposit`) es única por movimiento.

## Dónde se ven

- **Caja**: lista «En este turno» (hora, tipo, categoría · nota · quién, importe) y totales por tipo. Los importes del cajón siguen ocultos hasta el conteo ciego.
- **Cierre**: el resultado del conteo desglosa gastos, retiros y entradas antes del efectivo esperado.
- **Reportes → Resumen → Cortes**: columna «Mov.» (neto del turno) y el efectivo esperado ya lo incluye; también en el CSV («Movimientos»).

## Pendiente

- Comprobante impreso del movimiento (el usuario eligió «sólo registro por ahora»).
- Categorías configurables.
- La nube recibe cada movimiento por su entrada de auditoría (con tipo, categoría, importe y nota); la tabla `cash_movement` todavía no se sincroniza como entidad propia.
