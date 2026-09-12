# ADR-014 — Cantidades y artículos de inventario

Estado: implementado en FASE 1H.1. Fecha: 2026-09-11.

## Decisión

Las cantidades físicas no son dinero. `InventoryQuantity` conserva dimensión y una magnitud no negativa Int64 expresada en unidad atómica: mg para MASS, µL para VOLUME y pieza para COUNT. Las conversiones estándar usan factores enteros. Un `decimal` sólo puede entrar como intermediario y debe convertirse exactamente; no se persiste y nunca se usan float/double.

Cero es válido para representar ausencia de existencia. Las cantidades por presentación y por unidad comercial son estrictamente positivas. Sumas, restas, multiplicaciones y conversiones usan `checked`; no se permite restar por debajo de cero ni operar entre dimensiones.

`InventoryItem` es distinto de `Product` y admite RAW_MATERIAL, FINISHED_GOOD, PACKAGING y SUPPLY. Su dimensión es inmutable. Las presentaciones saco/caja/paquete pertenecen al artículo mediante `InventoryItemUnit`, almacenan su equivalencia atómica y se capturan en números enteros.

`ProductInventoryLink` es opcional y explícito. Conserva ProductId, InventoryItemId y cantidad exacta positiva por unidad comercial. `TrackInventory` no crea esta relación ni activa movimientos.

MAIN / Almacén principal es la ubicación inicial. El modelo admite identidades adicionales sin implementar transferencias. Los motivos de merma y ajuste son códigos estructurados; OTHER exige observación.

## Límites

Esta decisión no implementa ledger, saldo materializado, SQLite, permisos, servicios, UI, descuento por venta, recetas, producción, compras, proveedores, lotes ni caducidad. Los movimientos futuros se enumeran sólo para separar los habilitados en 1H de los reservados.

En 1H.2 debe elegirse una sola autoridad transaccional para actualizar `InventoryBalance`. `InventoryMovement` será la fuente histórica autoritativa y el saldo será una proyección reconstruible.
