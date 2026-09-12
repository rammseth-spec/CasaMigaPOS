# CASA MIGA POS — REPORTE FINAL FASE 1H.3

Estado: 1H.3 COMPLETADA. FASE 1H permanece ABIERTA; 1H.4 NO inició. Baseline: `cab53b33d322a53b3d809471d4b193b53a04b55d`. Commit técnico: `9e96edc2a4652487adce9a4ad2693ff70a3d218d`. El commit documental que contiene este archivo es el HEAD final recomendado. Modelo utilizado: GPT-5; no se usó Astra.

## Domain y contratos

InventoryOperationRequest coincide con schema 10. RECEIPT acepta note opcional normalizada de 1–500 caracteres, rechaza whitespace, exceso y reason_code. OPENING_BALANCE sigue rechazando reason/note. Las pruebas cubren las seis regresiones solicitadas.

InventoryModels incorpora comandos, resultados, IInventoryStore, read models paginados y InventoryApplicationException con códigos estables: Unauthorized, InactiveUser, InvalidUnit, DimensionMismatch, InvalidQuantity, InvalidNote, NotInitialized, AlreadyInitialized, InsufficientStock, IdempotencyConflict, InventoryItemNotFound, InventoryItemInactive, NoChange, ConcurrencyConflict, InconsistentBalance y errores de referencia/conflicto. Ningún contrato expone conexión, reader, fila o SQL.

## Casos de uso

InventoryService implementa RegisterOpeningBalance, ReceiveInventory, RecordWaste y AdjustToPhysicalCount. Los callers entregan OperationId; Application nunca lo sustituye en retry. Opening admite cero y sólo Administrator. Receive exige cantidad positiva, inventory.receive, referencia/note opcionales y produce delta positivo sin reason_code. Waste exige cantidad positiva, inventory.waste, razón aprobada y produce delta negativo; OTHER exige note. El stock nunca puede quedar negativo.

AdjustToPhysicalCount recibe el conteo absoluto. Calcula ADJUSTMENT_GAIN o ADJUSTMENT_LOSS; igualdad devuelve InventoryAdjustmentOutcome.NoChange sin operación, movimiento o auditoría. Sólo Administrator puede ajustar. La precondición de saldo/versión se verifica dentro de la transacción inmediata: una modificación concurrente devuelve ConcurrencyConflict y no aplica un delta obsoleto. Retry del mismo conteo recupera OperationId/movimiento; contenido diferente devuelve IdempotencyConflict.

Configuración incluye crear artículo, actualizar nombre/tipo con dimensión inmutable, activar/desactivar sin borrado, rechazar desactivación con saldo, agregar presentación entera positiva y crear/quitar asociación explícita Product–InventoryItem. La asociación exige cantidad atómica positiva y máximo una activa por Product; no crea movimiento ni activa SALE_CONSUMPTION. TrackInventory no tiene efecto automático.

Administrator conserva view/configure/receive/adjust/waste; Manager view/receive/waste; Cashier ninguno. Usuario activo, permiso, rol Administrator cuando corresponde y terminal se revalidan. Infrastructure vuelve a comprobar autorización dentro de las transacciones de movimientos/configuración.

Las acciones inventory.item.created/updated/activated/deactivated, inventory.unit.added e inventory.product_link.created/removed se auditan junto a su mutación. El ledger mantiene inventory.operation.confirmed como evidencia primaria; no se duplica cada movimiento en Audit.

## Consultas

Inventory list ofrece código, nombre, tipo, dimensión, estado, saldo MAIN, unidad atómica e indicador de asociación; filtra texto literal normalizado, tipo y estado. Item detail ofrece artículo, MAIN, saldo, presentaciones y asociaciones. Movement history devuelve UTC, tipo, artículo/ubicación, delta, saldos anterior/posterior, actor, terminal, razón, note, referencia y OperationId. Reconciliation status compara saldo materializado/reconstruido y sólo informa.

Lista e historial usan páginas configurables de 1–100, OFFSET acotado y orden estable. Cada read model se carga con consultas set-based y joins, sin consultas por fila. Todas usan conexión read-only y transacción deferred.

## Verificación y preservación

52 pruebas focalizadas de Domain/Application/Infrastructure aprobadas. Suite completa: 265 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores y 0 advertencias. La prueba de concurrencia intercala una recepción entre lectura y apply del conteo, comprueba ConcurrencyConflict, ausencia de adjustment y reconciliación intacta. También se cubren autorización/roles/revocación/inactividad, opening >0/=0/retry, recepción/note/conversiones, waste/OTHER/stock, gain/loss/NoChange, configuración/auditoría/rollback, presentaciones, links, filtros/paginación/orden/historial/reconciliación y errores estables.

Migraciones 001–010 permanecen byte por byte intactas; hashes en evidence/phase1h3-migration-hashes.txt. La suite completa conserva Checkout, Sale, Payment, ExpectedCash, tickets e impresión. No se usó impresora.

## Riesgos y continuidad

La UI deberá conservar OperationId desde preparación hasta resolución y volver a consultar ante ConcurrencyConflict. Un NoChange no deja registro por decisión explícita de 1H.3. El listado usa búsqueda normalizada sin FTS; no se promete rendimiento para catálogos futuros extraordinariamente grandes. La reconciliación informa inconsistencias pero no repara.

No se implementó UI WPF, menú, formularios, ventas→inventario, SALE_CONSUMPTION, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, hardware, LAN, rediseño, fullscreen o instalador. 1H.4 NO inició.

Próximo paso recomendado: proponer y autorizar 1H.4 para UI mínima sobre estos contratos, sin implementarla automáticamente. Modelo recomendado: SOL; Astra sólo ante un defecto técnico complejo.
