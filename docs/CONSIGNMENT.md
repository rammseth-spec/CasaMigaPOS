# Charolas a consignación

Casa Miga deja charolas de 12 o 15 panes surtidos en tiendas cliente. Al día siguiente recoge la charola, cobra sólo lo vendido y lo que regresa se aparta para hacer **Piedra**. Pantalla **Charolas** (permisos `consignment.view` y `consignment.manage`: Administrator y Manager). Migración `021_consignment.sql`.

## Reglas decididas con el negocio (06/10/2026)

| Tema | Regla |
|---|---|
| Precio | **Precio de charola por producto**, igual para todas las tiendas (pestaña «Precios de charola»). Un pan sin precio no se puede poner en charolas. Cada entrega congela el precio de charola y el de mostrador de ese momento. |
| Contenido | Se registran **piezas de cada pan**. Cada tienda tiene su **charola habitual**, que se propone al entregar y se puede ajustar ese día. |
| Sobrante | Lo que regresa **no es merma**: se aparta para hacer Piedra y se reporta como «Regresaron (Piedra)». |
| Cobro | Cuenta **por tienda, aparte de la caja del mostrador**: lo vendido se carga a la tienda y los pagos (efectivo, tarjeta o transferencia) abonan. Si no paga completo queda saldo pendiente. No cambia el efectivo esperado del corte. |

## Flujo

1. **Entregar**: elige la tienda; se propone su charola habitual. Se guarda la entrega con sus piezas y precios congelados.
2. **Recoger**: captura cuántas piezas regresaron de cada pan; las demás se vendieron. El importe vendido se carga a la tienda y se puede cobrar en el momento (total, parcial o nada).
3. **Cobrar**: registra pagos posteriores contra el saldo. No se acepta cobrar más de lo que la tienda debe.
4. **Anular**: sólo una charola que sigue en la tienda (captura equivocada), con motivo. No cuenta en reportes ni se cobra.

Entregas, recolecciones, anulaciones y pagos son inmutables (triggers). La base valida que vendidas + regresadas = entregadas y que el importe sea vendidas × precio de charola. Cada operación queda en auditoría (`consignment.*`), que sí se sube a la nube.

## Reporte

Por periodo (charolas entregadas en el periodo, sin anuladas; pagos recibidos en el periodo): charolas, piezas dejadas, vendidas y % de venta (de las ya recogidas), regresadas para Piedra, cargado, pagado, saldo actual y **margen que se queda en las tiendas** = precio de mostrador − precio de charola de las piezas vendidas. Por tienda y por pan, con barras de % de venta por tienda.

## Pendiente

- Subir las tablas de charolas a la nube (hoy sólo viaja la auditoría con todos los detalles; requiere ampliar `sync_outbox` y la API).
- Registrar en Inventario/Producción las piezas que regresan como insumo de Piedra.
- Ticket o comprobante impreso de entrega y de cobro para la tienda.
