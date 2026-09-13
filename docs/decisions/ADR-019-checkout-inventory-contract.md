# ADR-019 — Contrato persistente y checkout atómico con inventario

Estado: implementado en 1I.3, autorizado expresamente después del bloqueo en bbaf48df8f517a89ebe6f3250822bb7ba88470b7. No autoriza 1I.4.

## Frontera histórica

Schema 11 permite ventas legítimas sin evaluación incluso después de aplicar 011. Ninguna fecha, flag ni ausencia de evaluación permite clasificarlas. Migration 012 añade a sale inventory_evaluation_contract_version (0/1) e inventory_evaluation_id. ALTER TABLE aditivo evita reconstruir Sale y conserva sus FKs entrantes, índices, triggers e historia. Todas las filas existentes reciben 0/NULL; no hay backfill ni consumo retroactivo. Evaluaciones técnicas previamente existentes permanecen intactas.

Nuevos INSERT requieren 1 y puntero no nulo. CHECK limita versiones y coherencia; índice único parcial impide reutilizar evaluación; FK real DEFERRABLE INITIALLY DEFERRED exige evaluación al commit. Triggers defensivos verifican ambas identidades SaleId/CheckoutOperationId y el puntero recíproco. sale_no_update/no_delete protegen también las columnas nuevas. 001–011 no cambian.

## Commit único

CheckoutService conserva autorización sales.complete y el journal comercial. SqliteLocalUnitOfWork ejecuta BEGIN IMMEDIATE. Tras buscar primero el OperationId comercial, un cobro nuevo revalida contexto, productos y categorías. AddSale inserta partidas/pago/cabecera y completa inventario en esa misma conexión/transacción. La cabecera declara versión 1 y usa CheckoutOperationId como EvaluationId, conforme a la identidad V1 de 1I.2.

CompleteInventory lee el singleton dentro de la transacción. DISABLED y PAUSED guardan evaluación DISABLED sin movimientos. ENABLED resuelve tracking/link, artículo y stock MAIN actuales, ejecuta la política Domain checked y guarda NO_CONSUMPTION o CONSUMED. SqliteInventoryStore.WriteOperation sigue siendo el único escritor de ledger/balance; se reutiliza su hash canónico V1, sin duplicarlo. Savepoints internos no hacen commit independiente.

Auditoría sale.completed y, con impresión configurada, snapshot del ticket se guardan antes del único commit. CheckoutReceiptSnapshot sólo captura datos; no renderiza ni imprime. La configuración ausente/deshabilitada conserva el flujo previo sin snapshot anticipado. Winspool y reimpresión permanecen posteriores al commit. No se abre una transacción independiente para movimientos ni se exige inventory.adjust/configure al Cashier.

## Recovery e idempotencia

Una venta existente se compara primero contra el request comercial. Contrato 0/NULL retorna historia sin reevaluar inventario. Contrato 1 exige puntero/evaluación correspondientes, versiones de política/identidad V1, hash de evaluación, snapshots completos, UUIDv5 esperado y, si CONSUMED, operación confirmada y movimientos agregados consistentes con su hash canónico. Se usan exclusivamente datos históricos persistidos; no se consulta tracking, link, factor, estado de artículo, stock ni flag actuales.

Una inconsistencia produce InventoryRecoveryInconsistent sin reparación, recobro ni consumo adicional. Ante respuesta perdida después del commit, una lectura independiente recupera la venta y valida el contrato antes de vaciar el carrito. El journal permanece hasta aceptar el resultado; rollback comprobado conserva carrito y permite reintentar. Bloqueos SQLite se traducen a InventoryConcurrencyConflict. Las excepciones de política conservan código Application y contexto estructurado; la UI no recibe SQL.

## Límites y consecuencias

No existe activación productiva ni preflight en este bloque: el flag inicial permanece DISABLED. Las pruebas ENABLED/PAUSED usan exclusivamente bases temporales sintéticas. Esquemas antiguos sólo se usan para fixtures de upgrade; la aplicación normal migra antes de trabajar. Una versión antigua de la aplicación no puede crear ventas legacy tras 012: se rechazan, no se degradan silenciosamente.

ExpectedCash sigue sumando efectivo aplicado; recibido y cambio no son consumo ni ingreso adicional. Folios participan en rollback. No hay stock negativo, override, recetas, devoluciones, cambios visuales ni efectos físicos nuevos. Sistema de desarrollo, no listo para producción.
