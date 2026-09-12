# FASE 1H.2 — Migración, ledger y saldo

Estado: completada técnicamente. FASE 1H continúa abierta. 1H.3 no iniciada.

Baseline `d944c74c21b49739d1de06d5eaa874939aa88d50`. Implementación `a8fcfb2`.

La migración 009 crea measurement_unit, inventory_item, inventory_item_unit, inventory_location, inventory_operation, inventory_movement, inventory_balance y product_inventory_link. Siembra siete unidades exactas, MAIN y los cinco permisos aprobados con su matriz Administrator/Manager/Cashier.

SqliteInventoryStore posee la transacción inmediata y es la única autoridad que actualiza el saldo. InventoryMovement es el historial; InventoryBalance es una proyección. Las actualizaciones comprueban cantidad anterior y versión. Los triggers protegen inmutabilidad, apertura, secuencia y confirmación, pero no actualizan balances.

OPENING_BALANCE cero se conserva como línea opening de delta cero y balance cero. Deltas ordinarios son Int64 firmados no cero. Se prohíben negativos y overflow. Operaciones multipartida se ordenan y revierten enteras.

OperationId+SHA-256 proporcionan idempotencia. El store recupera mediante otra conexión después de un resultado incierto y sólo devuelve el confirmado si el hash coincide. Reconcile compara suma checked del ledger con el saldo y no repara.

Pruebas con conexiones independientes validan entradas simultáneas, salidas competidoras, mismo OperationId y conflictos. Backup/restauración conserva schema 9, catálogo, presentaciones, operación, movimientos, saldo, links e idempotencia.

Verificación: 41 focalizadas y 236 completas aprobadas, ninguna fallida u omitida; Release con 0 errores y 0 advertencias. Migraciones 001–008 intactas. Checkout, ventas, ExpectedCash, tickets e impresión sin cambios.

Pendiente 1H.3: autorizar casos de uso Application y su flujo de confirmación. No hay UI ni consumo por ventas.
