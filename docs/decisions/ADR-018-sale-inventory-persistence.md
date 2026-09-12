# ADR-018 — Schema 11: evaluación y persistencia de consumo por venta

Estado: aceptado para 1I.2. No habilita consumo desde Checkout.

## Esquema y migración

011_sale_inventory_consumption.sql reconstruye inventory_operation e inventory_movement dentro de la transacción de Database.Initialize, con foreign_keys=ON. Copia las filas de movimientos a una tabla temporal de migración, reconstruye padre e hija y recrea índices/triggers. Conserva columnas y valores previos; añade sale_evaluation_id nullable al padre. No usa writable_schema ni modifica 001–010. Un fallo revierte DDL, datos y registro de versión.

Nuevas tablas STRICT: sale_inventory_evaluation, sale_inventory_line_snapshot e inventory_sale_consumption_config. Evaluación única por SaleId y CheckoutOperationId; InventoryOperationId es único cuando existe. Las FKs de historia usan RESTRICT. Los snapshots referencian SaleItem por (sale_id,position); cada uno conserva ProductId, cantidad vendida, tracking, link nullable, artículo, MAIN, dimensión, factor, total y policy_version=1.

DISABLED guarda solamente el header, sin snapshots ni operación. NO_CONSUMPTION conserva todas las líneas evaluadas no inventariables, sin operación. CONSUMED conserva todas las líneas, incluidas no inventariables, y una operación SALE_CONSUMPTION confirmada. BLOCKED no es estado persistible. No hay backfill: las ventas anteriores y las ventas del Checkout actual siguen sin evaluación.

## Sellado, circularidad controlada e integridad

La venta se escribe con las primitivas existentes. La operación de inventario contiene sale_evaluation_id con FK diferida; los snapshots también difieren su FK al header de evaluación. La evaluación se inserta al final y valida completitud, identidad comercial, actor/terminal, operación confirmada y sumas de snapshots frente a movimientos. Al commit no puede quedar consumo huérfano. La circularidad está delimitada por estas FKs diferidas y no requiere desactivar foreign_keys.

Los headers y snapshots no admiten UPDATE/DELETE; tampoco se agregan snapshots tras sellar. El ledger mantiene sus protecciones existentes. SALE_CONSUMPTION sólo admite delta negativo, no opening, MAIN y BalanceAfter>=0. Los triggers protegen integridad; ninguno escribe InventoryBalance.

## Identidad e idempotencia

Algoritmo SaleConsumptionIdentity.OperationIdV1: UUIDv5 con namespace fijo
`82b7820c-b1d4-4cb2-a5a6-19419f756cb9` y nombre UTF-8 del CheckoutOperationId en formato Guid D minúsculo. Namespace y resultado usan byte order de red explícito; versión de identidad 1. No usa SaleId, reloj, proceso, terminal ni random.

Vectores fijos:
- aabbccdd-1234-5678-9abc-def012345678 → f2e3751e-040d-5020-b932-d14b048158e9
- 00000000-0000-0000-0000-000000000001 → da60d397-7f5a-5f7f-85b3-0f01bd8a0815

La prueba incluye el vector estándar DNS/www.example.com → 2ed6657d-e927-568b-95e1-2665a8aea6a2. Referencia del algoritmo: [RFC 9562, UUIDv5](https://www.rfc-editor.org/rfc/rfc9562.html#section-5.5), compatible con UUIDv5 de RFC 4122. Implementación propia a partir de la especificación.

El esquema real de Sale NO posee columna request hash; Checkout compara el contenido comercial contra la venta inmutable. Por ello se agrega evaluation_sha256, sin cambiar Sale: SHA-256 sobre un contrato JSON V1 explícito con identidad/contexto comercial, fecha, recibido, partidas y snapshots ordenados. No se serializan objetos de dominio completos para evitar cambios accidentales por nuevas propiedades. La operación conserva su request_sha256 V1 anterior, sin cambiar el hash de operaciones históricas. La misma evaluación devuelve el registro existente; contenido diferente produce InventoryIdempotencyConflictException.

## Infraestructura y transacción futura

SqliteInventoryStore.WriteOperation recibe una conexión y transacción existentes. Reutiliza exactamente el escritor de movimientos, balance, auditoría y confirmación utilizado por Apply. Apply mantiene su autorización e idempotencia previas y rechaza SaleConsumption. InventoryMovement sigue siendo fuente histórica; SqliteInventoryStore sigue siendo la única autoridad que actualiza InventoryBalance.

SqliteSaleInventoryStore es interno: Save y Find evaluación, ReadConfig. Save valida el plan completo y MAIN, deriva OperationId y usa un savepoint para evitar escrituras parciales aun si el llamador captura una excepción. No hace commit, no abre conexiones y no implementa recovery en otra transacción. RunTechnical es un adaptador interno del unit of work para probar venta/pago y consumo en la misma conexión/transacción. La aplicación no lo usa todavía. InternalsVisibleTo sólo habilita las pruebas.

El origen Checkout/system-generated queda representado por kind SALE_CONSUMPTION y la relación sale_evaluation_id → checkout_operation_id. El actor coincide con Sale.UserId y puede ser Cashier. La autorización sales.complete y lectura/revalidación de la política dentro de Checkout pertenecen a 1I.3; no se implementan aquí.

## Activación y continuidad

Singleton id=1, state DISABLED inicial, version=1, updated_utc y updated_by nullable sólo en el estado inicial. Las transiciones preparadas son DISABLED→ENABLED, ENABLED→PAUSED y PAUSED→ENABLED con versión incremental y actor. No se permite PAUSED antes de activación. No hay API de escritura de configuración, UI, preflight ni auditoría de activación; corresponden a 1I.4. Permiso inventory.sale_consumption.configure sólo para Administrator; los demás permisos permanecen iguales.

La primitiva técnica no decide si la integración está habilitada: recibe null para DISABLED o un plan evaluado. Sólo las pruebas la invocan en 1I.2. 1I.3 debe leer el singleton y resolver la política en la transacción autorizada. No confundir existencia del esquema con activación.

.cmbackup ya respalda toda la base y valida/migra el candidato restaurado; no requiere cambiar su formato. Las pruebas de schema 11 verifican evaluación, snapshots, ledger/balance, flag, idempotencia, integrity_check, foreign_key_check y reconciliación tras restore.

Pendientes: integración funcional y recuperación de Checkout (1I.3); preflight, habilitación, pausa/reanudación y auditoría administrativa (1I.4). No se inició ninguna.
