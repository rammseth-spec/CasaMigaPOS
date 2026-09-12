# ADR-017 — Política y plan de consumo de inventario por venta

Estado: aceptada para FASE 1I.1. No habilita persistencia ni Checkout.

## Contexto

FASE 1H dejó un ledger exacto e inmutable y una asociación explícita Product–InventoryItem. Checkout confirma actualmente Sale, SaleItem, Payment y auditoría sin consultar inventario. El enum de movimientos reservaba `SaleConsumption`, pero Domain y el esquema 10 lo rechazaban deliberadamente.

## Decisión

Domain incorpora una política pura que recibe partidas comerciales y contexto ya resuelto: intención `TrackInventory`, link activo, estado del artículo, posición y saldo disponible. No consulta SQLite. El resultado inmutable conserva una evaluación por SaleItem y agrega el consumo físico por InventoryLocation+InventoryItem en orden determinista.

`TrackInventory=false` sin link produce NoConsumption. `TrackInventory=true` sin link, `TrackInventory=false` con link o contexto de link incompatible producen InventoryTrackingConfigurationInvalid. Un artículo inactivo, MAIN no inicializada, stock insuficiente u overflow generan errores estructurados distintos. No existe consumo parcial ni stock negativo.

El cálculo es `checked((long)SoldQuantity * QuantityPerProductUnitAtomic)`. La suma por posición también es checked. El snapshot por línea conserva posición, ProductId, cantidad vendida, TrackInventory, LinkId, InventoryItemId, LocationId, dimensión, factor y total consumido. No contiene SaleId porque la venta aún no existe.

El constructor general de InventoryOperationRequest sigue rechazando `SaleConsumption`. `SaleConsumptionOperationFactory` es la única entrada preparada: exige un plan con consumo y produce exclusivamente líneas negativas, no opening, sin motivo manual. No existe caso de uso UI/Application que la invoque en 1I.1.

## Autorización futura

`sales.complete` autorizará la consecuencia automática incluso para Cashier; no se exigirá un permiso administrativo de inventario. CheckoutRequest.OperationId será la raíz idempotente y el futuro InventoryOperationId se derivará mediante un mecanismo estable y versionado que se decidirá en 1I.2.

La integración estará deshabilitada por defecto. Enabled/paused, preflight y auditoría se persistirán después. El preflight exigirá reconciliación consistente, links coherentes, artículos activos, MAIN inicializada y cantidades positivas.

## Consecuencias

1I.1 no cambia Checkout, Sale, Payment, ExpectedCash, recibos, impresión ni persistencia. Las ventas históricas no se tocan. Reimpresión nunca genera consumo y la impresión continúa post-commit.

La migración 011 es necesaria en 1I.2 porque el esquema 10 no admite el nuevo tipo, evaluación por venta, snapshot por SaleItem ni estado de activación. Las migraciones 001–010 permanecen inmutables.
