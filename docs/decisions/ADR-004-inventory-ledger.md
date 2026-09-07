# ADR-004 — Inventario por movimientos

Estado: diseño adoptado; no implementado en FASE 0.

La existencia deriva de movimientos inmutables firmados por operación: compra, consumo, salida de producción, venta, merma, ajuste, devolución y transferencia futura. Cada uno conserva ID, entidad, unidad base, cantidad con signo, origen, usuario, terminal, fecha UTC y costo snapshot cuando proceda. Una proyección de saldo sólo será una caché reconciliable.

No utilizar FK polimórfica sin integridad: proponer item de inventario común referenciable para ingrediente/producto. Cada operación tiene ID de idempotencia único y vínculo a sus movimientos; reversos referencian originales. Producción consume ingredientes y genera terminados en la misma transacción. Merma tiene entidad y motivo propios, no ajuste genérico.

Saldo negativo y valoración promedio ponderada requieren reglas operativas antes de inventario. El MVP no anunciará stock confiable antes del ledger; productos sin control de inventario podrán venderse bajo política explícita. No adelantar recetas completas ni compras a FASE 0.
