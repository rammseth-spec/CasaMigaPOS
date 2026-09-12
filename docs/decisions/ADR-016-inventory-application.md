# ADR-016 — Casos de uso Application de inventario

Estado: aceptada en FASE 1H.3.

## Contexto

El ledger de 1H.2 acepta deltas exactos, pero Presentation no debe construirlos. El operador expresa saldo inicial, cantidad recibida, merma positiva o conteo físico absoluto. El conteo introduce una carrera entre leer el saldo y aplicar la diferencia.

## Decisión

Application define IInventoryStore, comandos, resultados, errores y read models sin referencias a SQLite. InventoryService autoriza, valida intención y convierte unidades; Presentation futura sólo podrá invocar casos de uso empresariales.

Infrastructure conserva la frontera transaccional. Para AdjustToPhysicalCount, Application entrega saldo y versión esperados junto al request derivado. SqliteInventoryStore los compara dentro de la misma transacción inmediata. Si cambiaron, no escribe y devuelve ConcurrencyConflict. El retry consulta primero OperationId: el mismo conteo normalizado recupera la operación; un contenido diferente devuelve IdempotencyConflict.

La configuración combina mutación y audit_entry en una transacción. Los movimientos mantienen el audit agregado existente del ledger. NoChange no genera evidencia ficticia.

Las consultas abren conexiones read-only y transacciones deferred. Usan paginación máxima de 100, orden estable y joins/consultas agregadas para evitar N+1. ReconciliationStatus sólo informa y nunca repara.

## Consecuencias

No se necesita migración 011. Los hashes 001–010 permanecen intactos. La API no ofrece ApplyDelta ni ApplyAdjustment genérico. El conteo registra como movimiento la diferencia calculada en unidad atómica; la intención original sigue representada por el comando durante la operación, pero no se añade un registro separado cuando no existe diferencia.

1H.4 deberá definir cómo la UI conserva OperationId, confirma movimientos y presenta conflictos/recovery. El consumo por venta continúa deshabilitado.
