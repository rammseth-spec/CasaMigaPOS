# ADR-015 — Ledger y saldo persistente de inventario

Estado: implementado en FASE 1H.2. Fecha: 2026-09-11.

## Autoridad y transacción

`InventoryMovement` es la fuente histórica autoritativa. `InventoryBalance` es una proyección materializada reconstruible. Sólo `SqliteInventoryStore` actualiza la proyección, dentro de la misma transacción inmediata que inserta operación, movimientos y auditoría. Los triggers nunca calculan ni escriben el saldo.

La actualización de una fila existente exige cantidad y versión esperadas; una discrepancia aborta. Una posición nueva se crea exclusivamente mediante OPENING_BALANCE. Cualquier fallo revierte encabezado, movimientos, saldos y auditoría.

## Apertura e inmutabilidad

OPENING_BALANCE puede persistir una línea con delta cero y marca explícita `is_opening=1`. Esto conserva el conteo físico inicial cero. Un índice parcial permite una sola apertura por artículo+ubicación y los movimientos posteriores exigen una apertura confirmada.

La operación se inserta PENDING y sólo puede cambiar a CONFIRMED. El store hace ambas acciones en una transacción; PENDING no se confirma normalmente en disco. Operaciones y movimientos confirmados no admiten UPDATE o DELETE. Una corrección futura será compensatoria.

## Idempotencia y recuperación

OperationId es único. El contenido normalizado se ordena por ubicación/artículo y se firma con SHA-256. Mismo ID+hash devuelve el confirmado; mismo ID+hash distinto se rechaza. Tras una excepción potencialmente posterior al commit se consulta con una nueva conexión. Si existe CONFIRMED y coincide el hash, se devuelve sin volver a aplicar.

## Exactitud y reconciliación

`InventoryDelta` es Int64 firmado, separado de la magnitud no negativa. Se rechazan cero ordinario, `long.MinValue`, overflow, dimensión incompatible y saldo menor que cero. El movimiento conserva además el snapshot de captura, que no sustituye al valor atómico.

`Reconcile` suma con checked los movimientos confirmados por artículo+ubicación y compara `inventory_balance`. Reporta diferencias y no repara.

## Concurrencia y límites

BEGIN IMMEDIATE serializa escritores SQLite. El orden determinista, idempotencia y UPDATE condicionado añaden defensa. Pruebas con conexiones independientes cubren entradas simultáneas, salidas competidoras y OperationId concurrente.

1H.2 no agrega UI, casos de uso Application, consumo por ventas, recetas, producción, compras, lotes, costos ni red. SQLite no se comparte por LAN.
