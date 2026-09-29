# Pedidos — requisitos futuros, sin implementación en R2

La mayoría de los pedidos a tienda se captura antes de cobrar. Se requiere diseñar una entidad persistente Order/Pedido; no representarla como Sale anticipada ni como nota temporal. Este requisito sustituye la propuesta anterior de resolver todo agregando únicamente un tipo a Sale.

Contextos: Mostrador, Pedido y Pedido a tienda. Un Pedido deberá conservar folio propio, tienda/cliente, persona de referencia, productos, cantidades, fecha del pedido, fecha/hora requerida o de entrega, nota, estado, timestamps y auditoría.

Crear un Pedido no crea Sale ni Payment, no incrementa ExpectedCash y no representa una venta terminada. Al cobrar, Pedido → Checkout existente → Sale, conservando OrderId ↔ SaleId. La futura fase debe definir explícitamente estados y transiciones, compatibilidad con Historial, backup/restore y trazabilidad.

Registrar pedidos diarios/recurrentes y ocasionales. Evaluar plantillas de pedido habitual por tienda, instanciables para una fecha, sin recapturar todos los productos. No generar ventas automáticas.

No decidir reservas en R2. Separar pedido planificado, venta confirmada y consumo de inventario. Las políticas de reserva, entrega parcial y consumo requieren alcance y decisiones posteriores.

Pregunta pendiente: ¿alguna tienda recibe el pan y paga después, otro día? Si ocurre, evaluar crédito/cuentas por cobrar. No simularlo conservando indefinidamente un pedido sin cobrar. No implementar hasta aclararlo.

Cuando un Pedido se cobra, su ticket final deberá admitir PEDIDO A TIENDA, tienda, persona/referencia, folio del pedido y nota relevante, siempre con Variante 04. Podría existir un comprobante previo marcado PEDIDO — NO PAGADO, sin afectar Caja.

R2 no implementa Order, crédito, plantillas, reservas ni comprobantes de pedido; no crea migración 017. La futura fase requiere autorización y revisión de persistencia con Astra.
