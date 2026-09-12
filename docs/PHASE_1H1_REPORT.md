# FASE 1H.1 — Domain de inventario

Estado: completada técnicamente. FASE 1H continúa abierta. 1H.2 no iniciada.

Baseline: `6a2ee4994b4617cd4bc8276c56b2d888f64fa284`. Implementación: `3ed510c`.

Se agregó `InventoryQuantity` con dimensión y magnitud Int64 no negativa, unidades atómicas mg/µL/pieza, conversiones exactas de mg/g/kg, µL/ml/L y pieza, aritmética checked y rechazo de dimensiones incompatibles, pérdida de precisión, negativos y overflow.

El Domain ahora contiene InventoryItem, InventoryItemUnit, InventoryLocation y ProductInventoryLink. Los artículos distinguen materia prima, terminado, empaque e insumo; la dimensión no puede cambiar. Las presentaciones pertenecen al artículo y aceptan cantidades enteras positivas. MAIN / Almacén principal es la ubicación inicial. La asociación con Product es explícita, opcional y exige una cantidad positiva por unidad comercial.

Se definieron razones estructuradas para merma y ajustes; OTHER exige observación. OPENING_BALANCE, RECEIPT, ADJUSTMENT_GAIN, ADJUSTMENT_LOSS y WASTE están identificados como operaciones de 1H. Los tipos de producción, venta, devolución y transferencia permanecen reservados y deshabilitados.

`Product.TrackInventory` no produce ningún efecto físico. No se modificaron Checkout, Sale, Payment, Money, caja, ExpectedCash, tickets o impresión. No existe migración 009, ledger, saldo persistido, servicios, permisos o UI.

Verificación: 18 pruebas focalizadas y 213 pruebas completas aprobadas; 0 fallidas y 0 omitidas. Build Release con 0 errores y 0 advertencias. Migraciones 001–008 intactas.

Decisión pendiente para 1H.2: elegir una sola autoridad transaccional para mantener InventoryBalance y especificar la reconciliación contra el ledger. No iniciar 1H.2 sin autorización.
